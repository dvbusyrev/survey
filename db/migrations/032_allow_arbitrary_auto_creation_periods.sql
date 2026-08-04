\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '032') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 032_allow_arbitrary_auto_creation_periods

BEGIN;

ALTER TABLE public.auto_creation_config
    DROP CONSTRAINT IF EXISTS ck_auto_creation_config_working_period,
    DROP CONSTRAINT IF EXISTS ck_auto_creation_config_reporting_offset;

ALTER TABLE public.auto_creation_config
    ADD CONSTRAINT ck_auto_creation_config_working_period
        CHECK (working_period >= 1),
    ADD CONSTRAINT ck_auto_creation_config_reporting_offset
        CHECK (reporting_offset_business_days >= 1);

INSERT INTO public.schema_migrations (version, name)
VALUES ('032', 'allow_arbitrary_auto_creation_periods')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 032_allow_arbitrary_auto_creation_periods
\endif
