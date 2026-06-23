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
    private readonly ISurveyAssignmentRepository _assignmentRepository;

    public SurveyArchiveService(
        IDbConnectionFactory connectionFactory,
        ISurveyAssignmentRepository assignmentRepository)
    {
        _connectionFactory = connectionFactory;
        _assignmentRepository = assignmentRepository;
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

        var selectedOrganizationIds = ParseSelectedIds(organizationIds);
        var selectedSurveyIds = ParseSelectedIds(surveyIds);
        var bounds = ResolveDateBounds(year, month, dateFrom, dateTo);
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeSurveyArchiveSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeSurveyArchiveSortDirection(null, normalizedSortBy);

        var organizationOptions = BuildSelectionOptions(
            _assignmentRepository.GetArchivedOrganizationOptions(connection));
        var surveyOptions = BuildSelectionOptions(
            _assignmentRepository.GetArchivedSurveyOptions(connection));
        var totalCount = _assignmentRepository.CountArchivedSurveys(
            connection,
            selectedOrganizationIds,
            selectedSurveyIds,
            bounds.Start,
            bounds.End);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var pageRows = _assignmentRepository.GetArchivedSurveyPage(
            connection,
            selectedOrganizationIds,
            selectedSurveyIds,
            bounds.Start,
            bounds.End,
            normalizedSortBy,
            normalizedSortDirection,
            pageWindow.PageSize,
            pageWindow.Offset);

        return new SurveyArchivePageViewModel
        {
            SurveyRows = pageRows.Select(MapSurveyArchiveTablePageRow).ToList(),
            CurrentPage = pageWindow.CurrentPage,
            TotalPages = pageWindow.TotalPages,
            TotalCount = pageWindow.TotalCount,
            PageSize = pageWindow.PageSize,
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

        var userOrganizationId = _assignmentRepository.GetUserOrganizationId(connection, userId);

        if (!userOrganizationId.HasValue)
        {
            return null;
        }

        const int pageSize = 10;
        var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
        var normalizedDate = date?.Trim() ?? string.Empty;
        var normalizedDateFrom = dateFrom?.Trim() ?? string.Empty;
        var normalizedDateTo = dateTo?.Trim() ?? string.Empty;

        DateTime? exactCompletionDate = null;
        DateTime? completionDateFrom = null;
        DateTime? completionDateTo = null;
        if (DateOnly.TryParse(normalizedDate, out var exactDate))
        {
            exactCompletionDate = exactDate.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            if (DateTime.TryParse(normalizedDateFrom, out var parsedDateFrom))
            {
                completionDateFrom = parsedDateFrom;
            }

            if (DateTime.TryParse(normalizedDateTo, out var parsedDateTo))
            {
                completionDateTo = parsedDateTo;
            }
        }

        var pageData = _assignmentRepository.GetUserArchivePage(
            connection,
            userOrganizationId.Value,
            normalizedSearchTerm,
            exactCompletionDate,
            completionDateFrom,
            completionDateTo,
            signedOnly,
            pageSize,
            Math.Max(currentPage - 1, 0) * pageSize);
        var totalPages = pageData.TotalCount == 0
            ? 1
            : (int)Math.Ceiling((double)pageData.TotalCount / pageSize);

        return new UserSurveyArchivePageViewModel
        {
            ArchivedSurveys = pageData.Surveys,
            UserOrganizationId = userOrganizationId.Value,
            CurrentPage = Math.Max(currentPage, 1),
            TotalPages = totalPages,
            TotalCount = pageData.TotalCount,
            SearchTerm = normalizedSearchTerm,
            DateFrom = normalizedDateFrom,
            DateTo = normalizedDateTo,
            SignedOnly = signedOnly
        };
    }

    public IReadOnlyList<ArchivedSurvey> GetAdminArchivedSurveys()
    {
        using var connection = _connectionFactory.CreateConnection();
        return _assignmentRepository.GetAdminArchivedSurveySummaries(connection);
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

    private static SurveyTableRowViewModel MapSurveyArchiveTablePageRow(SurveyAssignmentTableRow row)
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

        var archivedSurvey = await _assignmentRepository.GetArchivedSurveyForCopyAsync(
            connection,
            transaction,
            request.SurveyId);

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
