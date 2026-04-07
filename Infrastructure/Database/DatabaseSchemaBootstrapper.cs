using Npgsql;

namespace main_project.Infrastructure.Database;

public static class DatabaseSchemaBootstrapper
{
    public static void EnsureInitialized(NpgsqlConnection connection)
    {
        OmsuSurveyLinkBootstrapper.EnsureInitialized(connection);
        UserRoleBootstrapper.EnsureInitialized(connection);
    }
}
