namespace MainProject.Application.Support;

public static class AppSortState
{
    public static bool HasExplicitSort(string? sortBy)
    {
        return !string.IsNullOrWhiteSpace(sortBy);
    }

    public static string NormalizeExplicitDirection(string? sortDirection)
    {
        return string.Equals(sortDirection?.Trim(), "desc", StringComparison.OrdinalIgnoreCase)
            ? "desc"
            : "asc";
    }
}
