using System.Globalization;

namespace MainProject.Application.Support;

public static class AppListPaging
{
    public const int DefaultPageSize = 10;

    public static StringComparer RuStringComparer { get; } = StringComparer.Create(
        new CultureInfo("ru-RU"),
        ignoreCase: true);

    public static PagedSlice<T> Slice<T>(IReadOnlyList<T> items, int currentPage, int pageSize = DefaultPageSize)
    {
        var window = CreateWindow(items.Count, currentPage, pageSize);
        var pageItems = items
            .Skip(window.Offset)
            .Take(window.PageSize)
            .ToList();

        return new PagedSlice<T>(
            pageItems,
            window.CurrentPage,
            window.TotalPages,
            window.TotalCount,
            window.PageSize);
    }

    public static PageWindow CreateWindow(int totalCount, int currentPage, int pageSize = DefaultPageSize)
    {
        var normalizedPageSize = pageSize > 0 ? pageSize : DefaultPageSize;
        var normalizedTotalCount = Math.Max(totalCount, 0);
        var totalPages = normalizedTotalCount == 0
            ? 1
            : (int)Math.Ceiling((double)normalizedTotalCount / normalizedPageSize);
        var normalizedPage = Math.Clamp(currentPage, 1, totalPages);

        return new PageWindow(
            normalizedPage,
            totalPages,
            normalizedTotalCount,
            normalizedPageSize,
            (normalizedPage - 1) * normalizedPageSize);
    }

    public readonly record struct PagedSlice<T>(
        IReadOnlyList<T> Items,
        int CurrentPage,
        int TotalPages,
        int TotalCount,
        int PageSize);

    public readonly record struct PageWindow(
        int CurrentPage,
        int TotalPages,
        int TotalCount,
        int PageSize,
        int Offset);
}
