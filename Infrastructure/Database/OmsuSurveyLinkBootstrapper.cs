using Npgsql;

namespace main_project.Infrastructure.Database;

public static class OmsuSurveyLinkBootstrapper
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
                       CREATE TABLE IF NOT EXISTS public.omsu_surveys (
                           id_omsu integer NOT NULL,
                           id_survey integer NOT NULL,
                           CONSTRAINT pk_omsu_surveys PRIMARY KEY (id_omsu, id_survey),
                           CONSTRAINT fk_omsu_surveys_omsu
                               FOREIGN KEY (id_omsu) REFERENCES public.omsu (id_omsu) ON DELETE CASCADE
                       );

                       CREATE INDEX IF NOT EXISTS idx_omsu_surveys_id_survey
                           ON public.omsu_surveys (id_survey);
                       """,
                       connection))
            {
                command.ExecuteNonQuery();
            }

            if (LegacyColumnExists(connection))
            {
                using var backfillCommand = new NpgsqlCommand(
                    """
                    INSERT INTO public.omsu_surveys (id_omsu, id_survey)
                    SELECT DISTINCT
                        o.id_omsu,
                        BTRIM(raw_survey_id)::integer AS id_survey
                    FROM public.omsu o
                    CROSS JOIN LATERAL unnest(string_to_array(COALESCE(o.list_surveys, ''), ',')) AS raw_survey_id
                    WHERE NULLIF(BTRIM(raw_survey_id), '') IS NOT NULL
                      AND BTRIM(raw_survey_id) ~ '^[0-9]+$'
                    ON CONFLICT (id_omsu, id_survey) DO NOTHING;
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
                  AND table_name = 'omsu'
                  AND column_name = 'list_surveys'
            );
            """,
            connection);

        return command.ExecuteScalar() is bool exists && exists;
    }
}
