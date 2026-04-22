\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '009') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 009_derive_survey_schedule_from_assignments

BEGIN;

DROP TRIGGER IF EXISTS trg_organization_survey_apply_schedule_defaults ON public.organization_survey;
DROP TRIGGER IF EXISTS trg_organization_survey_track_schedule_sync ON public.organization_survey;
DROP TRIGGER IF EXISTS trg_survey_propagate_schedule_to_assignments ON public.survey;

DROP FUNCTION IF EXISTS public.organization_survey_apply_schedule_defaults();
DROP FUNCTION IF EXISTS public.organization_survey_track_schedule_sync();
DROP FUNCTION IF EXISTS public.survey_propagate_schedule_to_assignments();

DROP TABLE IF EXISTS public.organization_survey_schedule_sync;
DROP VIEW IF EXISTS public.survey_schedule;

ALTER TABLE public.survey
    DROP COLUMN IF EXISTS date_begin,
    DROP COLUMN IF EXISTS date_end;

CREATE VIEW public.survey_schedule AS
SELECT
    s.id_survey,
    MIN(os.date_begin) AS date_begin,
    MAX(os.date_end) AS date_end
FROM public.survey s
LEFT JOIN public.organization_survey os
    ON os.id_survey = s.id_survey
GROUP BY s.id_survey;

INSERT INTO public.schema_migrations (version, name)
VALUES ('009', 'derive_survey_schedule_from_assignments')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 009_derive_survey_schedule_from_assignments
\endif
