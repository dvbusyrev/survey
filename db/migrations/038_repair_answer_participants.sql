\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '038') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 038_repair_answer_participants

BEGIN;

INSERT INTO public.answer_participant (id_answer, id_user, participation_type)
SELECT answer.id_answer, answer.user_update, 'legacy'
FROM public.answer answer
INNER JOIN public.app_user app_user
    ON app_user.id_user = answer.user_update
WHERE answer.user_update IS NOT NULL
ON CONFLICT DO NOTHING;

INSERT INTO public.schema_migrations (version, name)
VALUES ('038', 'repair_answer_participants')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 038_repair_answer_participants
\endif
