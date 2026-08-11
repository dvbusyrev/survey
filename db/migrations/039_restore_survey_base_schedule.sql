\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '039') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 039_restore_survey_base_schedule

BEGIN;

ALTER TABLE public.survey
    ADD COLUMN IF NOT EXISTS date_begin date,
    ADD COLUMN IF NOT EXISTS date_end date;

ALTER TABLE public.survey_l
    ADD COLUMN IF NOT EXISTS date_begin date,
    ADD COLUMN IF NOT EXISTS date_end date;

WITH assignment_periods AS (
    SELECT
        assignment.id_survey,
        MIN(assignment.date_begin) AS date_begin,
        CASE
            WHEN BOOL_OR(assignment.date_end IS NULL) THEN NULL::date
            ELSE MIN(assignment.date_end)
        END AS date_end
    FROM public.organization_survey assignment
    GROUP BY assignment.id_survey
)
UPDATE public.survey survey
SET
    date_begin = assignment_periods.date_begin,
    date_end = assignment_periods.date_end
FROM assignment_periods
WHERE assignment_periods.id_survey = survey.id_survey
  AND survey.date_begin IS NULL;

ALTER TABLE public.survey
    DROP CONSTRAINT IF EXISTS survey_date_range_check,
    ADD CONSTRAINT survey_date_range_check
        CHECK (date_begin IS NULL OR date_end IS NULL OR date_end >= date_begin);

CREATE OR REPLACE VIEW public.survey_schedule AS
SELECT
    survey.id_survey,
    survey.date_begin,
    survey.date_end
FROM public.survey survey;

INSERT INTO public.schema_migrations (version, name)
VALUES ('039', 'restore_survey_base_schedule')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 039_restore_survey_base_schedule
\endif
