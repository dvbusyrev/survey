namespace main_project.Infrastructure.Security;

public static class AppRoles
{
    public const string Admin = "Админ";
    public const string User = "user";

    public static IReadOnlyList<string> SupportedRoles { get; } = new[] { Admin, User };

    public static bool IsSupported(string? role)
    {
        return string.Equals(role, Admin, StringComparison.Ordinal)
            || string.Equals(role, User, StringComparison.Ordinal);
    }

    public static string Normalize(string? role)
    {
        var trimmedRole = role?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedRole))
        {
            return string.Empty;
        }

        if (string.Equals(trimmedRole, Admin, StringComparison.Ordinal)
            || string.Equals(trimmedRole, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmedRole, "administrator", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmedRole, "админ", StringComparison.OrdinalIgnoreCase))
        {
            return Admin;
        }

        if (string.Equals(trimmedRole, User, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmedRole, "пользователь", StringComparison.OrdinalIgnoreCase))
        {
            return User;
        }

        return trimmedRole;
    }
}
