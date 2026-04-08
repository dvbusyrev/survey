\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '001') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 001_unified_schema
\ir ../../recovery/reconstruct_schema.sql

BEGIN;

INSERT INTO public.schema_migrations (version, name)
VALUES ('001', 'unified_schema')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 001_unified_schema
\endif
