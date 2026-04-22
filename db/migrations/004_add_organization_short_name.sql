\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '004') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 004_add_organization_short_name

BEGIN;

ALTER TABLE public.organization
    ADD COLUMN IF NOT EXISTS organization_short_name text;

INSERT INTO public.schema_migrations (version, name)
VALUES ('004', 'add_organization_short_name')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 004_add_organization_short_name
\endif
