\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW()
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '045') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 045_remove_obsolete_user_csp_key

BEGIN;

ALTER TABLE IF EXISTS public.app_user
    DROP COLUMN IF EXISTS key_csp;

ALTER TABLE IF EXISTS public.app_user_l
    DROP COLUMN IF EXISTS key_csp;

INSERT INTO public.schema_migrations (version, name)
VALUES ('045', 'remove_obsolete_user_csp_key')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 045_remove_obsolete_user_csp_key
\endif
