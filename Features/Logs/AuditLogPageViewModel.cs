using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public static class AuditLogSortFields
{
    public const string Date = "date";
    public const string User = "user";
    public const string Event = "event";
    public const string Description = "description";
}

public sealed class AuditLogPageViewModel
{
    private const string DefaultSortDirection = "desc";

    public bool HasExplicitSort { get; init; }
    public IReadOnlyList<Log> Logs { get; init; } = Array.Empty<Log>();
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalCount { get; init; }
    public int PageSize { get; init; } = 10;
    public string SortBy { get; init; } = AuditLogSortFields.Date;
    public string SortDirection { get; init; } = DefaultSortDirection;
    public AppPaginationViewModel Pagination => AppPaginationViewModel.Create(
        CurrentPage,
        TotalPages,
        "Навигация по страницам журнала событий",
        BuildPageUrl,
        "logs-table-top");

    public bool IsSortedBy(string field)
    {
        return HasExplicitSort
            && string.Equals(NormalizeSortField(SortBy), NormalizeSortField(field), StringComparison.Ordinal);
    }

    public string GetHeaderSortDirection(string field)
    {
        return IsSortedBy(field) ? NormalizeSortDirection(SortDirection) : string.Empty;
    }

    public string GetAriaSort(string field)
    {
        if (!IsSortedBy(field))
        {
            return "none";
        }

        return string.Equals(NormalizeSortDirection(SortDirection), "asc", StringComparison.Ordinal)
            ? "ascending"
            : "descending";
    }

    public string BuildPageUrl(int page)
    {
        return BuildUrl(page, NormalizeSortField(SortBy), NormalizeSortDirection(SortDirection), HasExplicitSort);
    }

    public string BuildSortUrl(string field)
    {
        var normalizedField = NormalizeSortField(field);
        if (!IsSortedBy(normalizedField))
        {
            return BuildUrl(1, normalizedField, "asc", includeSort: true);
        }

        if (string.Equals(NormalizeSortDirection(SortDirection), "asc", StringComparison.Ordinal))
        {
            return BuildUrl(1, normalizedField, "desc", includeSort: true);
        }

        return BuildUrl(1, normalizedField, string.Empty, includeSort: false);
    }

    private static string NormalizeSortField(string? field)
    {
        return field?.Trim().ToLowerInvariant() switch
        {
            AuditLogSortFields.User => AuditLogSortFields.User,
            AuditLogSortFields.Event => AuditLogSortFields.Event,
            AuditLogSortFields.Description => AuditLogSortFields.Description,
            _ => AuditLogSortFields.Date
        };
    }

    private static string NormalizeSortDirection(string? direction)
    {
        return string.Equals(direction?.Trim(), "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
    }

    private static string GetDefaultDirectionForField(string field)
    {
        return string.Equals(field, AuditLogSortFields.Date, StringComparison.Ordinal)
            ? "desc"
            : "asc";
    }

    private static bool IsDefaultSort(string field, string direction)
    {
        return string.Equals(field, AuditLogSortFields.Date, StringComparison.Ordinal)
            && string.Equals(direction, DefaultSortDirection, StringComparison.Ordinal);
    }

    private static string BuildUrl(int page, string sortBy, string sortDirection, bool includeSort)
    {
        var queryParts = new List<string>();
        if (page > 1)
        {
            queryParts.Add($"page={page}");
        }

        if (includeSort)
        {
            queryParts.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
            queryParts.Add($"sortDirection={Uri.EscapeDataString(sortDirection)}");
        }

        return queryParts.Count == 0
            ? "/event-log"
            : $"/event-log?{string.Join("&", queryParts)}";
    }
}
