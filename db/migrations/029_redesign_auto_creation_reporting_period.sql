\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '029') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 029_redesign_auto_creation_reporting_period

BEGIN;

ALTER TABLE public.auto_creation_config
    ADD COLUMN IF NOT EXISTS reporting_period text NOT NULL DEFAULT 'month',
    ADD COLUMN IF NOT EXISTS reporting_offset_business_days integer NOT NULL DEFAULT 1;

ALTER TABLE public.auto_creation_config_l
    ADD COLUMN IF NOT EXISTS reporting_period text,
    ADD COLUMN IF NOT EXISTS reporting_offset_business_days integer;

ALTER TABLE public.auto_creation_config
    DROP CONSTRAINT IF EXISTS ck_auto_creation_config_working_period,
    DROP CONSTRAINT IF EXISTS ck_auto_creation_config_reporting_period,
    DROP CONSTRAINT IF EXISTS ck_auto_creation_config_reporting_offset;

UPDATE public.auto_creation_config
SET working_period = 8
WHERE working_period IS NULL;

ALTER TABLE public.auto_creation_config
    ALTER COLUMN working_period SET DEFAULT 8,
    ALTER COLUMN working_period SET NOT NULL,
    ADD CONSTRAINT ck_auto_creation_config_working_period
        CHECK (working_period BETWEEN 1 AND 14),
    ADD CONSTRAINT ck_auto_creation_config_reporting_period
        CHECK (reporting_period IN ('month', 'quarter', 'half-year', 'year')),
    ADD CONSTRAINT ck_auto_creation_config_reporting_offset
        CHECK (reporting_offset_business_days BETWEEN 1 AND 14);

ALTER TABLE public.auto_creation_config
    DROP COLUMN IF EXISTS id_creation_day CASCADE,
    DROP COLUMN IF EXISTS id_begin_day CASCADE;

ALTER TABLE public.auto_creation_config_l
    DROP COLUMN IF EXISTS id_creation_day,
    DROP COLUMN IF EXISTS id_begin_day;

WITH ranked AS (
    SELECT
        selected.ctid,
        row_number() OVER (
            PARTITION BY selected.id_config, lower(btrim(survey.name_survey))
            ORDER BY selected.id_survey
        ) AS position
    FROM public.survey_auto_creation_config selected
    INNER JOIN public.survey survey ON survey.id_survey = selected.id_survey
)
DELETE FROM public.survey_auto_creation_config selected
USING ranked
WHERE selected.ctid = ranked.ctid
  AND ranked.position > 1;

INSERT INTO public.schema_migrations (version, name)
VALUES ('029', 'redesign_auto_creation_reporting_period')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 029_redesign_auto_creation_reporting_period
\endif
