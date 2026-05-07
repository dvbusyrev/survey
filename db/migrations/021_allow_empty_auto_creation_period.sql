\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '021') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 021_allow_empty_auto_creation_period

BEGIN;

ALTER TABLE public.auto_creation_config
    DROP CONSTRAINT IF EXISTS ck_auto_creation_config_working_period;

ALTER TABLE public.auto_creation_config
    ALTER COLUMN working_period DROP NOT NULL,
    ALTER COLUMN working_period DROP DEFAULT;

ALTER TABLE public.auto_creation_config
    ADD CONSTRAINT ck_auto_creation_config_working_period
    CHECK (working_period IS NULL OR working_period BETWEEN 1 AND 14);

ALTER TABLE public.organization_survey
    ALTER COLUMN date_end DROP NOT NULL;

CREATE OR REPLACE VIEW public.survey_schedule AS
 SELECT s.id_survey,
    min(os.date_begin) AS date_begin,
    CASE
        WHEN bool_or(os.id_survey IS NOT NULL AND os.date_end IS NULL) THEN NULL::date
        ELSE max(os.date_end)
    END AS date_end
   FROM public.survey s
   LEFT JOIN public.organization_survey os
     ON os.id_survey = s.id_survey
  GROUP BY s.id_survey;

INSERT INTO public.schema_migrations (version, name)
VALUES ('021', 'allow_empty_auto_creation_period')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 021_allow_empty_auto_creation_period
\endif
