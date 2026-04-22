\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '005') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 005_add_organization_survey_schedule

BEGIN;

ALTER TABLE public.organization_survey
    ADD COLUMN IF NOT EXISTS date_open date,
    ADD COLUMN IF NOT EXISTS date_close date,
    ADD COLUMN IF NOT EXISTS is_custom_end_date boolean NOT NULL DEFAULT false;

UPDATE public.organization_survey os
SET
    date_open = COALESCE(os.date_open, s.date_open),
    date_close = COALESCE(
        os.date_close,
        GREATEST(COALESCE(os.extended_until::date, s.date_close), s.date_close)
    ),
    is_custom_end_date = COALESCE(os.is_custom_end_date, false)
        OR (
            os.extended_until IS NOT NULL
            AND os.extended_until::date > s.date_close
        )
FROM public.survey s
WHERE s.id_survey = os.id_survey;

ALTER TABLE public.organization_survey
    ALTER COLUMN date_open SET NOT NULL,
    ALTER COLUMN date_close SET NOT NULL,
    ALTER COLUMN is_custom_end_date SET DEFAULT false,
    ALTER COLUMN is_custom_end_date SET NOT NULL;

INSERT INTO public.schema_migrations (version, name)
VALUES ('005', 'add_organization_survey_schedule')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 005_add_organization_survey_schedule
\endif
