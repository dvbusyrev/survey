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
        var normalizedPageSize = pageSize > 0 ? pageSize : DefaultPageSize;
        var totalCount = items.Count;
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling((double)totalCount / normalizedPageSize);
        var normalizedPage = Math.Clamp(currentPage, 1, totalPages);
        var pageItems = items
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return new PagedSlice<T>(
            pageItems,
            normalizedPage,
            totalPages,
            totalCount,
            normalizedPageSize);
    }

    public readonly record struct PagedSlice<T>(
        IReadOnlyList<T> Items,
        int CurrentPage,
        int TotalPages,
        int TotalCount,
        int PageSize);
}
