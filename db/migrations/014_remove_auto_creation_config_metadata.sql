\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '014') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 014_remove_auto_creation_config_metadata

BEGIN;

DROP TRIGGER IF EXISTS trg_auto_creation_config_set_update_metadata ON public.auto_creation_config;

ALTER TABLE public.auto_creation_config
    DROP COLUMN IF EXISTS last_processed_schedule_date,
    DROP COLUMN IF EXISTS date_update,
    DROP COLUMN IF EXISTS user_update;

INSERT INTO public.schema_migrations (version, name)
VALUES ('014', 'remove_auto_creation_config_metadata')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 014_remove_auto_creation_config_metadata
\endif
