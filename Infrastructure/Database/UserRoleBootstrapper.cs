using main_project.Infrastructure.Security;
using Npgsql;

namespace main_project.Infrastructure.Database;

public static class UserRoleBootstrapper
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    public static void EnsureInitialized(NpgsqlConnection connection)
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            using (var normalizeCommand = new NpgsqlCommand(
                       """
                       UPDATE public.users
                       SET name_role = CASE
                           WHEN name_role IS NULL THEN name_role
                           WHEN BTRIM(name_role) IN ('Админ', 'админ', 'АДМИН', 'admin', 'Admin', 'ADMIN', 'administrator', 'Administrator', 'ADMINISTRATOR')
                               THEN 'Админ'
                           WHEN LOWER(BTRIM(name_role)) IN ('user', 'пользователь')
                               THEN 'user'
                           ELSE BTRIM(name_role)
                       END
                       WHERE name_role IS DISTINCT FROM CASE
                           WHEN name_role IS NULL THEN name_role
                           WHEN BTRIM(name_role) IN ('Админ', 'админ', 'АДМИН', 'admin', 'Admin', 'ADMIN', 'administrator', 'Administrator', 'ADMINISTRATOR')
                               THEN 'Админ'
                           WHEN LOWER(BTRIM(name_role)) IN ('user', 'пользователь')
                               THEN 'user'
                           ELSE BTRIM(name_role)
                       END;
                       """,
                       connection))
            {
                normalizeCommand.ExecuteNonQuery();
            }

            EnsureOnlySupportedRolesRemain(connection);

            using (var constraintCommand = new NpgsqlCommand(
                       """
                       DO $$
                       BEGIN
                           IF NOT EXISTS (
                               SELECT 1
                               FROM pg_constraint
                               WHERE conname = 'chk_users_name_role'
                                 AND conrelid = 'public.users'::regclass
                           ) THEN
                               ALTER TABLE public.users
                                   ADD CONSTRAINT chk_users_name_role
                                   CHECK (name_role IN ('Админ', 'user'));
                           END IF;
                       END $$;
                       """,
                       connection))
            {
                constraintCommand.ExecuteNonQuery();
            }

            _initialized = true;
        }
    }

    private static void EnsureOnlySupportedRolesRemain(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(
            """
            SELECT string_agg(DISTINCT name_role, ', ' ORDER BY name_role)
            FROM public.users
            WHERE name_role NOT IN ('Админ', 'user');
            """,
            connection);

        var unsupportedRoles = command.ExecuteScalar() as string;
        if (!string.IsNullOrWhiteSpace(unsupportedRoles))
        {
            throw new InvalidOperationException(
                $"В public.users обнаружены неподдерживаемые роли: {unsupportedRoles}. " +
                $"Допустимые роли: {string.Join(", ", AppRoles.SupportedRoles)}.");
        }
    }
}
