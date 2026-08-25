\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW()
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '048') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 048_allow_open_ended_survey_templates

BEGIN;

ALTER TABLE public.survey_template
    ALTER COLUMN date_end DROP NOT NULL;

ALTER TABLE public.survey_template
    DROP CONSTRAINT IF EXISTS survey_template_date_range_check;

ALTER TABLE public.survey_template
    ADD CONSTRAINT survey_template_date_range_check
    CHECK (date_end IS NULL OR date_end > date_begin);

INSERT INTO public.schema_migrations (version, name)
VALUES ('048', 'allow_open_ended_survey_templates')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 048_allow_open_ended_survey_templates
\endif
