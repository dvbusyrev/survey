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
                        SELECT string_agg(o.name_omsu, ', ')
                        FROM public.omsu_surveys os
                        INNER JOIN public.omsu o
                            ON o.id_omsu = os.id_omsu
                        WHERE os.id_survey = s.id_survey
                    ),
                    'Не указано'
                ) AS name_omsu
            FROM public.surveys s
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
                @"INSERT INTO surveys (name_survey, description, date_create, date_open, date_close)
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
            await InsertOmsuSurveyAssignmentsAsync(connection, transaction, newSurveyId, request.Organizations);

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
              FROM public.surveys s
              WHERE id_survey = @id",
            new { id });

        if (survey == null)
        {
            return null;
        }

        AttachSurveyQuestions(connection, new[] { survey });

        var allOmsu = connection.Query<OmsuSelectionItem>(
            @"SELECT id_omsu AS Id, name_omsu AS Name
              FROM public.omsu
              WHERE block = false
              ORDER BY name_omsu").ToList();

        var selectedOmsu = connection.Query<OmsuSelectionItem>(
            @"SELECT o.id_omsu AS Id, o.name_omsu AS Name
              FROM public.omsu_surveys os
              INNER JOIN public.omsu o
                  ON o.id_omsu = os.id_omsu
              WHERE os.id_survey = @surveyId
              ORDER BY name_omsu",
            new { surveyId = id }).ToList();

        return new SurveyEditPageViewModel
        {
            Survey = survey,
            AllOmsu = allOmsu,
            SelectedOmsuIds = selectedOmsu.Select(o => o.Id).ToList(),
            SelectedOmsuNames = selectedOmsu.Select(o => o.Name).ToList(),
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
                "SELECT EXISTS(SELECT 1 FROM surveys WHERE id_survey = @id)",
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
                @"UPDATE surveys SET
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
            UpdateOmsuSurveyAssignments(connection, transaction, id, model.Organizations);
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
              FROM public.surveys s
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
                  FROM public.surveys s
                  WHERE id_survey = @Id",
                new { Id = id },
                transaction);

            if (originalSurvey == null)
            {
                transaction.Rollback();
                return null;
            }

            var newSurveyId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO surveys (name_survey, description, date_create, date_open, date_close)
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
                @"INSERT INTO public.survey_questions (id_survey, question_order, question_text)
                  SELECT @NewId, question_order, question_text
                  FROM public.survey_questions
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
                @"INSERT INTO public.omsu_surveys (id_omsu, id_survey)
                  SELECT id_omsu, @NewId
                  FROM public.omsu_surveys
                  WHERE id_survey = @OldId
                  ON CONFLICT (id_omsu, id_survey) DO NOTHING",
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
                "DELETE FROM public.survey_questions WHERE id_survey = @id",
                new { id = surveyId },
                transaction);

            connection.Execute(
                "DELETE FROM public.history_answer WHERE id_survey = @id",
                new { id = surveyId },
                transaction);

            connection.Execute(
                "DELETE FROM public.omsu_surveys WHERE id_survey = @id",
                new { id = surveyId },
                transaction);

            var deletedId = connection.ExecuteScalar<int?>(
                "DELETE FROM public.surveys WHERE id_survey = @id RETURNING id_survey",
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

    private void UpdateOmsuSurveyAssignments(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IEnumerable<int> omsuIds)
    {
        connection.Execute(
            "DELETE FROM public.omsu_surveys WHERE id_survey = @surveyId",
            new { surveyId },
            transaction);

        InsertOmsuSurveyAssignmentsAsync(connection, transaction, surveyId, omsuIds)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task InsertOmsuSurveyAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IEnumerable<int> omsuIds)
    {
        foreach (var omsuId in omsuIds.Distinct())
        {
            await connection.ExecuteAsync(
                @"INSERT INTO public.omsu_surveys (id_omsu, id_survey)
                  VALUES (@omsuId, @surveyId)
                  ON CONFLICT (id_omsu, id_survey) DO NOTHING",
                new
                {
                    omsuId,
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
              FROM public.survey_questions
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
              FROM public.survey_questions
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
            "DELETE FROM public.survey_questions WHERE id_survey = @surveyId",
            new { surveyId },
            transaction);

        foreach (var question in questionRows.OrderBy(q => q.QuestionOrder))
        {
            await connection.ExecuteAsync(
                @"INSERT INTO public.survey_questions (id_survey, question_order, question_text)
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
