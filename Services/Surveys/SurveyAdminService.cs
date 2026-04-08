using Dapper;
using main_project.Infrastructure.Database;
using main_project.Models;
using Newtonsoft.Json;
using Npgsql;

namespace main_project.Services.Surveys;

public sealed class SurveyAdminService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SurveyAdminService> _logger;

    public SurveyAdminService(IDbConnectionFactory connectionFactory, ILogger<SurveyAdminService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public List<Survey> GetSurveys()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                s.id_survey,
                s.name_survey,
                s.date_create,
                s.date_open,
                s.date_close,
                COALESCE(
                    (
                        SELECT string_agg(o.organization_name, ', ')
                        FROM public.organization_survey os
                        INNER JOIN public.organization o
                            ON o.organization_id = os.organization_id
                        WHERE os.id_survey = s.id_survey
                    ),
                    'Не указано'
                ) AS organization_name
            FROM public.survey s
            WHERE s.date_close >= NOW()
            ORDER BY s.id_survey DESC;";

        var surveys = connection.Query<Survey>(sql).ToList();
        AttachSurveyQuestions(connection, surveys);
        return surveys;
    }

    public async Task<int> CreateSurveyAsync(SurveyAddRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var questionRows = request.Criteria
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select((text, index) => new SurveyQuestionRow
                {
                    QuestionOrder = index + 1,
                    QuestionText = text.Trim()
                })
                .ToList();
            var newSurveyId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO public.survey (name_survey, description, date_create, date_open, date_close)
                  VALUES (@Title, @Description, NOW(), @StartDate, @EndDate)
                  RETURNING id_survey",
                new
                {
                    request.Title,
                    request.Description,
                    StartDate = DateTime.Parse(request.StartDate),
                    EndDate = DateTime.Parse(request.EndDate)
                },
                transaction);

            await ReplaceSurveyQuestionsAsync(connection, transaction, newSurveyId, questionRows);
            await InsertOrganizationSurveyAssignmentsAsync(connection, transaction, newSurveyId, request.Organizations);

            await LogSurveyCreationAsync(connection, transaction, newSurveyId, request.Title);
            transaction.Commit();

            return newSurveyId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public SurveyEditPageViewModel? GetSurveyEditPage(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        var survey = connection.QueryFirstOrDefault<Survey>(
            @"SELECT
                id_survey,
                name_survey,
                date_create,
                date_open,
                date_close,
                description
              FROM public.survey s
              WHERE id_survey = @id",
            new { id });

        if (survey == null)
        {
            return null;
        }

        AttachSurveyQuestions(connection, new[] { survey });

        var allOrganization = connection.Query<OrganizationSelectionItem>(
            @"SELECT organization_id AS Id, organization_name AS Name
              FROM public.organization
              WHERE block = false
              ORDER BY organization_name").ToList();

        var selectedOrganization = connection.Query<OrganizationSelectionItem>(
            @"SELECT o.organization_id AS Id, o.organization_name AS Name
              FROM public.organization_survey os
              INNER JOIN public.organization o
                  ON o.organization_id = os.organization_id
              WHERE os.id_survey = @surveyId
              ORDER BY organization_name",
            new { surveyId = id }).ToList();

        return new SurveyEditPageViewModel
        {
            Survey = survey,
            AllOrganization = allOrganization,
            SelectedOrganizationIds = selectedOrganization.Select(o => o.Id).ToList(),
            SelectedOrganizationNames = selectedOrganization.Select(o => o.Name).ToList(),
            Criteria = GetCriteria(connection, id)
        };
    }

    public bool UpdateSurvey(int id, SurveyUpdateRequest model)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var exists = connection.ExecuteScalar<bool>(
                "SELECT EXISTS(SELECT 1 FROM public.survey WHERE id_survey = @id)",
                new { id },
                transaction);

            if (!exists)
            {
                return false;
            }

            var questionRows = model.Criteria
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select((text, index) => new SurveyQuestionRow
                {
                    QuestionOrder = index + 1,
                    QuestionText = text.Trim()
                })
                .ToList();
            var affectedRows = connection.Execute(
                @"UPDATE public.survey SET
                    name_survey = @Title,
                    description = @Description,
                    date_open = @StartDate::date,
                    date_close = @EndDate::date
                WHERE id_survey = @id",
                new
                {
                    id,
                    model.Title,
                    model.Description,
                    model.StartDate,
                    model.EndDate
                },
                transaction);

            if (affectedRows == 0)
            {
                transaction.Rollback();
                return false;
            }

            ReplaceSurveyQuestionsAsync(connection, transaction, id, questionRows)
                .GetAwaiter()
                .GetResult();
            UpdateOrganizationSurveyAssignments(connection, transaction, id, model.Organizations);
            transaction.Commit();

            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public Survey? GetSurveyForCopy(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        var survey = connection.QueryFirstOrDefault<Survey>(
            @"SELECT
                  s.id_survey,
                  s.name_survey,
                  s.description,
                  s.date_open,
                  s.date_close
              FROM public.survey s
              WHERE id_survey = @id",
            new { id });

        if (survey != null)
        {
            AttachSurveyQuestions(connection, new[] { survey });
        }

        return survey;
    }

    public async Task<int?> CopySurveyAsync(int id, SurveyCopyRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var originalSurvey = await connection.QueryFirstOrDefaultAsync<Survey>(
                @"SELECT
                      s.id_survey,
                      s.name_survey,
                      s.description
                  FROM public.survey s
                  WHERE id_survey = @Id",
                new { Id = id },
                transaction);

            if (originalSurvey == null)
            {
                transaction.Rollback();
                return null;
            }

            var newSurveyId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO public.survey (name_survey, description, date_create, date_open, date_close)
                  VALUES (@Name, @Description, NOW(), @StartDate, @EndDate)
                  RETURNING id_survey",
                new
                {
                    Name = $"{originalSurvey.name_survey} (Копия)",
                    Description = originalSurvey.description,
                    StartDate = DateTime.Parse(request.StartDate),
                    EndDate = DateTime.Parse(request.EndDate)
                },
                transaction);

            await connection.ExecuteAsync(
                @"INSERT INTO public.survey_question (id_survey, question_order, question_text)
                  SELECT @NewId, question_order, question_text
                  FROM public.survey_question
                  WHERE id_survey = @OldId
                  ON CONFLICT (id_survey, question_order) DO UPDATE
                  SET question_text = EXCLUDED.question_text",
                new
                {
                    NewId = newSurveyId,
                    OldId = id
                },
                transaction);

            await connection.ExecuteAsync(
                @"INSERT INTO public.organization_survey (organization_id, id_survey)
                  SELECT organization_id, @NewId
                  FROM public.organization_survey
                  WHERE id_survey = @OldId
                  ON CONFLICT (organization_id, id_survey) DO NOTHING",
                new
                {
                    NewId = newSurveyId,
                    OldId = id
                },
                transaction);

            transaction.Commit();
            return newSurveyId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public List<Survey>? DeleteSurvey(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            connection.Execute(
                "DELETE FROM public.survey_question WHERE id_survey = @id",
                new { id = surveyId },
                transaction);

            connection.Execute(
                "DELETE FROM public.answer WHERE id_survey = @id",
                new { id = surveyId },
                transaction);

            connection.Execute(
                "DELETE FROM public.organization_survey WHERE id_survey = @id",
                new { id = surveyId },
                transaction);

            var deletedId = connection.ExecuteScalar<int?>(
                "DELETE FROM public.survey WHERE id_survey = @id RETURNING id_survey",
                new { id = surveyId },
                transaction);

            if (!deletedId.HasValue)
            {
                transaction.Rollback();
                return null;
            }

            transaction.Commit();
            return GetSurveys();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task LogSurveyCreationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        string surveyTitle)
    {
        try
        {
            await connection.ExecuteAsync(
                @"INSERT INTO log (target_type, event_type, date, description, extra_data)
                  VALUES ('survey', 'create', NOW(), @Description, @ExtraData::jsonb)",
                new
                {
                    Description = $"Создана новая анкета: {surveyTitle}",
                    ExtraData = JsonConvert.SerializeObject(new
                    {
                        survey_id = surveyId,
                        survey_title = surveyTitle
                    })
                },
                transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при записи в лог о создании анкеты");
        }
    }

    private void UpdateOrganizationSurveyAssignments(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IEnumerable<int> organizationIds)
    {
        connection.Execute(
            "DELETE FROM public.organization_survey WHERE id_survey = @surveyId",
            new { surveyId },
            transaction);

        InsertOrganizationSurveyAssignmentsAsync(connection, transaction, surveyId, organizationIds)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task InsertOrganizationSurveyAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IEnumerable<int> organizationIds)
    {
        foreach (var organizationId in organizationIds.Distinct())
        {
            await connection.ExecuteAsync(
                @"INSERT INTO public.organization_survey (organization_id, id_survey)
                  VALUES (@organizationId, @surveyId)
                  ON CONFLICT (organization_id, id_survey) DO NOTHING",
                new
                {
                    organizationId,
                    surveyId
                },
                transaction);
        }
    }

    private static IReadOnlyList<string> GetCriteria(
        NpgsqlConnection connection,
        int surveyId)
    {
        return connection.Query<string>(
            @"SELECT question_text
              FROM public.survey_question
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId }).ToList();
    }

    private static void AttachSurveyQuestions(
        NpgsqlConnection connection,
        IEnumerable<Survey> surveys)
    {
        var surveyList = surveys.ToList();
        if (surveyList.Count == 0)
        {
            return;
        }

        var surveyIds = surveyList.Select(s => s.id_survey).Distinct().ToArray();
        var questionRows = connection.Query<SurveyQuestionLookupRow>(
            @"SELECT
                  id_survey AS IdSurvey,
                  question_order AS QuestionOrder,
                  question_text AS QuestionText
              FROM public.survey_question
              WHERE id_survey = ANY(@surveyIds)
              ORDER BY id_survey, question_order",
            new { surveyIds });

        var questionLookup = questionRows
            .GroupBy(row => row.IdSurvey)
            .ToDictionary(
                group => group.Key,
                group => (List<SurveyQuestionItem>)group
                    .Select(row => new SurveyQuestionItem
                    {
                        Id = row.QuestionOrder,
                        Text = row.QuestionText
                    })
                    .ToList());

        foreach (var survey in surveyList)
        {
            survey.Questions = questionLookup.GetValueOrDefault(survey.id_survey, new List<SurveyQuestionItem>());
        }
    }

    private sealed class SurveyQuestionLookupRow
    {
        public int IdSurvey { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
    }

    private static async Task ReplaceSurveyQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IReadOnlyList<SurveyQuestionRow> questionRows)
    {
        await connection.ExecuteAsync(
            "DELETE FROM public.survey_question WHERE id_survey = @surveyId",
            new { surveyId },
            transaction);

        foreach (var question in questionRows.OrderBy(q => q.QuestionOrder))
        {
            await connection.ExecuteAsync(
                @"INSERT INTO public.survey_question (id_survey, question_order, question_text)
                  VALUES (@surveyId, @questionOrder, @questionText)",
                new
                {
                    surveyId,
                    questionOrder = question.QuestionOrder,
                    questionText = question.QuestionText
                },
                transaction);
        }
    }
}
