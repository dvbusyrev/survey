\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW()
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '041') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 041_remove_redundant_user_update

BEGIN;

-- Preserve the only remaining business relation that previously used
-- user_update before removing the generic metadata column.
INSERT INTO public.answer_participant (id_answer, id_user, participation_type)
SELECT answer.id_answer, answer.user_update, 'legacy'
FROM public.answer answer
INNER JOIN public.app_user app_user
    ON app_user.id_user = answer.user_update
WHERE answer.user_update IS NOT NULL
ON CONFLICT DO NOTHING;

CREATE OR REPLACE FUNCTION public.set_update_metadata()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.date_update = NOW();
    RETURN NEW;
END;
$function$;

DO $migration$
DECLARE
    table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY[
        'schema_migrations',
        'answer',
        'answer_item',
        'app_user',
        'email_config',
        'organization',
        'organization_survey',
        'survey',
        'survey_auto_creation_config',
        'survey_question',
        'theme_config',
        'answer_l',
        'answer_item_l',
        'app_user_l',
        'email_config_l',
        'organization_l',
        'organization_survey_l',
        'survey_l',
        'survey_auto_creation_config_l',
        'survey_question_l',
        'theme_config_l'
    ]
    LOOP
        IF to_regclass(format('public.%I', table_name)) IS NOT NULL THEN
            EXECUTE format(
                'ALTER TABLE public.%I DROP COLUMN IF EXISTS user_update',
                table_name
            );
        END IF;
    END LOOP;
END;
$migration$;

INSERT INTO public.schema_migrations (version, name)
VALUES ('041', 'remove_redundant_user_update')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 041_remove_redundant_user_update
\endif
