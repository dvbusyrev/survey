\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '033') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 033_remove_obsolete_week_day

BEGIN;

DROP TABLE IF EXISTS public.week_day;

INSERT INTO public.schema_migrations (version, name)
VALUES ('033', 'remove_obsolete_week_day')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 033_remove_obsolete_week_day
\endif
