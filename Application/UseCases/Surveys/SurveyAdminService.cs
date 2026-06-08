using System.Data;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
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

    public SurveyListPageViewModel GetSurveysPage(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds)
    {
        using var connection = _connectionFactory.CreateConnection();

        var selectedOrganizationIds = ParseSelectedIds(organizationIds);
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeSurveySortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeSurveySortDirection(null, normalizedSortBy);

        var parameters = new DynamicParameters();
        parameters.Add("selectedOrganizationIds", selectedOrganizationIds.ToArray());
        parameters.Add("hasOrganizationFilter", selectedOrganizationIds.Count > 0);

        var organizationOptions = GetActiveSurveyOrganizationOptions(connection);
        var totalCount = connection.ExecuteScalar<int>(
            $"{ActiveSurveyRowsCte} SELECT COUNT(*) FROM survey_rows WHERE {BuildSurveyOrganizationFilterPredicate()};",
            parameters);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        parameters.Add("pageSize", pageWindow.PageSize);
        parameters.Add("offset", pageWindow.Offset);

        var pageRows = connection.Query<SurveyTablePageRow>(
            $"""
            {ActiveSurveyRowsCte}
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                organization_ids AS OrganizationIds,
                organization_names AS OrganizationNames
            FROM survey_rows
            WHERE {BuildSurveyOrganizationFilterPredicate()}
            ORDER BY {BuildSurveyOrderBy(normalizedSortBy, normalizedSortDirection)}
            LIMIT @pageSize OFFSET @offset;
            """,
            parameters).ToList();

        return new SurveyListPageViewModel
        {
            SurveyRows = pageRows.Select(MapSurveyTablePageRow).ToList(),
            CurrentPage = pageWindow.CurrentPage,
            TotalPages = pageWindow.TotalPages,
            TotalCount = pageWindow.TotalCount,
            PageSize = pageWindow.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            FilterState = new ServerTableFilterStateViewModel
            {
                BasePath = "/surveys",
                EnableOrganizationFilter = true,
                OrganizationOptions = organizationOptions,
                SelectedOrganizationIds = selectedOrganizationIds
            }
        };
    }

    public List<Survey> GetSurveys()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                s.id_survey,
                s.name_survey,
                ss.date_begin,
                ss.date_end,
                COALESCE(
                    (
                        SELECT string_agg(
                            COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name),
                            ', '
                        )
                        FROM public.organization_survey os
                        INNER JOIN public.organization o
                            ON o.id_organization = os.id_organization
                        WHERE os.id_survey = s.id_survey
                    ),
                    'Не указано'
                ) AS organization_name
            FROM public.survey s
            LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
            WHERE EXISTS (
                SELECT 1
                FROM public.organization_survey os
                WHERE os.id_survey = s.id_survey
                  AND (os.date_end IS NULL OR os.date_end >= CURRENT_DATE)
            )
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
                @"INSERT INTO public.survey (name_survey, description)
                  VALUES (@Title, @Description)
                  RETURNING id_survey",
                new
                {
                    Title = title,
                    Description = description
                },
                transaction);

            await ReplaceSurveyQuestionsAsync(connection, transaction, newSurveyId, questionRows);
            await InsertOrganizationSurveyAssignmentsAsync(
                connection,
                transaction,
                newSurveyId,
                organizationIds,
                startDate,
                endDate);
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
                s.id_survey,
                s.name_survey,
                COALESCE(ss.date_begin, CURRENT_DATE) AS date_begin,
                ss.date_end AS date_end,
                s.description
              FROM public.survey s
              LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
              WHERE s.id_survey = @id",
            new { id });

        if (survey == null)
        {
            return null;
        }

        AttachSurveyQuestions(connection, new[] { survey });

        var allOrganization = connection.Query<OrganizationSelectionItem>(
            @"SELECT
                  id_organization AS Id,
                  COALESCE(NULLIF(organization_short_name, ''), organization_name) AS Name
              FROM public.organization
              WHERE date_end IS NULL
                 OR date_end >= CURRENT_DATE
                 OR id_organization IN (
                      SELECT id_organization
                      FROM public.organization_survey
                      WHERE id_survey = @surveyId
                  )
              ORDER BY COALESCE(NULLIF(organization_short_name, ''), organization_name)",
            new { surveyId = id }).ToList();

        var selectedOrganization = connection.Query<OrganizationSelectionItem>(
            @"SELECT
                  o.id_organization AS Id,
                  COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS Name
              FROM public.organization_survey os
              INNER JOIN public.organization o
                  ON o.id_organization = os.id_organization
              WHERE os.id_survey = @surveyId
              ORDER BY COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)",
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
            var affectedRows = connection.Execute(
                @"UPDATE public.survey SET
                    name_survey = @Title,
                    description = @Description
                WHERE id_survey = @id",
                new
                {
                    id,
                    Title = title,
                    Description = description
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
            SynchronizeOrganizationSurveyAssignments(
                connection,
                transaction,
                id,
                organizationIds,
                startDate,
                endDate);
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

    public SurveyCommandResult UpdateActiveSurveysWorkPeriod(SurveyWorkPeriodRequest? request)
    {
        if (request == null)
        {
            return new SurveyCommandResult
            {
                Message = "Неверные данные запроса"
            };
        }

        if (!TryValidateDateRange(request.DateBegin, request.DateEnd, out var validationError))
        {
            return new SurveyCommandResult
            {
                Message = validationError
            };
        }

        if (request.DateEnd.Date < DateTime.Today)
        {
            return new SurveyCommandResult
            {
                Message = "Дата конца не может быть раньше сегодняшней даты"
            };
        }

        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var affectedSurveyCount = connection.ExecuteScalar<int>(
                @"WITH active_survey AS (
                      SELECT DISTINCT id_survey
                      FROM public.organization_survey
                      WHERE date_end IS NULL OR date_end >= CURRENT_DATE
                  ),
                  updated AS (
                      UPDATE public.organization_survey os
                      SET
                          date_begin = @DateBegin,
                          date_end = @DateEnd
                      FROM active_survey active
                      WHERE os.id_survey = active.id_survey
                      RETURNING os.id_survey
                  )
                  SELECT COUNT(DISTINCT id_survey)
                  FROM updated",
                new
                {
                    DateBegin = request.DateBegin.Date,
                    DateEnd = request.DateEnd.Date
                },
                transaction);

            transaction.Commit();

            return new SurveyCommandResult
            {
                Success = true,
                Message = affectedSurveyCount == 0
                    ? "Активные анкеты не найдены"
                    : "Период работы активных анкет сохранён"
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
                  COALESCE(ss.date_begin, CURRENT_DATE) AS date_begin,
                  ss.date_end AS date_end
              FROM public.survey s
              LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
              WHERE s.id_survey = @id",
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
                @"INSERT INTO public.survey (name_survey, description)
                  VALUES (@Name, @Description)
                  RETURNING id_survey",
                new
                {
                    Name = $"{originalSurvey.NameSurvey} (Копия)",
                    Description = originalSurvey.Description
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

            var organizationIds = (await connection.QueryAsync<int>(
                @"SELECT id_organization
                  FROM public.organization_survey
                  WHERE id_survey = @OldId",
                new { OldId = id },
                transaction)).ToArray();

            await InsertOrganizationSurveyAssignmentsAsync(
                connection,
                transaction,
                newSurveyId,
                organizationIds,
                startDate,
                endDate);

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
            validationError = "Дата конца должна быть позже даты начала";
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

    private static void SynchronizeOrganizationSurveyAssignments(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IEnumerable<int> organizationIds,
        DateTime dateBegin,
        DateTime dateEnd)
    {
        var normalizedOrganizationIds = organizationIds.Distinct().ToArray();

        connection.Execute(
            @"DELETE FROM public.organization_survey
              WHERE id_survey = @surveyId
                AND NOT (id_organization = ANY(@organizationIds))",
            new
            {
                surveyId,
                organizationIds = normalizedOrganizationIds
            },
            transaction);

        InsertOrganizationSurveyAssignmentsAsync(
                connection,
                transaction,
                surveyId,
                normalizedOrganizationIds,
                dateBegin,
                dateEnd)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task InsertOrganizationSurveyAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IEnumerable<int> organizationIds,
        DateTime dateBegin,
        DateTime dateEnd)
    {
        foreach (var organizationId in organizationIds.Distinct())
        {
            await connection.ExecuteAsync(
                @"INSERT INTO public.organization_survey (id_organization, id_survey, date_begin, date_end)
                  VALUES (@organizationId, @surveyId, @dateBegin, @dateEnd)
                  ON CONFLICT (id_organization, id_survey) DO UPDATE
                  SET
                      date_begin = EXCLUDED.date_begin,
                      date_end = EXCLUDED.date_end",
                new
                {
                    organizationId,
                    surveyId,
                    dateBegin = dateBegin.Date,
                    dateEnd = dateEnd.Date
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

    private static List<SurveyTableRowViewModel> BuildSurveyTableRows(IEnumerable<SurveyAssignmentListRow> rows)
    {
        return rows
            .GroupBy(row => new
            {
                row.IdSurvey,
                row.NameSurvey,
                row.DateBegin,
                row.DateEnd
            })
            .Select(group => new SurveyTableRowViewModel
            {
                IdSurvey = group.Key.IdSurvey,
                NameSurvey = group.Key.NameSurvey ?? string.Empty,
                DateBegin = group.Key.DateBegin,
                DateEnd = group.Key.DateEnd,
                OrganizationIds = group
                    .Where(row => row.OrganizationId > 0)
                    .Select(row => row.OrganizationId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray(),
                OrganizationNames = group
                    .Where(row => !string.IsNullOrWhiteSpace(row.OrganizationName))
                    .Select(row => row.OrganizationName!.Trim())
                    .Distinct(AppListPaging.RuStringComparer)
                    .OrderBy(name => name, AppListPaging.RuStringComparer)
                    .ToArray()
            })
            .ToList();
    }

    private const string ActiveSurveyRowsCte = """
        WITH survey_rows AS (
            SELECT
                s.id_survey,
                s.name_survey,
                ss.date_begin,
                ss.date_end,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT os2.id_organization
                        FROM public.organization_survey os2
                        WHERE os2.id_survey = s.id_survey
                          AND os2.id_organization IS NOT NULL
                        ORDER BY os2.id_organization
                    ),
                    ARRAY[]::integer[]
                ) AS organization_ids,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT COALESCE(NULLIF(o2.organization_short_name, ''), o2.organization_name)
                        FROM public.organization_survey os2
                        INNER JOIN public.organization o2
                            ON o2.id_organization = os2.id_organization
                        WHERE os2.id_survey = s.id_survey
                          AND COALESCE(NULLIF(o2.organization_short_name, ''), o2.organization_name) IS NOT NULL
                        ORDER BY COALESCE(NULLIF(o2.organization_short_name, ''), o2.organization_name)
                    ),
                    ARRAY[]::text[]
                ) AS organization_names
            FROM public.survey s
            LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
            WHERE EXISTS (
                SELECT 1
                FROM public.organization_survey active_os
                WHERE active_os.id_survey = s.id_survey
                  AND (active_os.date_end IS NULL OR active_os.date_end >= CURRENT_DATE)
            )
        )
        """;

    private static IReadOnlyList<SelectionOption> GetActiveSurveyOrganizationOptions(IDbConnection connection)
    {
        return BuildSelectionOptions(connection.Query<SelectionOption>(
            """
            SELECT DISTINCT
                o.id_organization AS Id,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS Name
            FROM public.organization_survey os
            INNER JOIN public.organization o
                ON o.id_organization = os.id_organization
            WHERE EXISTS (
                SELECT 1
                FROM public.organization_survey active_os
                WHERE active_os.id_survey = os.id_survey
                  AND (active_os.date_end IS NULL OR active_os.date_end >= CURRENT_DATE)
            );
            """));
    }

    private static string BuildSurveyOrganizationFilterPredicate()
    {
        return "(@hasOrganizationFilter = false OR organization_ids && @selectedOrganizationIds)";
    }

    private static SurveyTableRowViewModel MapSurveyTablePageRow(SurveyTablePageRow row)
    {
        return new SurveyTableRowViewModel
        {
            IdSurvey = row.IdSurvey,
            NameSurvey = row.NameSurvey ?? string.Empty,
            DateBegin = row.DateBegin,
            DateEnd = row.DateEnd,
            OrganizationIds = row.OrganizationIds ?? Array.Empty<int>(),
            OrganizationNames = row.OrganizationNames ?? Array.Empty<string>()
        };
    }

    private static string BuildSurveyOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.Ordinal)
            ? "DESC"
            : "ASC";

        return sortBy switch
        {
            SurveyListSortFields.Name => $"name_survey {direction}, id_survey DESC",
            SurveyListSortFields.DateBegin => $"date_begin {direction} NULLS LAST, id_survey DESC",
            SurveyListSortFields.DateEnd => $"date_end {direction} NULLS LAST, id_survey DESC",
            _ => "id_survey DESC"
        };
    }

    private static IReadOnlyList<int> ParseSelectedIds(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? Array.Empty<int>()
            : rawValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
    }

    private static IReadOnlyList<SelectionOption> BuildSelectionOptions(IEnumerable<SelectionOption> options)
    {
        return options
            .Where(option => option.Id > 0 && !string.IsNullOrWhiteSpace(option.Name))
            .GroupBy(option => option.Id)
            .Select(group => group.First())
            .OrderBy(option => option.Name, AppListPaging.RuStringComparer)
            .ThenBy(option => option.Id)
            .ToList();
    }

    private static string NormalizeSurveySortField(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            SurveyListSortFields.Name => SurveyListSortFields.Name,
            SurveyListSortFields.DateBegin => SurveyListSortFields.DateBegin,
            SurveyListSortFields.DateEnd => SurveyListSortFields.DateEnd,
            _ => SurveyListSortFields.Default
        };
    }

    private static string NormalizeSurveySortDirection(string? sortDirection, string sortField)
    {
        if (string.Equals(sortDirection?.Trim(), "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        if (string.Equals(sortDirection?.Trim(), "desc", StringComparison.OrdinalIgnoreCase))
        {
            return "desc";
        }

        return sortField switch
        {
            SurveyListSortFields.Name => "asc",
            _ => "desc"
        };
    }

    private static List<SurveyTableRowViewModel> SortSurveyRows(
        IEnumerable<SurveyTableRowViewModel> rows,
        string sortBy,
        string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.Ordinal);
        IOrderedEnumerable<SurveyTableRowViewModel> orderedRows = sortBy switch
        {
            SurveyListSortFields.Name => descending
                ? rows.OrderByDescending(row => row.NameSurvey, AppListPaging.RuStringComparer)
                : rows.OrderBy(row => row.NameSurvey, AppListPaging.RuStringComparer),
            SurveyListSortFields.DateBegin => descending
                ? rows.OrderByDescending(row => row.DateBegin)
                : rows.OrderBy(row => row.DateBegin),
            SurveyListSortFields.DateEnd => descending
                ? rows.OrderByDescending(row => row.DateEnd ?? DateTime.MinValue)
                : rows.OrderBy(row => row.DateEnd ?? DateTime.MaxValue),
            _ => rows.OrderByDescending(row => row.IdSurvey)
        };

        return orderedRows
            .ThenByDescending(row => row.IdSurvey)
            .ToList();
    }

    private sealed class SurveyQuestionLookupRow
    {
        public int IdSurvey { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
    }

    private sealed class SurveyAssignmentListRow
    {
        public int IdSurvey { get; init; }
        public string? NameSurvey { get; init; }
        public DateTime DateBegin { get; init; }
        public DateTime? DateEnd { get; init; }
        public int OrganizationId { get; init; }
        public string? OrganizationName { get; init; }
    }

    private sealed class SurveyTablePageRow
    {
        public int IdSurvey { get; init; }
        public string? NameSurvey { get; init; }
        public DateTime DateBegin { get; init; }
        public DateTime? DateEnd { get; init; }
        public int[]? OrganizationIds { get; init; }
        public string[]? OrganizationNames { get; init; }
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
