namespace MainProject.Web.ViewModels;

public sealed class AppPaginationItemViewModel
{
    public string Label { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public string? Url { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsAction { get; init; }
}

public sealed class AppPaginationViewModel
{
    private const int MaxVisiblePages = 5;

    public string AriaLabel { get; init; } = "Навигация по страницам";
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public string ScrollAnchorId { get; init; } = string.Empty;
    public IReadOnlyList<AppPaginationItemViewModel> VisiblePages { get; init; } = Array.Empty<AppPaginationItemViewModel>();
    public AppPaginationItemViewModel? FirstShortcut { get; init; }
    public AppPaginationItemViewModel? NextShortcut { get; init; }

    public bool ShowPagination => TotalPages > 1;

    public static AppPaginationViewModel Create(
        int currentPage,
        int totalPages,
        string ariaLabel,
        Func<int, string>? urlBuilder = null,
        string? scrollAnchorId = null)
    {
        var normalizedTotalPages = Math.Max(totalPages, 1);
        var normalizedCurrentPage = Math.Clamp(currentPage, 1, normalizedTotalPages);

        var visiblePageStart = GetVisiblePageStart(normalizedCurrentPage, normalizedTotalPages);
        var visiblePageEnd = Math.Min(normalizedTotalPages, visiblePageStart + MaxVisiblePages - 1);

        var visiblePages = new List<AppPaginationItemViewModel>(visiblePageEnd - visiblePageStart + 1);
        for (var page = visiblePageStart; page <= visiblePageEnd; page += 1)
        {
            visiblePages.Add(CreateItem(page, page.ToString(), page == normalizedCurrentPage, false, urlBuilder));
        }

        return new AppPaginationViewModel
        {
            AriaLabel = ariaLabel,
            CurrentPage = normalizedCurrentPage,
            TotalPages = normalizedTotalPages,
            ScrollAnchorId = string.IsNullOrWhiteSpace(scrollAnchorId) ? string.Empty : scrollAnchorId.Trim(),
            VisiblePages = visiblePages,
            FirstShortcut = normalizedTotalPages > MaxVisiblePages && visiblePageStart > 1
                ? CreateItem(1, "В начало", false, true, urlBuilder)
                : null,
            NextShortcut = normalizedCurrentPage < normalizedTotalPages
                ? CreateItem(normalizedCurrentPage + 1, "Дальше", false, true, urlBuilder)
                : null
        };
    }

    private static AppPaginationItemViewModel CreateItem(int page, string label, bool isCurrent, bool isAction, Func<int, string>? urlBuilder)
    {
        return new AppPaginationItemViewModel
        {
            Label = label,
            Page = page,
            Url = isCurrent ? null : urlBuilder?.Invoke(page),
            IsCurrent = isCurrent,
            IsAction = isAction
        };
    }

    private static int GetVisiblePageStart(int currentPage, int totalPages)
    {
        if (totalPages <= MaxVisiblePages)
        {
            return 1;
        }

        var startPage = currentPage - 2;
        if (startPage < 1)
        {
            startPage = 1;
        }

        var maxStartPage = totalPages - MaxVisiblePages + 1;
        if (startPage > maxStartPage)
        {
            startPage = maxStartPage;
        }

        return startPage;
    }
}
