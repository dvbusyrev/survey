\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '002') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 002_repair_survey_foreign_keys
\ir ../../recovery/repair_live_constraints.sql

BEGIN;

INSERT INTO public.schema_migrations (version, name)
VALUES ('002', 'repair_survey_foreign_keys')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 002_repair_survey_foreign_keys
\endif
