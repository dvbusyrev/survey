using Npgsql;

namespace main_project.Infrastructure.Database;

public static class OrganizationSchemaBootstrapper
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

            using var command = new NpgsqlCommand(
                """
                DO $$
                BEGIN
                    IF to_regclass('public.omsu') IS NOT NULL AND to_regclass('public.organization') IS NULL THEN
                        EXECUTE 'ALTER TABLE public.omsu RENAME TO organization';
                    END IF;

                    IF to_regclass('public.omsu_surveys') IS NOT NULL AND to_regclass('public.organization_survey') IS NULL THEN
                        EXECUTE 'ALTER TABLE public.omsu_surveys RENAME TO organization_survey';
                    END IF;

                    IF to_regclass('public.omsu_l') IS NOT NULL AND to_regclass('public.organization_l') IS NULL THEN
                        EXECUTE 'ALTER TABLE public.omsu_l RENAME TO organization_l';
                    END IF;

                    IF to_regclass('public.omsu_surveys_l') IS NOT NULL AND to_regclass('public.organization_survey_l') IS NULL THEN
                        EXECUTE 'ALTER TABLE public.omsu_surveys_l RENAME TO organization_survey_l';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'organization'
                          AND column_name = 'id_omsu'
                    ) THEN
                        EXECUTE 'ALTER TABLE public.organization RENAME COLUMN id_omsu TO organization_id';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'organization'
                          AND column_name = 'name_omsu'
                    ) THEN
                        EXECUTE 'ALTER TABLE public.organization RENAME COLUMN name_omsu TO organization_name';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'app_user'
                          AND column_name = 'id_omsu'
                    ) THEN
                        EXECUTE 'ALTER TABLE public.app_user RENAME COLUMN id_omsu TO organization_id';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'history_answer'
                          AND column_name = 'id_omsu'
                    ) THEN
                        EXECUTE 'ALTER TABLE public.history_answer RENAME COLUMN id_omsu TO organization_id';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'answer'
                          AND column_name = 'id_omsu'
                    ) THEN
                        EXECUTE 'ALTER TABLE public.answer RENAME COLUMN id_omsu TO organization_id';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'history_answer'
                          AND column_name = 'name_omsu'
                    ) THEN
                        EXECUTE 'ALTER TABLE public.history_answer RENAME COLUMN name_omsu TO organization_name';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'answer'
                          AND column_name = 'name_omsu'
                    ) THEN
                        EXECUTE 'ALTER TABLE public.answer RENAME COLUMN name_omsu TO organization_name';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'access_extension'
                          AND column_name = 'id_omsu'
                    ) THEN
                        EXECUTE 'ALTER TABLE public.access_extension RENAME COLUMN id_omsu TO organization_id';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'organization_survey'
                          AND column_name = 'id_omsu'
                    ) THEN
                        EXECUTE 'ALTER TABLE public.organization_survey RENAME COLUMN id_omsu TO organization_id';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM pg_class
                        WHERE relkind = 'i'
                          AND relnamespace = 'public'::regnamespace
                          AND relname = 'idx_omsu_block'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM pg_class
                        WHERE relkind = 'i'
                          AND relnamespace = 'public'::regnamespace
                          AND relname = 'idx_organization_block'
                    ) THEN
                        EXECUTE 'ALTER INDEX public.idx_omsu_block RENAME TO idx_organization_block';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM pg_class
                        WHERE relkind = 'i'
                          AND relnamespace = 'public'::regnamespace
                          AND relname = 'idx_omsu_date_end'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM pg_class
                        WHERE relkind = 'i'
                          AND relnamespace = 'public'::regnamespace
                          AND relname = 'idx_organization_date_end'
                    ) THEN
                        EXECUTE 'ALTER INDEX public.idx_omsu_date_end RENAME TO idx_organization_date_end';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM pg_class
                        WHERE relkind = 'i'
                          AND relnamespace = 'public'::regnamespace
                          AND relname = 'idx_omsu_surveys_id_survey'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM pg_class
                        WHERE relkind = 'i'
                          AND relnamespace = 'public'::regnamespace
                          AND relname = 'idx_organization_survey_id_survey'
                    ) THEN
                        EXECUTE 'ALTER INDEX public.idx_omsu_surveys_id_survey RENAME TO idx_organization_survey_id_survey';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM pg_class
                        WHERE relkind = 'i'
                          AND relnamespace = 'public'::regnamespace
                          AND relname = 'idx_users_id_omsu'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM pg_class
                        WHERE relkind = 'i'
                          AND relnamespace = 'public'::regnamespace
                          AND relname = 'idx_app_user_organization_id'
                    ) THEN
                        EXECUTE 'ALTER INDEX public.idx_users_id_omsu RENAME TO idx_app_user_organization_id';
                    END IF;
                END $$;
                """,
                connection);

            command.ExecuteNonQuery();
            _initialized = true;
        }
    }
}
