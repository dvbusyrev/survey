using System.Text;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Admin;

public class OrganizationManagementService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IClock _clock;

    protected OrganizationManagementService()
    {
        _connectionFactory = null!;
        _clock = null!;
    }

    protected OrganizationManagementService(IClock clock)
    {
        _connectionFactory = null!;
        _clock = clock;
    }

    public OrganizationManagementService(IDbConnectionFactory connectionFactory, IClock clock)
    {
        _connectionFactory = connectionFactory;
        _clock = clock;
    }

    public virtual Task<OrganizationListPageViewModel> GetActiveOrganizationsPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool openAddOrganizationModal = false,
        CancellationToken cancellationToken = default)
    {
        return GetOrganizationsPageAsync(currentPage, sortBy, sortDirection, includeArchived: false, openAddOrganizationModal, cancellationToken);
    }

    public virtual async Task<OrganizationSurveyAssignmentsPageViewModel> GetOrganizationSurveyAssignmentsPageAsync(CancellationToken cancellationToken = default)
    {
        var rows = await LoadLatestUnansweredAssignmentsAsync(cancellationToken: cancellationToken);

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

    public virtual Task<OrganizationListPageViewModel> GetArchivedOrganizationsPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default)
    {
        return GetOrganizationsPageAsync(currentPage, sortBy, sortDirection, includeArchived: true, cancellationToken: cancellationToken);
    }

    private async Task<OrganizationListPageViewModel> GetOrganizationsPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool includeArchived,
        bool openAddOrganizationModal = false,
        CancellationToken cancellationToken = default)
    {
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeOrganizationSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeOrganizationSortDirection(null, normalizedSortBy);

        var totalCount = await CountAsync(includeArchived, cancellationToken);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var organizations = await GetPageAsync(
            includeArchived,
            normalizedSortBy,
            normalizedSortDirection,
            pageWindow.PageSize,
            pageWindow.Offset,
            cancellationToken);

        return new OrganizationListPageViewModel
        {
            Organizations = organizations,
            OpenAddOrganizationModal = openAddOrganizationModal,
            CurrentPage = pageWindow.CurrentPage,
            TotalPages = pageWindow.TotalPages,
            TotalCount = pageWindow.TotalCount,
            PageSize = pageWindow.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            ViewModeIsArchive = includeArchived
        };
    }

    public virtual Task<IReadOnlyList<Organization>> GetArchivedOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        return GetAllAsync(includeArchived: true, cancellationToken);
    }

    public virtual Task<IReadOnlyList<OrganizationDataResponse>> GetOrganizationOptionsAsync(CancellationToken cancellationToken = default)
    {
        return GetActiveOptionsAsync(cancellationToken);
    }

    public virtual Task<Organization?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, cancellationToken);
    }

    public virtual async Task<OperationResult> CreateOrganizationAsync(OrganizationSaveRequest request, CancellationToken cancellationToken = default)
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

        var organizationId = await CreateAsync(ToWriteModel(request, dateBegin, dateEnd), cancellationToken);

        return new OperationResult
        {
            Success = true,
            Message = "Организация успешно создана.",
            EntityId = organizationId,
            ShouldReload = true
        };
    }

    public virtual async Task<OperationResult> UpdateOrganizationAsync(int id, OrganizationSaveRequest request, CancellationToken cancellationToken = default)
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

        var affectedRows = await UpdateAsync(id, ToWriteModel(request, dateBegin, dateEnd), cancellationToken);

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Организация успешно обновлена."
                : "Организация не найдена."
        };
    }

    public virtual async Task<OperationResult> ArchiveOrganizationAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await ArchiveIfUnusedAsync(id, cancellationToken);
        if (!result.Found)
        {
            return new OperationResult
            {
                Success = false,
                Message = "Организация не найдена."
            };
        }

        if (result.SurveyNames.Count > 0 || result.UserNames.Count > 0)
        {
            var message = BuildArchiveRestrictedMessage(result.SurveyNames, result.UserNames);

            return new OperationResult
            {
                Success = false,
                Message = message,
                Error = message,
                Code = "organization_in_use"
            };
        }

        return new OperationResult
        {
            Success = result.Archived,
            Message = result.Archived
                ? "Организация успешно удалена."
                : "Организация не найдена."
        };
    }

    public virtual async Task<OrganizationSurveyEndDateUpdateResult> UpdateOrganizationSurveyEndDatesAsync(
        OrganizationSurveyEndDateUpdateRequest request,
        CancellationToken cancellationToken = default)
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

        var organizationIds = assignments.Select(item => item.OrganizationId).Distinct().ToArray();
        var assignmentRows = await LoadLatestUnansweredAssignmentsAsync(organizationIds, cancellationToken);
        var assignmentLookup = assignmentRows
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

        try
        {
            if (!await UpdateAssignmentEndDatesAsync(
                    assignments.Select(item => (item.OrganizationId, item.SurveyId)).ToArray(),
                    requestedEndDate,
                    cancellationToken))
            {
                return new OrganizationSurveyEndDateUpdateResult
                {
                    Success = false,
                    Message = "Не удалось обновить дату конца анкет.",
                    Errors = ["Не удалось обновить одну или несколько связок организации и анкеты."]
                };
            }
        }
        catch (Exception)
        {
            return new OrganizationSurveyEndDateUpdateResult
            {
                Success = false,
                Message = "Не удалось обновить дату конца анкет.",
                Error = "Не удалось обновить дату конца анкет."
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

    private async Task<int> CountAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM public.organization o WHERE {GetArchivePredicate(includeArchived)};",
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyList<Organization>> GetPageAsync(
        bool includeArchived,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var organizations = await connection.QueryAsync<Organization>(new CommandDefinition(
            $"""
            {OrganizationSelectSql}
            WHERE {GetArchivePredicate(includeArchived)}
            ORDER BY {BuildOrderBy(sortBy, sortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { PageSize = pageSize, Offset = offset, Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return organizations.AsList();
    }

    private async Task<IReadOnlyList<Organization>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var organizations = await connection.QueryAsync<Organization>(new CommandDefinition(
            $"""
            {OrganizationSelectSql}
            WHERE {GetArchivePredicate(includeArchived)}
            ORDER BY o.organization_name;
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return organizations.AsList();
    }

    private async Task<IReadOnlyList<OrganizationDataResponse>> GetActiveOptionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var options = await connection.QueryAsync<OrganizationDataResponse>(new CommandDefinition(
            """
            SELECT id_organization AS Id, COALESCE(NULLIF(organization_short_name, ''), organization_name) AS Name
            FROM public.organization
            WHERE date_end IS NULL OR date_end >= @Today
            ORDER BY COALESCE(NULLIF(organization_short_name, ''), organization_name);
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return options.AsList();
    }

    private async Task<Organization?> GetByIdAsync(int organizationId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<Organization>(new CommandDefinition(
            """
            SELECT organization_name, organization_short_name, email, date_begin, date_end, id_organization AS OrganizationId
            FROM public.organization
            WHERE id_organization = @OrganizationId;
            """,
            new { OrganizationId = organizationId },
            cancellationToken: cancellationToken));
    }

    private async Task<int> CreateAsync(OrganizationWriteModel organization, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO public.organization (organization_name, organization_short_name, email, date_begin, date_end)
            VALUES (@Name, @ShortName, @Email, @DateBegin, @DateEnd)
            RETURNING id_organization;
            """,
            organization,
            cancellationToken: cancellationToken));
    }

    private async Task<int> UpdateAsync(int organizationId, OrganizationWriteModel organization, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.organization
            SET organization_name = @Name, organization_short_name = @ShortName, email = @Email,
                date_begin = @DateBegin, date_end = @DateEnd
            WHERE id_organization = @OrganizationId;
            """,
            new { OrganizationId = organizationId, organization.Name, organization.ShortName, organization.Email, organization.DateBegin, organization.DateEnd },
            cancellationToken: cancellationToken));
    }

    private async Task<OrganizationArchiveResult> ArchiveIfUnusedAsync(int organizationId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var exists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT id_organization FROM public.organization WHERE id_organization = @OrganizationId FOR UPDATE;",
            new { OrganizationId = organizationId },
            transaction,
            cancellationToken: cancellationToken));
        if (!exists.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            return new OrganizationArchiveResult(false, false, [], []);
        }

        var surveyNames = await GetAssignedSurveyNamesAsync(connection, organizationId, transaction, cancellationToken);
        var userNames = await GetAssignedUserNamesAsync(connection, organizationId, transaction, cancellationToken);
        if (surveyNames.Count > 0 || userNames.Count > 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new OrganizationArchiveResult(true, false, surveyNames, userNames);
        }

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.organization
            SET date_end = CASE WHEN date_end IS NULL OR date_end >= @Today
                THEN (@Today - INTERVAL '1 day')::date ELSE date_end END
            WHERE id_organization = @OrganizationId;
            """,
            new { OrganizationId = organizationId, Today = _clock.Today.Date },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return new OrganizationArchiveResult(true, affectedRows > 0, [], []);
    }

    protected virtual async Task<IReadOnlyList<OrganizationSurveyAssignmentRecord>> LoadLatestUnansweredAssignmentsAsync(
        IReadOnlyCollection<int>? organizationIds = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var sql = new StringBuilder(
            """
            WITH latest_assignment AS (
                SELECT * FROM (
                    SELECT os.id_organization, os.id_survey, os.id_organization_survey, os.date_end, s.name_survey,
                        ROW_NUMBER() OVER (
                            PARTITION BY os.id_organization, LOWER(BTRIM(s.name_survey))
                            ORDER BY os.date_begin DESC, os.id_survey DESC, os.id_organization_survey DESC) AS assignment_rank
                    FROM public.organization_survey os
                    INNER JOIN public.survey s ON s.id_survey = os.id_survey
                ) ranked
                WHERE assignment_rank = 1
                  AND NOT EXISTS (SELECT 1 FROM public.answer a WHERE a.id_organization_survey = ranked.id_organization_survey)
            )
            SELECT o.id_organization AS OrganizationId,
                   COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS OrganizationName,
                   latest_assignment.id_survey AS SurveyId, latest_assignment.name_survey AS SurveyName,
                   latest_assignment.date_end AS AssignmentDateEnd
            FROM public.organization o
            LEFT JOIN latest_assignment ON latest_assignment.id_organization = o.id_organization
            WHERE o.date_end IS NULL OR o.date_end >= @Today
            """);
        if (organizationIds is { Count: > 0 })
        {
            sql.Append(" AND o.id_organization = ANY(@OrganizationIds)");
        }

        sql.Append(" ORDER BY COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name), latest_assignment.name_survey NULLS LAST;");
        var assignments = await connection.QueryAsync<OrganizationSurveyAssignmentRecord>(new CommandDefinition(
            sql.ToString(),
            new { OrganizationIds = organizationIds?.ToArray() ?? [], Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return assignments.AsList();
    }

    private async Task<bool> UpdateAssignmentEndDatesAsync(
        IReadOnlyCollection<(int OrganizationId, int SurveyId)> assignments,
        DateTime dateEnd,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var assignment in assignments)
            {
                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE public.organization_survey SET date_end = @DateEnd WHERE id_organization = @OrganizationId AND id_survey = @SurveyId;",
                    new { DateEnd = dateEnd.Date, assignment.OrganizationId, assignment.SurveyId },
                    transaction,
                    cancellationToken: cancellationToken));
                if (affected == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<IReadOnlyList<string>> GetAssignedSurveyNamesAsync(
        System.Data.IDbConnection connection,
        int organizationId,
        System.Data.IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var surveyNames = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT DISTINCT survey_name
            FROM (
                SELECT COALESCE(NULLIF(TRIM(s.name_survey), ''), 'Анкета #' || os.id_survey::text) AS survey_name
                FROM public.organization_survey os
                LEFT JOIN public.survey s ON s.id_survey = os.id_survey
                WHERE os.id_organization = @OrganizationId

                UNION

                SELECT COALESCE(NULLIF(TRIM(s.name_survey), ''), 'Анкета #' || os.id_survey::text) AS survey_name
                FROM public.answer a
                INNER JOIN public.organization_survey os ON os.id_organization_survey = a.id_organization_survey
                LEFT JOIN public.survey s ON s.id_survey = os.id_survey
                WHERE os.id_organization = @OrganizationId

                UNION

                SELECT COALESCE(
                    NULLIF(TRIM(s.name_survey), ''),
                    CASE WHEN audit_row.survey_id IS NOT NULL THEN 'Анкета #' || audit_row.survey_id::text END
                ) AS survey_name
                FROM (
                    SELECT DISTINCT
                        COALESCE(audit_raw.id_organization, os.id_organization) AS id_organization,
                        COALESCE(audit_raw.survey_id, os.id_survey) AS survey_id
                    FROM (
                        SELECT id_organization, id_survey AS survey_id, id_organization_survey
                        FROM public.organization_survey_l
                    ) audit_raw
                    LEFT JOIN public.organization_survey os
                        ON os.id_organization_survey = audit_raw.id_organization_survey
                ) audit_row
                LEFT JOIN public.survey s ON s.id_survey = audit_row.survey_id
                WHERE audit_row.id_organization = @OrganizationId
            ) assigned_surveys
            WHERE survey_name IS NOT NULL AND BTRIM(survey_name) <> ''
            ORDER BY survey_name;
            """,
            new { OrganizationId = organizationId },
            transaction,
            cancellationToken: cancellationToken));
        return surveyNames
            .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<IReadOnlyList<string>> GetAssignedUserNamesAsync(
        System.Data.IDbConnection connection,
        int organizationId,
        System.Data.IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var userNames = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT DISTINCT user_name
            FROM (
                SELECT COALESCE(NULLIF(TRIM(u.full_name), ''), NULLIF(TRIM(u.login), ''), 'Пользователь #' || u.id_user::text) AS user_name
                FROM public.app_user u
                WHERE u.id_organization = @OrganizationId

                UNION

                SELECT COALESCE(
                    NULLIF(TRIM(u.full_name), ''),
                    NULLIF(TRIM(u.login), ''),
                    NULLIF(TRIM(audit_row.full_name), ''),
                    NULLIF(TRIM(audit_row.user_name), ''),
                    CASE WHEN audit_row.user_id IS NOT NULL THEN 'Пользователь #' || audit_row.user_id::text END
                ) AS user_name
                FROM (
                    SELECT DISTINCT id_user AS user_id, id_organization, full_name, login AS user_name
                    FROM public.app_user_l
                ) audit_row
                LEFT JOIN public.app_user u ON u.id_user = audit_row.user_id
                WHERE audit_row.id_organization = @OrganizationId
            ) assigned_users
            WHERE user_name IS NOT NULL AND BTRIM(user_name) <> ''
            ORDER BY user_name;
            """,
            new { OrganizationId = organizationId },
            transaction,
            cancellationToken: cancellationToken));
        return userNames
            .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string GetArchivePredicate(bool includeArchived) => includeArchived
        ? "o.date_end < @Today"
        : "(o.date_end IS NULL OR o.date_end >= @Today)";

    private static string BuildOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.Ordinal) ? "DESC" : "ASC";
        return sortBy switch
        {
            "date_begin" => $"o.date_begin {direction} NULLS LAST, o.id_organization ASC",
            "date_end" => $"o.date_end {direction} NULLS LAST, o.id_organization ASC",
            _ => $"o.organization_name {direction}, o.id_organization ASC"
        };
    }

    private const string OrganizationSelectSql = """
        SELECT o.id_organization AS OrganizationId, o.organization_name, o.organization_short_name, o.date_begin, o.date_end,
               COALESCE((SELECT string_agg(s.name_survey, ', ' ORDER BY s.name_survey)
                         FROM public.organization_survey os
                         INNER JOIN public.survey s ON s.id_survey = os.id_survey
                         WHERE os.id_organization = o.id_organization), 'Не указано') AS survey_names,
               o.email
        FROM public.organization o
        """;

    private OrganizationSurveyItemViewModel MapOrganizationSurveyItem(OrganizationSurveyAssignmentRecord row)
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
            IsExpired = effectiveEndDate.Date < _clock.Today.Date
        };
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

    private OrganizationSurveyAssignmentUpdateItem BuildOrganizationSurveyAssignmentUpdateItem(
        OrganizationSurveyAssignmentRecord row,
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
            IsExpired = effectiveEndDate.Date < _clock.Today.Date
        };
    }

    private string FormatRemainingText(DateTime effectiveEndDate)
    {
        var daysRemaining = (effectiveEndDate.Date - _clock.Today.Date).Days;

        return daysRemaining switch
        {
            > 0 => $"Осталось {daysRemaining} дн.",
            0 => "Завершается сегодня",
            _ => $"Срок истёк {Math.Abs(daysRemaining)} дн. назад"
        };
    }

    private IReadOnlyList<string> ValidateOrganizationSurveyEndDateUpdateRequest(
        OrganizationSurveyEndDateUpdateRequest request)
    {
        var errors = new List<string>();

        if (!DateTime.TryParse(request.DateEnd, out var requestedEndDate) || requestedEndDate.Date <= _clock.Today.Date)
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
            ( > 0, > 0) => "Нельзя удалить организацию: для неё уже заводились анкеты и выбирались пользователи.",
            ( > 0, 0) => "Нельзя удалить организацию: для неё уже заводились анкеты.",
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

    private bool TryValidateOrganizationRequest(
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
            validationError = "Введите название организации.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.DateBegin))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Укажите дату начала.";
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

        if (dateEnd.HasValue && dateEnd.Value.Date < _clock.Today.Date)
        {
            validationError = "Дата конца не может быть раньше сегодняшней даты.";
            return false;
        }

        if (dateBegin.HasValue && dateEnd.HasValue && dateEnd.Value < dateBegin.Value)
        {
            validationError = "Дата конца не может быть раньше даты начала.";
            return false;
        }

        return true;
    }

    private static OrganizationWriteModel ToWriteModel(
        OrganizationSaveRequest request,
        DateTime? dateBegin,
        DateTime? dateEnd)
    {
        return new OrganizationWriteModel(
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            dateBegin,
            dateEnd);
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

}

public sealed record OrganizationWriteModel(
    string Name,
    string? ShortName,
    string? Email,
    DateTime? DateBegin,
    DateTime? DateEnd);

public sealed class OrganizationSurveyAssignmentRecord
{
    public int OrganizationId { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public int? SurveyId { get; init; }
    public string? SurveyName { get; init; }
    public DateTime? AssignmentDateEnd { get; init; }
}

public sealed record OrganizationArchiveResult(
    bool Found,
    bool Archived,
    IReadOnlyList<string> SurveyNames,
    IReadOnlyList<string> UserNames);
