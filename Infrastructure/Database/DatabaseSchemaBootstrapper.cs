using Npgsql;

namespace main_project.Infrastructure.Database;

public static class DatabaseSchemaBootstrapper
{
    public static void EnsureInitialized(NpgsqlConnection connection)
    {
        OrganizationSchemaBootstrapper.EnsureInitialized(connection);
        SingularTableNamingBootstrapper.EnsureInitialized(connection);
        OrganizationSurveyLinkBootstrapper.EnsureInitialized(connection);
        UserRoleBootstrapper.EnsureInitialized(connection);
        SurveyNormalizationBootstrapper.EnsureInitialized(connection);
        CrudAuditBootstrapper.EnsureInitialized(connection);
    }
}
