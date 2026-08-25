\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW()
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '042') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 042_add_survey_templates

BEGIN;

ALTER TABLE public.survey
    ADD COLUMN IF NOT EXISTS is_template boolean NOT NULL DEFAULT false;

ALTER TABLE public.survey_l
    ADD COLUMN IF NOT EXISTS is_template boolean;

CREATE INDEX IF NOT EXISTS idx_survey_template_period
    ON public.survey (is_template, date_begin, date_end, id_survey);

INSERT INTO public.schema_migrations (version, name)
VALUES ('042', 'add_survey_templates')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 042_add_survey_templates
\endif
