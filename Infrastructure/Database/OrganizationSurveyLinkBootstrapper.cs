using Npgsql;

namespace MainProject.Infrastructure.Database;

public static class OrganizationSurveyLinkBootstrapper
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

            using (var command = new NpgsqlCommand(
                       """
                       CREATE TABLE IF NOT EXISTS public.organization_survey (
                           organization_id integer NOT NULL,
                           id_survey integer NOT NULL,
                           extended_until timestamp without time zone,
                           CONSTRAINT organization_survey_pkey PRIMARY KEY (organization_id, id_survey),
                           CONSTRAINT organization_survey_organization_id_fkey
                               FOREIGN KEY (organization_id) REFERENCES public.organization (organization_id) ON DELETE CASCADE
                       );

                       ALTER TABLE public.organization_survey
                           ADD COLUMN IF NOT EXISTS extended_until timestamp without time zone;

                       CREATE INDEX IF NOT EXISTS idx_organization_survey_id_survey
                           ON public.organization_survey (id_survey);
                       """,
                       connection))
            {
                command.ExecuteNonQuery();
            }

            if (LegacyColumnExists(connection))
            {
                using var backfillCommand = new NpgsqlCommand(
                    """
                    INSERT INTO public.organization_survey (organization_id, id_survey)
                    SELECT DISTINCT
                        o.organization_id,
                        BTRIM(raw_survey_id)::integer AS id_survey
                    FROM public.organization o
                    CROSS JOIN LATERAL unnest(string_to_array(COALESCE(o.list_surveys, ''), ',')) AS raw_survey_id
                    WHERE NULLIF(BTRIM(raw_survey_id), '') IS NOT NULL
                      AND BTRIM(raw_survey_id) ~ '^[0-9]+$'
                    ON CONFLICT (organization_id, id_survey) DO NOTHING;
                    """,
                    connection);

                backfillCommand.ExecuteNonQuery();
            }

            _initialized = true;
        }
    }

    private static bool LegacyColumnExists(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'organization'
                  AND column_name = 'list_surveys'
            );
            """,
            connection);

        return command.ExecuteScalar() is bool exists && exists;
    }
}
