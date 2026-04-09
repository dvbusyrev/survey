using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Npgsql;

namespace MainProject.Application.UseCases.Surveys;

public sealed class SurveyAdminService : ISurveyAdminService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SurveyAdminService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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

    public async Task<SurveyCommandResult> CreateSurveyAsync(SurveyAddRequest? request)
    {
        if (!TryValidateCreateRequest(
                request,
                out var title,
                out var description,
                out var startDate,
                out var endDate,
                out var organizationIds,
                out var questionRows,
                out var validationError))
        {
            return new SurveyCommandResult
            {
                Message = validationError
            };
        }

        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var newSurveyId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO public.survey (name_survey, description, date_create, date_open, date_close)
                  VALUES (@Title, @Description, NOW(), @StartDate, @EndDate)
                  RETURNING id_survey",
                new
                {
                    Title = title,
                    Description = description,
                    StartDate = startDate,
                    EndDate = endDate
                },
                transaction);

            await ReplaceSurveyQuestionsAsync(connection, transaction, newSurveyId, questionRows);
            await InsertOrganizationSurveyAssignmentsAsync(connection, transaction, newSurveyId, organizationIds);
            transaction.Commit();

            return new SurveyCommandResult
            {
                Success = true,
                Message = "Анкета успешно создана",
                SurveyId = newSurveyId
            };
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

    public SurveyCommandResult UpdateSurvey(int id, SurveyUpdateRequest? model)
    {
        if (!TryValidateUpdateRequest(
                model,
                out var title,
                out var description,
                out var startDate,
                out var endDate,
                out var organizationIds,
                out var questionRows,
                out var validationError))
        {
            return new SurveyCommandResult
            {
                Message = validationError
            };
        }

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
                return new SurveyCommandResult
                {
                    NotFound = true,
                    Message = "Анкета не найдена"
                };
            }

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
                    Title = title,
                    Description = description,
                    StartDate = startDate,
                    EndDate = endDate
                },
                transaction);

            if (affectedRows == 0)
            {
                transaction.Rollback();
                return new SurveyCommandResult
                {
                    NotFound = true,
                    Message = "Анкета не найдена"
                };
            }

            ReplaceSurveyQuestionsAsync(connection, transaction, id, questionRows)
                .GetAwaiter()
                .GetResult();
            UpdateOrganizationSurveyAssignments(connection, transaction, id, organizationIds);
            transaction.Commit();

            return new SurveyCommandResult
            {
                Success = true,
                Message = "Анкета успешно обновлена",
                SurveyId = id
            };
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

    public async Task<SurveyCommandResult> CopySurveyAsync(int id, SurveyCopyRequest? request)
    {
        if (!TryValidateCopyRequest(request, out var startDate, out var endDate, out var validationError))
        {
            return new SurveyCommandResult
            {
                Message = validationError
            };
        }

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
                return new SurveyCommandResult
                {
                    NotFound = true,
                    Message = "Анкета не найдена"
                };
            }

            var newSurveyId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO public.survey (name_survey, description, date_create, date_open, date_close)
                  VALUES (@Name, @Description, NOW(), @StartDate, @EndDate)
                  RETURNING id_survey",
                new
                {
                    Name = $"{originalSurvey.NameSurvey} (Копия)",
                    Description = originalSurvey.Description,
                    StartDate = startDate,
                    EndDate = endDate
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
            return new SurveyCommandResult
            {
                Success = true,
                Message = "Анкета успешно скопирована",
                SurveyId = newSurveyId
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static bool TryValidateCreateRequest(
        SurveyAddRequest? request,
        out string title,
        out string description,
        out DateTime startDate,
        out DateTime endDate,
        out IReadOnlyList<int> organizationIds,
        out IReadOnlyList<SurveyQuestionRow> questionRows,
        out string validationError)
    {
        title = string.Empty;
        description = string.Empty;
        startDate = default;
        endDate = default;
        organizationIds = Array.Empty<int>();
        questionRows = Array.Empty<SurveyQuestionRow>();
        validationError = string.Empty;

        if (request == null)
        {
            validationError = "Неверные данные запроса";
            return false;
        }

        title = request.Title?.Trim() ?? string.Empty;
        description = request.Description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            validationError = "Название анкеты обязательно";
            return false;
        }

        if (!TryParseDateRange(request.StartDate, request.EndDate, out startDate, out endDate, out validationError))
        {
            return false;
        }

        if (!TryNormalizeOrganizationIds(request.Organizations, out organizationIds, out validationError))
        {
            return false;
        }

        return TryBuildQuestionRows(request.Criteria, out questionRows, out validationError);
    }

    private static bool TryValidateUpdateRequest(
        SurveyUpdateRequest? request,
        out string title,
        out string description,
        out DateTime startDate,
        out DateTime endDate,
        out IReadOnlyList<int> organizationIds,
        out IReadOnlyList<SurveyQuestionRow> questionRows,
        out string validationError)
    {
        title = string.Empty;
        description = string.Empty;
        startDate = default;
        endDate = default;
        organizationIds = Array.Empty<int>();
        questionRows = Array.Empty<SurveyQuestionRow>();
        validationError = string.Empty;

        if (request == null)
        {
            validationError = "Данные анкеты не предоставлены";
            return false;
        }

        title = request.Title?.Trim() ?? string.Empty;
        description = request.Description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            validationError = "Название анкеты обязательно";
            return false;
        }

        if (!TryValidateDateRange(request.StartDate, request.EndDate, out validationError))
        {
            return false;
        }

        startDate = request.StartDate;
        endDate = request.EndDate;

        if (!TryNormalizeOrganizationIds(request.Organizations, out organizationIds, out validationError))
        {
            return false;
        }

        return TryBuildQuestionRows(request.Criteria, out questionRows, out validationError);
    }

    private static bool TryValidateCopyRequest(
        SurveyCopyRequest? request,
        out DateTime startDate,
        out DateTime endDate,
        out string validationError)
    {
        startDate = default;
        endDate = default;
        validationError = string.Empty;

        if (request == null)
        {
            validationError = "Неверные данные запроса";
            return false;
        }

        return TryParseDateRange(request.StartDate, request.EndDate, out startDate, out endDate, out validationError);
    }

    private static bool TryParseDateRange(
        string? rawStartDate,
        string? rawEndDate,
        out DateTime startDate,
        out DateTime endDate,
        out string validationError)
    {
        startDate = default;
        endDate = default;
        validationError = string.Empty;

        if (!DateTime.TryParse(rawStartDate, out startDate)
            || !DateTime.TryParse(rawEndDate, out endDate))
        {
            validationError = "Неверный формат даты";
            return false;
        }

        return TryValidateDateRange(startDate, endDate, out validationError);
    }

    private static bool TryValidateDateRange(DateTime startDate, DateTime endDate, out string validationError)
    {
        validationError = string.Empty;

        if (startDate == default || endDate == default)
        {
            validationError = "Неверный формат даты";
            return false;
        }

        if (endDate <= startDate)
        {
            validationError = "Дата окончания должна быть позже даты начала";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeOrganizationIds(
        IEnumerable<int>? rawOrganizationIds,
        out IReadOnlyList<int> organizationIds,
        out string validationError)
    {
        organizationIds = (rawOrganizationIds ?? Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (organizationIds.Count == 0)
        {
            validationError = "Выберите хотя бы одну организацию";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private static bool TryBuildQuestionRows(
        IEnumerable<string>? rawCriteria,
        out IReadOnlyList<SurveyQuestionRow> questionRows,
        out string validationError)
    {
        questionRows = (rawCriteria ?? Array.Empty<string>())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select((text, index) => new SurveyQuestionRow
            {
                QuestionOrder = index + 1,
                QuestionText = text.Trim()
            })
            .ToList();

        if (questionRows.Count == 0)
        {
            validationError = "Добавьте хотя бы один критерий";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    public List<Survey>? DeleteSurvey(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
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

        var surveyIds = surveyList.Select(s => s.IdSurvey).Distinct().ToArray();
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
            survey.Questions = questionLookup.GetValueOrDefault(survey.IdSurvey, new List<SurveyQuestionItem>());
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
