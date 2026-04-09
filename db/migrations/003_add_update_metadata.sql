\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '003') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 003_add_update_metadata

BEGIN;

\ir ../../recovery/update_metadata_support.sql

INSERT INTO public.schema_migrations (version, name)
VALUES ('003', 'add_update_metadata')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 003_add_update_metadata
\endif
