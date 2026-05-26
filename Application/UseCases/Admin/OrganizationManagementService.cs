using System.Data;
using System.Text;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Admin;

public sealed class OrganizationManagementService : IOrganizationManagementService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrganizationManagementService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public OrganizationListPageViewModel GetActiveOrganizationsPage(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool openAddOrganizationModal = false)
    {
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeOrganizationSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeOrganizationSortDirection(null, normalizedSortBy);
        var organizations = SortOrganizations(GetOrganizations(includeArchived: false), sortBy, sortDirection);
        var pageSlice = AppListPaging.Slice(organizations, currentPage);

        return new OrganizationListPageViewModel
        {
            Organizations = pageSlice.Items,
            OpenAddOrganizationModal = openAddOrganizationModal,
            CurrentPage = pageSlice.CurrentPage,
            TotalPages = pageSlice.TotalPages,
            TotalCount = pageSlice.TotalCount,
            PageSize = pageSlice.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            ViewModeIsArchive = false
        };
    }

    public OrganizationSurveyAssignmentsPageViewModel GetOrganizationSurveyAssignmentsPage()
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = GetOrganizationSurveyAssignmentRows(connection);

        return new OrganizationSurveyAssignmentsPageViewModel
        {
            Organizations = rows
                .GroupBy(row => new { row.OrganizationId, row.OrganizationName })
                .Select(group => new OrganizationSurveyGroupViewModel
                {
                    OrganizationId = group.Key.OrganizationId,
                    OrganizationName = group.Key.OrganizationName,
                    Surveys = group
                        .Where(row =>
                            row.SurveyId.HasValue
                            && row.AssignmentDateEnd.HasValue
                            && !string.IsNullOrWhiteSpace(row.SurveyName))
                        .Select(MapOrganizationSurveyItem)
                        .ToList()
                })
                .ToList()
        };
    }

    public OrganizationListPageViewModel GetArchivedOrganizationsPage(
        int currentPage,
        string? sortBy,
        string? sortDirection)
    {
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeOrganizationSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeOrganizationSortDirection(null, normalizedSortBy);
        var organizations = SortOrganizations(GetOrganizations(includeArchived: true), sortBy, sortDirection);
        var pageSlice = AppListPaging.Slice(organizations, currentPage);

        return new OrganizationListPageViewModel
        {
            Organizations = pageSlice.Items,
            CurrentPage = pageSlice.CurrentPage,
            TotalPages = pageSlice.TotalPages,
            TotalCount = pageSlice.TotalCount,
            PageSize = pageSlice.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            ViewModeIsArchive = true
        };
    }

    public IReadOnlyList<Organization> GetArchivedOrganizations()
    {
        return GetOrganizations(includeArchived: true);
    }

    public IReadOnlyList<OrganizationDataResponse> GetOrganizationOptions()
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.Query<OrganizationDataResponse>(
            """
            SELECT
                id_organization AS Id,
                COALESCE(NULLIF(organization_short_name, ''), organization_name) AS Name
            FROM public.organization
            WHERE date_end IS NULL
               OR date_end >= CURRENT_DATE
            ORDER BY COALESCE(NULLIF(organization_short_name, ''), organization_name);
            """).ToList();
    }

    public Organization? GetOrganizationById(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.QueryFirstOrDefault<Organization>(
            """
            SELECT
                organization_name,
                organization_short_name,
                email,
                date_begin,
                date_end,
                id_organization AS OrganizationId
            FROM public.organization
            WHERE id_organization = @id;
            """,
            new { id });
    }

    public OperationResult CreateOrganization(OrganizationSaveRequest request)
    {
        if (!TryValidateOrganizationRequest(request, out var dateBegin, out var dateEnd, out var validationError))
        {
            return new OperationResult
            {
                Success = false,
                Message = validationError,
                Error = validationError
            };
        }

        using var connection = _connectionFactory.CreateConnection();

        var organizationId = connection.ExecuteScalar<int>(
            """
            INSERT INTO public.organization (
                organization_name,
                organization_short_name,
                email,
                date_begin,
                date_end
            )
            VALUES (
                @name,
                @shortName,
                @email,
                @dateBegin,
                @dateEnd
            )
            RETURNING id_organization;
            """,
            new
            {
                name = request.Name.Trim(),
                shortName = string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim(),
                email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                dateBegin,
                dateEnd
            });

        return new OperationResult
        {
            Success = true,
            Message = "Организация успешно создана.",
            EntityId = organizationId,
            ShouldReload = true
        };
    }

    public OperationResult UpdateOrganization(int id, OrganizationSaveRequest request)
    {
        if (!TryValidateOrganizationRequest(request, out var dateBegin, out var dateEnd, out var validationError))
        {
            return new OperationResult
            {
                Success = false,
                Message = validationError,
                Error = validationError
            };
        }

        using var connection = _connectionFactory.CreateConnection();

        var affectedRows = connection.Execute(
            """
            UPDATE public.organization
            SET
                organization_name = @name,
                organization_short_name = @shortName,
                email = @email,
                date_begin = @dateBegin,
                date_end = @dateEnd
            WHERE id_organization = @id;
            """,
            new
            {
                id,
                name = request.Name.Trim(),
                shortName = string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim(),
                email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                dateBegin,
                dateEnd
            });

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Организация успешно обновлена"
                : "Организация не найдена."
        };
    }

    public OperationResult ArchiveOrganization(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        var surveyNames = GetOrganizationAssignedSurveyNames(connection, id);
        var userNames = GetOrganizationAssignedUserNames(connection, id);

        if (surveyNames.Count > 0 || userNames.Count > 0)
        {
            var message = BuildArchiveRestrictedMessage(surveyNames, userNames);

            return new OperationResult
            {
                Success = false,
                Message = message,
                Error = message,
                Code = "organization_in_use"
            };
        }

        var affectedRows = connection.Execute(
            """
            UPDATE public.organization
            SET date_end = CASE
                WHEN date_end IS NULL OR date_end >= CURRENT_DATE
                    THEN (CURRENT_DATE - INTERVAL '1 day')::date
                ELSE date_end
            END
            WHERE id_organization = @id;
            """,
            new { id });

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Организация успешно удалена."
                : "Произошла ошибка при удалении организации."
        };
    }

    public OrganizationSurveyEndDateUpdateResult UpdateOrganizationSurveyEndDates(
        OrganizationSurveyEndDateUpdateRequest request)
    {
        var validationErrors = ValidateOrganizationSurveyEndDateUpdateRequest(request);
        if (validationErrors.Count > 0)
        {
            return new OrganizationSurveyEndDateUpdateResult
            {
                Success = false,
                Message = "Не удалось обновить дату конца анкет.",
                Errors = validationErrors
            };
        }

        var assignments = request.Assignments
            .Where(item => item.OrganizationId > 0 && item.SurveyId > 0)
            .DistinctBy(item => (item.OrganizationId, item.SurveyId))
            .ToList();

        var requestedEndDate = DateTime.Parse(request.DateEnd).Date;

        using var connection = _connectionFactory.CreateConnection();
        var organizationIds = assignments.Select(item => item.OrganizationId).Distinct().ToArray();
        var assignmentLookup = GetOrganizationSurveyAssignmentRows(connection, organizationIds)
            .Where(row => row.SurveyId.HasValue)
            .ToDictionary(
                row => (row.OrganizationId, row.SurveyId!.Value),
                row => row);

        var missingAssignments = assignments
            .Where(item => !assignmentLookup.ContainsKey((item.OrganizationId, item.SurveyId)))
            .Select(item => $"Связка организации {item.OrganizationId} и анкеты {item.SurveyId} не найдена.")
            .ToList();

        if (missingAssignments.Count > 0)
        {
            return new OrganizationSurveyEndDateUpdateResult
            {
                Success = false,
                Message = "Не удалось обновить дату конца анкет.",
                Errors = missingAssignments
            };
        }

        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var assignment in assignments)
            {
                var updatedRows = connection.Execute(
                    """
                    UPDATE public.organization_survey
                    SET date_end = @requestedEndDate::date
                    WHERE id_organization = @organizationId
                      AND id_survey = @surveyId;
                    """,
                    new
                    {
                        requestedEndDate,
                        organizationId = assignment.OrganizationId,
                        surveyId = assignment.SurveyId
                    },
                    transaction);

                if (updatedRows == 0)
                {
                    transaction.Rollback();

                    return new OrganizationSurveyEndDateUpdateResult
                    {
                        Success = false,
                        Message = "Не удалось обновить дату конца анкет.",
                        Errors = [$"Связка организации {assignment.OrganizationId} и анкеты {assignment.SurveyId} не найдена."]
                    };
                }
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();

            return new OrganizationSurveyEndDateUpdateResult
            {
                Success = false,
                Message = "Не удалось обновить дату конца анкет.",
                Error = ex.Message
            };
        }

        return new OrganizationSurveyEndDateUpdateResult
        {
            Success = true,
            Message = "Дата конца для выбранных анкет обновлена.",
            UpdatedAssignments = assignments
                .Select(assignment =>
                    BuildOrganizationSurveyAssignmentUpdateItem(
                        assignmentLookup[(assignment.OrganizationId, assignment.SurveyId)],
                        requestedEndDate))
                .ToList()
        };
    }

    private IReadOnlyList<Organization> GetOrganizations(bool includeArchived)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.Query<Organization>(
            """
            SELECT
                o.id_organization AS OrganizationId,
                o.organization_name,
                o.organization_short_name,
                o.date_begin,
                o.date_end,
                COALESCE((
                    SELECT string_agg(s.name_survey, ', ' ORDER BY s.name_survey)
                    FROM public.organization_survey os
                    INNER JOIN public.survey s
                        ON s.id_survey = os.id_survey
                    WHERE os.id_organization = o.id_organization
                ), 'Не указано') AS survey_names,
                o.email
            FROM public.organization o
            WHERE (
                @includeArchived = true
                AND o.date_end < CURRENT_DATE
            ) OR (
                @includeArchived = false
                AND (o.date_end IS NULL OR o.date_end >= CURRENT_DATE)
            )
            ORDER BY o.organization_name;
            """,
            new { includeArchived }).ToList();
    }

    private static IReadOnlyList<OrganizationSurveyAssignmentRow> GetOrganizationSurveyAssignmentRows(
        IDbConnection connection,
        IReadOnlyCollection<int>? organizationIds = null)
    {
        var sql = new StringBuilder(
            """
            SELECT
                o.id_organization AS OrganizationId,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS organization_name,
                s.id_survey AS survey_id,
                s.name_survey AS survey_name,
                os.date_end AS assignment_date_end
            FROM public.organization o
            LEFT JOIN public.organization_survey os
                ON os.id_organization = o.id_organization
               AND NOT EXISTS (
                    SELECT 1
                    FROM public.answer a
                    WHERE a.id_organization_survey = os.id_organization_survey
               )
            LEFT JOIN public.survey s
                ON s.id_survey = os.id_survey
            WHERE (o.date_end IS NULL OR o.date_end >= CURRENT_DATE)
            """);

        if (organizationIds is { Count: > 0 })
        {
            sql.AppendLine("  AND o.id_organization = ANY(@organizationIds)");
        }

        sql.AppendLine("ORDER BY COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name), s.name_survey;");

        return connection.Query<OrganizationSurveyAssignmentRow>(
            sql.ToString(),
            new
            {
                organizationIds = organizationIds?.ToArray() ?? Array.Empty<int>()
            }).ToList();
    }

    private static OrganizationSurveyItemViewModel MapOrganizationSurveyItem(OrganizationSurveyAssignmentRow row)
    {
        var effectiveEndDate = row.AssignmentDateEnd!.Value.Date;

        return new OrganizationSurveyItemViewModel
        {
            SurveyId = row.SurveyId!.Value,
            SurveyName = row.SurveyName ?? string.Empty,
            BaseEndDateIso = row.AssignmentDateEnd.Value.ToString("yyyy-MM-dd"),
            EffectiveEndDateDisplay = effectiveEndDate.ToString("dd.MM.yyyy"),
            EffectiveEndDateIso = effectiveEndDate.ToString("yyyy-MM-dd"),
            RemainingText = FormatRemainingText(effectiveEndDate),
            IsExpired = effectiveEndDate.Date < DateTime.Today
        };
    }

    private static List<Organization> SortOrganizations(
        IEnumerable<Organization> organizations,
        string? sortBy,
        string? sortDirection)
    {
        var normalizedSortBy = NormalizeOrganizationSortField(sortBy);
        var normalizedSortDirection = NormalizeOrganizationSortDirection(sortDirection, normalizedSortBy);
        var descending = string.Equals(normalizedSortDirection, "desc", StringComparison.Ordinal);

        IOrderedEnumerable<Organization> orderedOrganizations = normalizedSortBy switch
        {
            OrganizationListSortFields.DateBegin => descending
                ? organizations.OrderByDescending(organization => organization.DateBegin ?? DateTime.MinValue)
                : organizations.OrderBy(organization => organization.DateBegin ?? DateTime.MaxValue),
            OrganizationListSortFields.DateEnd => descending
                ? organizations.OrderByDescending(organization => organization.DateEnd ?? DateTime.MinValue)
                : organizations.OrderBy(organization => organization.DateEnd ?? DateTime.MaxValue),
            _ => descending
                ? organizations.OrderByDescending(organization => organization.OrganizationName, AppListPaging.RuStringComparer)
                : organizations.OrderBy(organization => organization.OrganizationName, AppListPaging.RuStringComparer)
        };

        return orderedOrganizations
            .ThenBy(organization => organization.OrganizationId)
            .ToList();
    }

    private static string NormalizeOrganizationSortField(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            OrganizationListSortFields.DateBegin => OrganizationListSortFields.DateBegin,
            OrganizationListSortFields.DateEnd => OrganizationListSortFields.DateEnd,
            _ => OrganizationListSortFields.Name
        };
    }

    private static string NormalizeOrganizationSortDirection(string? sortDirection, string sortField)
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
            OrganizationListSortFields.DateBegin => "desc",
            OrganizationListSortFields.DateEnd => "desc",
            _ => "asc"
        };
    }

    private static OrganizationSurveyAssignmentUpdateItem BuildOrganizationSurveyAssignmentUpdateItem(
        OrganizationSurveyAssignmentRow row,
        DateTime requestedEndDate)
    {
        var effectiveEndDate = requestedEndDate.Date;

        return new OrganizationSurveyAssignmentUpdateItem
        {
            OrganizationId = row.OrganizationId,
            SurveyId = row.SurveyId!.Value,
            EffectiveEndDateDisplay = effectiveEndDate.ToString("dd.MM.yyyy"),
            EffectiveEndDateIso = effectiveEndDate.ToString("yyyy-MM-dd"),
            RemainingText = FormatRemainingText(effectiveEndDate),
            IsExpired = effectiveEndDate.Date < DateTime.Today
        };
    }

    private static string FormatRemainingText(DateTime effectiveEndDate)
    {
        var daysRemaining = (effectiveEndDate.Date - DateTime.Today).Days;

        return daysRemaining switch
        {
            > 0 => $"Осталось {daysRemaining} дн.",
            0 => "Завершается сегодня",
            _ => $"Срок истёк {Math.Abs(daysRemaining)} дн. назад"
        };
    }

    private static IReadOnlyList<string> ValidateOrganizationSurveyEndDateUpdateRequest(
        OrganizationSurveyEndDateUpdateRequest request)
    {
        var errors = new List<string>();

        if (!DateTime.TryParse(request.DateEnd, out var requestedEndDate) || requestedEndDate.Date <= DateTime.Today)
        {
            errors.Add("Укажите корректную дату конца позже текущего дня.");
        }

        if (request.Assignments.Count == 0)
        {
            errors.Add("Выберите хотя бы одну анкету организации.");
        }

        foreach (var assignment in request.Assignments)
        {
            if (assignment.OrganizationId <= 0)
            {
                errors.Add("Некорректный идентификатор организации.");
            }

            if (assignment.SurveyId <= 0)
            {
                errors.Add("Некорректный идентификатор анкеты.");
            }
        }

        return errors;
    }

    private static string BuildArchiveRestrictedMessage(
        IReadOnlyList<string> surveyNames,
        IReadOnlyList<string> userNames)
    {
        var builder = new StringBuilder();

        builder.Append((surveyNames.Count, userNames.Count) switch
        {
            (> 0, > 0) => "Нельзя удалить организацию: для неё уже заводились анкеты и выбирались пользователи.",
            (> 0, 0) => "Нельзя удалить организацию: для неё уже заводились анкеты.",
            (0, > 0) => "Нельзя удалить организацию: для неё уже выбирались пользователи.",
            _ =>
                "Нельзя удалить организацию."
        });

        if (surveyNames.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Анкеты: ");
            builder.Append(string.Join(", ", surveyNames));
            builder.Append('.');
        }

        if (userNames.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Пользователи: ");
            builder.Append(string.Join(", ", userNames));
            builder.Append('.');
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> GetOrganizationAssignedSurveyNames(
        global::Npgsql.NpgsqlConnection connection,
        int organizationId)
    {
        return connection.Query<string>(
            """
            SELECT DISTINCT survey_name
            FROM (
                SELECT
                    COALESCE(
                        NULLIF(TRIM(s.name_survey), ''),
                        'Анкета #' || os.id_survey::text
                    ) AS survey_name
                FROM public.organization_survey os
                LEFT JOIN public.survey s
                    ON s.id_survey = os.id_survey
                WHERE os.id_organization = @organizationId

                UNION

                SELECT
                    COALESCE(
                        NULLIF(TRIM(s.name_survey), ''),
                        'Анкета #' || os.id_survey::text
                    ) AS survey_name
                FROM public.answer a
                INNER JOIN public.organization_survey os
                    ON os.id_organization_survey = a.id_organization_survey
                LEFT JOIN public.survey s
                    ON s.id_survey = os.id_survey
                WHERE os.id_organization = @organizationId

                UNION

                SELECT
                    COALESCE(
                        NULLIF(TRIM(s.name_survey), ''),
                        CASE
                            WHEN audit_row.survey_id IS NOT NULL
                                THEN 'Анкета #' || audit_row.survey_id::text
                            ELSE NULL
                        END
                    ) AS survey_name
                FROM (
                    SELECT DISTINCT
                        COALESCE(audit_raw.id_organization, os.id_organization) AS id_organization,
                        COALESCE(audit_raw.survey_id, os.id_survey) AS survey_id
                    FROM (
                        SELECT
                            CASE
                                WHEN COALESCE(record_pk->>'id_organization', row_data->>'id_organization', '') ~ '^[0-9]+$'
                                    THEN COALESCE(record_pk->>'id_organization', row_data->>'id_organization')::integer
                                ELSE NULL
                            END AS id_organization,
                            CASE
                                WHEN COALESCE(record_pk->>'id_survey', row_data->>'id_survey', '') ~ '^[0-9]+$'
                                    THEN COALESCE(record_pk->>'id_survey', row_data->>'id_survey')::integer
                                ELSE NULL
                            END AS survey_id,
                            CASE
                                WHEN COALESCE(record_pk->>'id_organization_survey', row_data->>'id_organization_survey', '') ~ '^[0-9]+$'
                                    THEN COALESCE(record_pk->>'id_organization_survey', row_data->>'id_organization_survey')::integer
                                ELSE NULL
                            END AS id_organization_survey
                        FROM public.organization_survey_l
                    ) audit_raw
                    LEFT JOIN public.organization_survey os
                        ON os.id_organization_survey = audit_raw.id_organization_survey
                ) audit_row
                LEFT JOIN public.survey s
                    ON s.id_survey = audit_row.survey_id
                WHERE audit_row.id_organization = @organizationId
            ) assigned_surveys
            WHERE survey_name IS NOT NULL
              AND BTRIM(survey_name) <> ''
            ORDER BY survey_name;
            """,
            new { organizationId })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetOrganizationAssignedUserNames(
        global::Npgsql.NpgsqlConnection connection,
        int organizationId)
    {
        return connection.Query<string>(
            """
            SELECT DISTINCT user_name
            FROM (
                SELECT
                    COALESCE(
                        NULLIF(TRIM(u.full_name), ''),
                        NULLIF(TRIM(u.login), ''),
                        'Пользователь #' || u.id_user::text
                    ) AS user_name
                FROM public.app_user u
                WHERE u.id_organization = @organizationId

                UNION

                SELECT
                    COALESCE(
                        NULLIF(TRIM(u.full_name), ''),
                        NULLIF(TRIM(u.login), ''),
                        NULLIF(TRIM(audit_row.full_name), ''),
                        NULLIF(TRIM(audit_row.user_name), ''),
                        CASE
                            WHEN audit_row.user_id IS NOT NULL
                                THEN 'Пользователь #' || audit_row.user_id::text
                            ELSE NULL
                        END
                    ) AS user_name
                FROM (
                    SELECT DISTINCT
                        CASE
                            WHEN COALESCE(record_pk->>'id_user', row_data->>'id_user', '') ~ '^[0-9]+$'
                                THEN COALESCE(record_pk->>'id_user', row_data->>'id_user')::integer
                            ELSE NULL
                        END AS user_id,
                        CASE
                            WHEN COALESCE(row_data->>'id_organization', record_pk->>'id_organization', '') ~ '^[0-9]+$'
                                THEN COALESCE(row_data->>'id_organization', record_pk->>'id_organization')::integer
                            ELSE NULL
                        END AS id_organization,
                        row_data->>'full_name' AS full_name,
                        COALESCE(row_data->>'login', row_data->>'name_user') AS user_name
                    FROM public.app_user_l
                ) audit_row
                LEFT JOIN public.app_user u
                    ON u.id_user = audit_row.user_id
                WHERE audit_row.id_organization = @organizationId
            ) assigned_users
            WHERE user_name IS NOT NULL
              AND BTRIM(user_name) <> ''
            ORDER BY user_name;
            """,
            new { organizationId })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryValidateOrganizationRequest(
        OrganizationSaveRequest request,
        out DateTime? dateBegin,
        out DateTime? dateEnd,
        out string validationError)
    {
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Название организации обязательно для заполнения.";
            return false;
        }

        if (!TryParseOptionalDate(request.DateBegin, out dateBegin, out validationError))
        {
            dateEnd = null;
            return false;
        }

        if (!TryParseOptionalDate(request.DateEnd, out dateEnd, out validationError))
        {
            return false;
        }

        if (dateBegin.HasValue && dateEnd.HasValue && dateEnd.Value < dateBegin.Value)
        {
            validationError = "Дата конца не может быть раньше даты начала.";
            return false;
        }

        return true;
    }

    private static bool TryParseOptionalDate(string? rawValue, out DateTime? parsedValue, out string validationError)
    {
        parsedValue = null;
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        if (DateTime.TryParse(rawValue, out var date))
        {
            parsedValue = date;
            return true;
        }

        validationError = "Некорректный формат даты.";
        return false;
    }

    private sealed class OrganizationSurveyAssignmentRow
    {
        public int OrganizationId { get; init; }
        public string OrganizationName { get; init; } = string.Empty;
        public int? SurveyId { get; init; }
        public string? SurveyName { get; init; }
        public DateTime? AssignmentDateEnd { get; init; }
    }
}
