\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW()
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '050') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 050_add_planned_survey_templates

BEGIN;

ALTER TABLE public.survey_template
    ADD COLUMN IF NOT EXISTS ancestor_id integer;

ALTER TABLE public.survey_template_l
    ADD COLUMN IF NOT EXISTS ancestor_id integer;

ALTER TABLE public.survey_template
    DROP CONSTRAINT IF EXISTS survey_template_ancestor_id_fkey;

ALTER TABLE public.survey_template
    ADD CONSTRAINT survey_template_ancestor_id_fkey
    FOREIGN KEY (ancestor_id)
    REFERENCES public.survey_template(id_survey_template)
    ON DELETE RESTRICT;

ALTER TABLE public.survey_template
    DROP CONSTRAINT IF EXISTS survey_template_ancestor_not_self_check;

ALTER TABLE public.survey_template
    ADD CONSTRAINT survey_template_ancestor_not_self_check
    CHECK (ancestor_id IS NULL OR ancestor_id <> id_survey_template);

-- The UI and service still require date_end > date_begin. Equality is allowed only
-- for a parent that is archived automatically one day before its planned successor.
ALTER TABLE public.survey_template
    DROP CONSTRAINT IF EXISTS survey_template_date_range_check;

ALTER TABLE public.survey_template
    ADD CONSTRAINT survey_template_date_range_check
    CHECK (date_end IS NULL OR date_end >= date_begin);

CREATE INDEX IF NOT EXISTS idx_survey_template_ancestor
    ON public.survey_template (ancestor_id)
    WHERE ancestor_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_survey_template_planned_dates
    ON public.survey_template (date_begin, date_end, id_survey_template)
    WHERE ancestor_id IS NOT NULL;

INSERT INTO public.schema_migrations (version, name)
VALUES ('050', 'add_planned_survey_templates')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 050_add_planned_survey_templates
\endif
