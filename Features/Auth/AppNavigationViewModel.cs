using MainProject.Infrastructure.Security;

namespace MainProject.Web.ViewModels;

public sealed class AppNavigationViewModel
{
    public string UserRole { get; init; } = string.Empty;
    public string ActiveTab { get; init; } = string.Empty;

    public bool IsAdmin => AppRoles.Normalize(UserRole) == AppRoles.Admin;
}

public static class AppNavigationRouteResolver
{
    public static string Resolve(string? requestPath)
    {
        var path = NormalizePath(requestPath);

        if (path == "/statistics")
        {
            return "open_statistics";
        }

        if (path == "/survey-templates/archive")
        {
            return "archived_survey_templates";
        }

        if (path == "/survey-templates/create")
        {
            return "add_survey_template";
        }

        if (path == "/survey-templates"
            || IsNumberedAction(path, "/survey-templates/", "/edit"))
        {
            return "survey_templates";
        }

        if (path is "/survey/answer" or "/surveys/answers")
        {
            return "list_answers_users";
        }

        if (path == "/survey/archive"
            || path == "/surveys/archive"
            || IsNumberedAction(path, "/survey/archive/", "/edit")
            || IsNumberedAction(path, "/surveys/archive/", "/edit"))
        {
            return "archived_surveys";
        }

        if (path == "/settings/survey-creation" || path == "/survey-auto-creation")
        {
            return "survey_auto_creation";
        }

        if (path is "/survey/create" or "/surveys/create")
        {
            return "add_survey";
        }

        if (path == "/survey"
            || path == "/surveys"
            || IsNumberedAction(path, "/survey/", "/edit")
            || IsNumberedAction(path, "/surveys/", "/edit")
            || IsNumberedAction(path, "/survey/", "/copy")
            || IsNumberedAction(path, "/surveys/", "/copy"))
        {
            return "get_surveys";
        }

        if (path == "/users/archive")
        {
            return "archived_users";
        }

        if (path == "/users"
            || path == "/users/create"
            || IsNumberedAction(path, "/users/", "/edit"))
        {
            return "get_users";
        }

        if (path == "/organizations/archive")
        {
            return "archive_list_organizations";
        }

        if (path is "/organizations/survey" or "/organizations/surveys")
        {
            return "organization_surveys";
        }

        if (path == "/organizations"
            || path == "/organizations/create"
            || IsNumberedAction(path, "/organizations/", "/edit"))
        {
            return "get_organization";
        }

        if (path == "/reports")
        {
            return "reports";
        }

        if (path is "/settings/theme" or "/theme/configuration" or "/theme-settings")
        {
            return "theme_settings";
        }

        if (path is "/settings/email" or "/mail/configuration" or "/mail-settings")
        {
            return "email_settings";
        }

        if (path is "/email" or "/mail" or "/mail/new")
        {
            return "email_new";
        }

        if (path is "/logs" or "/event-log")
        {
            return "get_logs";
        }

        if (path == "/help")
        {
            return "help";
        }

        return "get_surveys";
    }

    private static string NormalizePath(string? requestPath)
    {
        var path = string.IsNullOrWhiteSpace(requestPath)
            ? "/"
            : requestPath.Trim().ToLowerInvariant();
        var queryIndex = path.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        return path.Length > 1 ? path.TrimEnd('/') : path;
    }

    private static bool IsNumberedAction(string path, string prefix, string suffix)
    {
        if (!path.StartsWith(prefix, StringComparison.Ordinal)
            || !path.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var id = path[prefix.Length..^suffix.Length];
        return int.TryParse(id, out var parsedId) && parsedId > 0;
    }
}
