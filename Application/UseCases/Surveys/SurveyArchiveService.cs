using System.Data;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Surveys;

public sealed class SurveyArchiveService : ISurveyArchiveService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SurveyArchiveService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public SurveyArchivePageViewModel GetAdminArchivedSurveysPage(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        string? surveyIds,
        string? year,
        string? month,
        string? dateFrom,
        string? dateTo)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                s.id_survey AS IdSurvey,
                s.name_survey AS NameSurvey,
                ss.date_begin AS DateBegin,
                ss.date_end AS DateEnd,
                o.id_organization AS OrganizationId,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS OrganizationName
            FROM public.survey s
            LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
            LEFT JOIN public.organization_survey os
                ON os.id_survey = s.id_survey
            LEFT JOIN public.organization o
                ON o.id_organization = os.id_organization
            WHERE EXISTS (
                    SELECT 1
                    FROM public.organization_survey existing_os
                    WHERE existing_os.id_survey = s.id_survey
                )
              AND EXISTS (
                    SELECT 1
                    FROM public.answer a
                    INNER JOIN public.organization_survey answered_os
                        ON answered_os.id_organization_survey = a.id_organization_survey
                    WHERE answered_os.id_survey = s.id_survey
                )
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.organization_survey active_os
                    WHERE active_os.id_survey = s.id_survey
                      AND (active_os.date_end IS NULL OR active_os.date_end >= CURRENT_DATE)
                )
            ORDER BY s.id_survey DESC, COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name);";

        var assignmentRows = connection.Query<SurveyArchiveAssignmentRow>(sql).ToList();
        var groupedRows = BuildSurveyTableRows(assignmentRows);
        var organizationOptions = BuildSelectionOptions(
            assignmentRows
                .Where(row => row.OrganizationId > 0 && !string.IsNullOrWhiteSpace(row.OrganizationName))
                .Select(row => new SelectionOption
                {
                    Id = row.OrganizationId,
                    Name = row.OrganizationName!.Trim()
                }));
        var surveyOptions = groupedRows
            .Select(row => new SelectionOption
            {
                Id = row.IdSurvey,
                Name = row.NameSurvey
            })
            .OrderBy(option => option.Name, AppListPaging.RuStringComparer)
            .ThenBy(option => option.Id)
            .ToList();

        var selectedOrganizationIds = ParseSelectedIds(organizationIds);
        var selectedSurveyIds = ParseSelectedIds(surveyIds);
        var bounds = ResolveDateBounds(year, month, dateFrom, dateTo);

        var filteredRows = groupedRows
            .Where(row => selectedOrganizationIds.Count == 0 || row.OrganizationIds.Any(selectedOrganizationIds.Contains))
            .Where(row => selectedSurveyIds.Count == 0 || selectedSurveyIds.Contains(row.IdSurvey))
            .Where(row => MatchesDateBounds(row, bounds.Start, bounds.End))
            .ToList();

        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeSurveyArchiveSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeSurveyArchiveSortDirection(null, normalizedSortBy);
        var sortedRows = SortSurveyRows(filteredRows, normalizedSortBy, normalizedSortDirection);
        var pageSlice = AppListPaging.Slice(sortedRows, currentPage);

        return new SurveyArchivePageViewModel
        {
            SurveyRows = pageSlice.Items,
            CurrentPage = pageSlice.CurrentPage,
            TotalPages = pageSlice.TotalPages,
            TotalCount = pageSlice.TotalCount,
            PageSize = pageSlice.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            FilterState = new ServerTableFilterStateViewModel
            {
                BasePath = "/surveys/archive",
                EnableDateFilter = true,
                EnableOrganizationFilter = true,
                EnableSurveyFilter = true,
                OrganizationOptions = organizationOptions,
                SelectedOrganizationIds = selectedOrganizationIds,
                SurveyOptions = surveyOptions,
                SelectedSurveyIds = selectedSurveyIds,
                Year = bounds.FilterType == ArchiveDateFilterType.Year ? bounds.Year : null,
                Month = bounds.FilterType == ArchiveDateFilterType.Month ? bounds.Month : string.Empty,
                DateFrom = bounds.FilterType == ArchiveDateFilterType.Range ? bounds.Start?.ToString("yyyy-MM-dd") ?? string.Empty : string.Empty,
                DateTo = bounds.FilterType == ArchiveDateFilterType.Range ? bounds.End?.ToString("yyyy-MM-dd") ?? string.Empty : string.Empty
            }
        };
    }

    public UserSurveyArchivePageViewModel? GetUserArchivePage(
        int userId,
        int currentPage,
        string? searchTerm,
        string? date,
        string? dateFrom,
        string? dateTo,
        bool signedOnly)
    {
        using var connection = _connectionFactory.CreateConnection();

        var userOrganizationId = connection.ExecuteScalar<int?>(
            "SELECT id_organization FROM public.app_user WHERE id_user = @userId",
            new { userId });

        if (!userOrganizationId.HasValue)
        {
            return null;
        }

        const int pageSize = 10;
        var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
        var normalizedDate = date?.Trim() ?? string.Empty;
        var normalizedDateFrom = dateFrom?.Trim() ?? string.Empty;
        var normalizedDateTo = dateTo?.Trim() ?? string.Empty;

        var filters = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("userOrganizationId", userOrganizationId.Value);
        parameters.Add("searchPattern", string.IsNullOrWhiteSpace(normalizedSearchTerm) ? null : $"%{normalizedSearchTerm}%");
        parameters.Add("offset", Math.Max(currentPage - 1, 0) * pageSize);
        parameters.Add("pageSize", pageSize);

        if (!string.IsNullOrWhiteSpace(normalizedSearchTerm))
        {
            filters.Add("archived.name_survey ILIKE @searchPattern");
        }

        if (DateOnly.TryParse(normalizedDate, out var exactDate))
        {
            filters.Add("archived.completion_date::date = @exactDate");
            parameters.Add("exactDate", exactDate.ToDateTime(TimeOnly.MinValue));
        }
        else
        {
            if (DateTime.TryParse(normalizedDateFrom, out var parsedDateFrom))
            {
                filters.Add("archived.completion_date >= @dateFrom");
                parameters.Add("dateFrom", parsedDateFrom);
            }

            if (DateTime.TryParse(normalizedDateTo, out var parsedDateTo))
            {
                filters.Add("archived.completion_date <= @dateTo");
                parameters.Add("dateTo", parsedDateTo);
            }
        }

        if (signedOnly)
        {
            filters.Add("COALESCE(archived.csp, '') <> ''");
        }

        var whereClause = filters.Count == 0
            ? string.Empty
            : "WHERE " + string.Join(" AND ", filters);

        const string archivedSql = @"
            FROM (
                SELECT
                    s.id_survey,
                    s.name_survey,
                    s.description,
                    COALESCE(os.date_begin, ss.date_begin) AS date_begin,
                    COALESCE(os.date_end, ss.date_end) AS date_end,
                    a.completion_date,
                    a.csp,
                    os.id_organization AS OrganizationId
                FROM public.survey s
                INNER JOIN public.organization_survey os
                    ON os.id_survey = s.id_survey
                INNER JOIN public.answer a
                    ON a.id_organization_survey = os.id_organization_survey
                LEFT JOIN public.survey_schedule ss
                    ON ss.id_survey = s.id_survey
                WHERE os.id_organization = @userOrganizationId
            ) AS archived";

        var totalCount = connection.ExecuteScalar<int>(
            $"SELECT COUNT(*) {archivedSql} {whereClause}",
            parameters);

        var archivedSurveys = connection.Query<Survey>(
            $@"SELECT
                    archived.id_survey,
                    archived.name_survey,
                    archived.description,
                    archived.date_begin,
                    archived.date_end,
                    archived.completion_date,
                    archived.csp,
                    archived.OrganizationId
               {archivedSql}
               {whereClause}
               ORDER BY archived.completion_date DESC
               OFFSET @offset
               LIMIT @pageSize",
            parameters).ToList();

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling((double)totalCount / pageSize);

        return new UserSurveyArchivePageViewModel
        {
            ArchivedSurveys = archivedSurveys,
            UserOrganizationId = userOrganizationId.Value,
            CurrentPage = Math.Max(currentPage, 1),
            TotalPages = totalPages,
            TotalCount = totalCount,
            SearchTerm = normalizedSearchTerm,
            DateFrom = normalizedDateFrom,
            DateTo = normalizedDateTo,
            SignedOnly = signedOnly
        };
    }

    public IReadOnlyList<ArchivedSurvey> GetAdminArchivedSurveys()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                s.id_survey,
                ss.date_begin AS date_begin,
                ss.date_end AS date_end,
                s.name_survey,
                COALESCE(
                    (
                        SELECT string_agg(
                            COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name),
                            ', '
                            ORDER BY COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)
                        )
                        FROM public.organization_survey os
                        INNER JOIN public.organization o
                            ON o.id_organization = os.id_organization
                        WHERE os.id_survey = s.id_survey
                    ),
                    'Не указано'
                ) AS organization_name,
                s.description
            FROM public.survey s
            LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
            WHERE EXISTS (
                    SELECT 1
                    FROM public.organization_survey os
                    WHERE os.id_survey = s.id_survey
                )
              AND EXISTS (
                    SELECT 1
                    FROM public.answer a
                    INNER JOIN public.organization_survey aos
                        ON aos.id_organization_survey = a.id_organization_survey
                    WHERE aos.id_survey = s.id_survey
                )
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.organization_survey os
                    WHERE os.id_survey = s.id_survey
                      AND (os.date_end IS NULL OR os.date_end >= CURRENT_DATE)
                )
            ORDER BY id_survey DESC";

        return connection.Query<ArchivedSurvey>(sql).ToList();
    }

    private static List<SurveyTableRowViewModel> BuildSurveyTableRows(IEnumerable<SurveyArchiveAssignmentRow> rows)
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

    private static (ArchiveDateFilterType FilterType, int? Year, string Month, DateTime? Start, DateTime? End) ResolveDateBounds(
        string? year,
        string? month,
        string? dateFrom,
        string? dateTo)
    {
        if (int.TryParse(year, out var parsedYear) && parsedYear >= 1900 && parsedYear <= 3000)
        {
            return (
                ArchiveDateFilterType.Year,
                parsedYear,
                string.Empty,
                new DateTime(parsedYear, 1, 1),
                new DateTime(parsedYear, 12, 31));
        }

        if (!string.IsNullOrWhiteSpace(month)
            && DateTime.TryParseExact(
                $"{month}-01",
                "yyyy-MM-dd",
                null,
                System.Globalization.DateTimeStyles.None,
                out var parsedMonth))
        {
            return (
                ArchiveDateFilterType.Month,
                null,
                month.Trim(),
                new DateTime(parsedMonth.Year, parsedMonth.Month, 1),
                new DateTime(parsedMonth.Year, parsedMonth.Month, DateTime.DaysInMonth(parsedMonth.Year, parsedMonth.Month)));
        }

        if (DateTime.TryParse(dateFrom, out var parsedDateFrom)
            && DateTime.TryParse(dateTo, out var parsedDateTo))
        {
            return (
                ArchiveDateFilterType.Range,
                null,
                string.Empty,
                parsedDateFrom.Date,
                parsedDateTo.Date);
        }

        return (ArchiveDateFilterType.None, null, string.Empty, null, null);
    }

    private static bool MatchesDateBounds(
        SurveyTableRowViewModel row,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            return true;
        }

        return row.DateEnd.HasValue
            && row.DateBegin.Date >= startDate.Value.Date
            && row.DateBegin.Date <= endDate.Value.Date
            && row.DateEnd.Value.Date >= startDate.Value.Date
            && row.DateEnd.Value.Date <= endDate.Value.Date;
    }

    private static string NormalizeSurveyArchiveSortField(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            SurveyArchiveSortFields.Name => SurveyArchiveSortFields.Name,
            SurveyArchiveSortFields.DateBegin => SurveyArchiveSortFields.DateBegin,
            SurveyArchiveSortFields.DateEnd => SurveyArchiveSortFields.DateEnd,
            _ => SurveyArchiveSortFields.Default
        };
    }

    private static string NormalizeSurveyArchiveSortDirection(string? sortDirection, string sortField)
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
            SurveyArchiveSortFields.Name => "asc",
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
            SurveyArchiveSortFields.Name => descending
                ? rows.OrderByDescending(row => row.NameSurvey, AppListPaging.RuStringComparer)
                : rows.OrderBy(row => row.NameSurvey, AppListPaging.RuStringComparer),
            SurveyArchiveSortFields.DateBegin => descending
                ? rows.OrderByDescending(row => row.DateBegin)
                : rows.OrderBy(row => row.DateBegin),
            SurveyArchiveSortFields.DateEnd => descending
                ? rows.OrderByDescending(row => row.DateEnd ?? DateTime.MinValue)
                : rows.OrderBy(row => row.DateEnd ?? DateTime.MaxValue),
            _ => rows.OrderByDescending(row => row.IdSurvey)
        };

        return orderedRows
            .ThenByDescending(row => row.IdSurvey)
            .ToList();
    }

    private enum ArchiveDateFilterType
    {
        None,
        Year,
        Month,
        Range
    }

    public async Task<int> CopyArchiveSurveyAsync(ArchiveSurveyCopyRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        var archivedSurvey = await connection.QueryFirstOrDefaultAsync<ArchivedSurvey>(
            @"SELECT
                  s.id_survey,
                  ss.date_begin AS date_begin,
                  ss.date_end AS date_end,
                  s.name_survey,
                  s.description
              FROM public.survey s
              LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
              WHERE s.id_survey = @surveyId
                AND EXISTS (
                    SELECT 1
                    FROM public.organization_survey os
                    WHERE os.id_survey = s.id_survey
                )
                AND EXISTS (
                    SELECT 1
                    FROM public.answer a
                    INNER JOIN public.organization_survey aos
                        ON aos.id_organization_survey = a.id_organization_survey
                    WHERE aos.id_survey = s.id_survey
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM public.organization_survey os
                    WHERE os.id_survey = s.id_survey
                      AND (os.date_end IS NULL OR os.date_end >= CURRENT_DATE)
                )",
            new { surveyId = request.SurveyId },
            transaction);

        if (archivedSurvey == null)
        {
            throw new InvalidOperationException("Архивная анкета не найдена.");
        }

        archivedSurvey.Questions = connection.Query<SurveyQuestionItem>(
            @"SELECT
                  question_order AS Id,
                  question_text AS Text
              FROM public.survey_question
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId = request.SurveyId },
            transaction).ToList();

        var newSurveyId = await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO public.survey
                (name_survey, description)
              VALUES
                (@nameSurvey, @description)
              RETURNING id_survey;",
            new
            {
                nameSurvey = archivedSurvey.NameSurvey,
                description = archivedSurvey.Description ?? string.Empty
            },
            transaction);

        foreach (var question in archivedSurvey.Questions.OrderBy(q => q.Id))
        {
            await connection.ExecuteAsync(
                @"INSERT INTO public.survey_question (id_survey, question_order, question_text)
                  VALUES (@idSurvey, @questionOrder, @questionText);",
                new
                {
                    idSurvey = newSurveyId,
                    questionOrder = question.Id,
                    questionText = question.Text
                },
                transaction);
        }

        transaction.Commit();
        return newSurveyId;
    }

    private sealed class SurveyArchiveAssignmentRow
    {
        public int IdSurvey { get; init; }
        public string? NameSurvey { get; init; }
        public DateTime DateBegin { get; init; }
        public DateTime? DateEnd { get; init; }
        public int OrganizationId { get; init; }
        public string? OrganizationName { get; init; }
    }
}
