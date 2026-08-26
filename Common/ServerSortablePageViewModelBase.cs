namespace MainProject.Web.ViewModels;

public abstract class ServerSortablePageViewModelBase
{
    public bool HasExplicitSort { get; init; }
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalCount { get; init; }
    public int PageSize { get; init; } = 10;
    public string SortBy { get; init; } = string.Empty;
    public string SortDirection { get; init; } = string.Empty;

    protected abstract string BasePath { get; }
    protected abstract string DefaultSortField { get; }
    protected abstract string DefaultSortDirection { get; }
    protected abstract string PaginationAriaLabel { get; }

    protected virtual string ScrollAnchorId => string.Empty;

    public AppPaginationViewModel Pagination => AppPaginationViewModel.Create(
        CurrentPage,
        TotalPages,
        PaginationAriaLabel,
        BuildPageUrl,
        ScrollAnchorId);

    public bool IsSortedBy(string field)
    {
        if (!HasExplicitSort)
        {
            return false;
        }

        return string.Equals(
            NormalizeSortField(SortBy),
            NormalizeSortField(field),
            StringComparison.Ordinal);
    }

    public string GetHeaderSortDirection(string field)
    {
        return IsSortedBy(field)
            ? NormalizeSortDirection(SortDirection, NormalizeSortField(field))
            : string.Empty;
    }

    public string GetAriaSort(string field)
    {
        if (!IsSortedBy(field))
        {
            return "none";
        }

        return string.Equals(
            NormalizeSortDirection(SortDirection, NormalizeSortField(field)),
            "asc",
            StringComparison.Ordinal)
            ? "ascending"
            : "descending";
    }

    public string BuildPageUrl(int page)
    {
        return BuildUrl(
            page,
            NormalizeSortField(SortBy),
            NormalizeSortDirection(SortDirection, NormalizeSortField(SortBy)),
            HasExplicitSort);
    }

    public string BuildSortUrl(string field)
    {
        var normalizedField = NormalizeSortField(field);
        var initialDirection = NormalizeSortDirection(null, normalizedField);
        if (!IsSortedBy(normalizedField))
        {
            return BuildUrl(1, normalizedField, initialDirection, includeSort: true);
        }

        var currentDirection = NormalizeSortDirection(SortDirection, normalizedField);
        if (string.Equals(currentDirection, initialDirection, StringComparison.Ordinal))
        {
            var oppositeDirection = string.Equals(initialDirection, "asc", StringComparison.Ordinal)
                ? "desc"
                : "asc";
            return BuildUrl(1, normalizedField, oppositeDirection, includeSort: true);
        }

        return BuildUrl(1, normalizedField, string.Empty, includeSort: false);
    }

    protected virtual IEnumerable<KeyValuePair<string, string>> BuildAdditionalQueryParameters()
    {
        yield break;
    }

    protected abstract string NormalizeSortField(string? field);

    protected virtual string NormalizeSortDirection(string? direction, string sortField)
    {
        if (string.Equals(direction?.Trim(), "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        if (string.Equals(direction?.Trim(), "desc", StringComparison.OrdinalIgnoreCase))
        {
            return "desc";
        }

        return string.Equals(sortField, DefaultSortField, StringComparison.Ordinal)
            ? DefaultSortDirection
            : GetDefaultDirectionForField(sortField);
    }

    protected virtual string GetDefaultDirectionForField(string field)
    {
        return "asc";
    }

    protected virtual bool IsDefaultSort(string field, string direction)
    {
        return string.Equals(field, DefaultSortField, StringComparison.Ordinal)
            && string.Equals(direction, DefaultSortDirection, StringComparison.Ordinal);
    }

    private string BuildUrl(int page, string sortBy, string sortDirection, bool includeSort)
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

        foreach (var pair in BuildAdditionalQueryParameters())
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            queryParts.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
        }

        return queryParts.Count == 0
            ? BasePath
            : $"{BasePath}?{string.Join("&", queryParts)}";
    }
}
