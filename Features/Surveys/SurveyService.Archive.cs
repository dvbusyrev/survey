using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Surveys;

public partial class SurveyService
{
    public async Task<SurveyArchivePageViewModel> GetAdminArchivedSurveysPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        string? surveyIds,
        string? year,
        string? month,
        string? dateFrom,
        string? dateTo,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var selectedOrganizationIds = ParseArchiveSelectedIds(organizationIds);
        var selectedSurveyIds = ParseArchiveSelectedIds(surveyIds);
        var bounds = ResolveDateBounds(year, month, dateFrom, dateTo);
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeSurveyArchiveSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeSurveyArchiveSortDirection(null, normalizedSortBy);

        var organizationOptions = BuildArchiveSelectionOptions(
            await _surveyRepository.GetArchivedOrganizationOptionsAsync(connection, cancellationToken));
        var surveyOptions = BuildArchiveSelectionOptions(
            await _surveyRepository.GetArchivedSurveyOptionsAsync(connection, cancellationToken));
        var totalCount = await _surveyRepository.CountArchivedSurveysAsync(
            connection,
            selectedOrganizationIds,
            selectedSurveyIds,
            bounds.Start,
            bounds.End,
            cancellationToken);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var pageRows = await _surveyRepository.GetArchivedSurveyPageAsync(
            connection,
            selectedOrganizationIds,
            selectedSurveyIds,
            bounds.Start,
            bounds.End,
            normalizedSortBy,
            normalizedSortDirection,
            pageWindow.PageSize,
            pageWindow.Offset,
            cancellationToken);

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

    public async Task<UserSurveyArchivePageViewModel?> GetUserArchivePageAsync(
        int userId,
        int currentPage,
        string? searchTerm,
        string? date,
        string? dateFrom,
        string? dateTo,
        bool signedOnly,
        string? surveyIds = null,
        string? year = null,
        string? month = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var userOrganizationId = await _surveyRepository.GetUserOrganizationIdAsync(connection, userId, cancellationToken);

        if (!userOrganizationId.HasValue)
        {
            return null;
        }

        const int pageSize = 10;
        var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
        var normalizedDate = date?.Trim() ?? string.Empty;
        var normalizedDateFrom = dateFrom?.Trim() ?? string.Empty;
        var normalizedDateTo = dateTo?.Trim() ?? string.Empty;
        var selectedSurveyIds = ParseArchiveSelectedIds(surveyIds);
        var bounds = ResolveDateBounds(year, month, normalizedDateFrom, normalizedDateTo);

        DateTime? exactCompletionDate = null;
        DateTime? completionDateFrom = null;
        DateTime? completionDateTo = null;
        if (DateOnly.TryParse(normalizedDate, out var exactDate))
        {
            exactCompletionDate = exactDate.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            if (bounds.FilterType != ArchiveDateFilterType.None)
            {
                completionDateFrom = bounds.Start;
                completionDateTo = bounds.End;
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
        }

        var surveyOptions = BuildArchiveSelectionOptions(
            await _surveyRepository.GetUserArchivedSurveyOptionsAsync(
                connection,
                userOrganizationId.Value,
                cancellationToken));

        var pageData = await _surveyRepository.GetUserArchivePageAsync(
            connection,
            userOrganizationId.Value,
            normalizedSearchTerm,
            selectedSurveyIds,
            exactCompletionDate,
            completionDateFrom,
            completionDateTo,
            signedOnly,
            pageSize,
            Math.Max(currentPage - 1, 0) * pageSize,
            cancellationToken);
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
            SignedOnly = signedOnly,
            FilterState = new ServerTableFilterStateViewModel
            {
                BasePath = "/archive",
                EnableDateFilter = true,
                EnableSurveyFilter = true,
                SurveyOptions = surveyOptions,
                SelectedSurveyIds = selectedSurveyIds,
                Year = bounds.FilterType == ArchiveDateFilterType.Year ? bounds.Year : null,
                Month = bounds.FilterType == ArchiveDateFilterType.Month ? bounds.Month : string.Empty,
                DateFrom = bounds.FilterType == ArchiveDateFilterType.Range ? bounds.Start?.ToString("yyyy-MM-dd") ?? string.Empty : string.Empty,
                DateTo = bounds.FilterType == ArchiveDateFilterType.Range ? bounds.End?.ToString("yyyy-MM-dd") ?? string.Empty : string.Empty
            }
        };
    }

    public async Task<IReadOnlyList<ArchivedSurvey>> GetAdminArchivedSurveysAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _surveyRepository.GetAdminArchivedSurveySummariesAsync(connection, cancellationToken);
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

    private static IReadOnlyList<int> ParseArchiveSelectedIds(string? rawValue)
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

    private static IReadOnlyList<SelectionOption> BuildArchiveSelectionOptions(IEnumerable<SelectionOption> options)
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

    private enum ArchiveDateFilterType
    {
        None,
        Year,
        Month,
        Range
    }

    public async Task<int> CopyArchiveSurveyAsync(ArchiveSurveyCopyRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var archivedSurvey = await _surveyRepository.GetArchivedSurveyForCopyAsync(
            connection,
            transaction,
            request.SurveyId,
            cancellationToken);

        if (archivedSurvey == null)
        {
            throw new InvalidOperationException("Архивная анкета не найдена.");
        }

        var questions = await _surveyRepository.GetSurveyQuestionsAsync(
            connection, transaction, request.SurveyId, cancellationToken);
        var newSurveyId = await _surveyRepository.CreateSurveyAsync(
            connection,
            transaction,
            archivedSurvey.NameSurvey,
            archivedSurvey.Description,
            cancellationToken);
        await _surveyRepository.ReplaceSurveyQuestionsAsync(connection, transaction, newSurveyId, questions, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return newSurveyId;
    }

}
