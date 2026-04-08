\set ON_ERROR_STOP on

BEGIN;

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW()
);

COMMIT;

\ir 001_unified_schema.sql
\ir 002_repair_survey_foreign_keys.sql
