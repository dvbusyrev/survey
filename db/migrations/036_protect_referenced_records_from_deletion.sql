\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '036') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 036_protect_referenced_records_from_deletion

BEGIN;

ALTER TABLE public.organization_survey
    DROP CONSTRAINT IF EXISTS organization_survey_organization_id_fkey,
    DROP CONSTRAINT IF EXISTS organization_survey_id_organization_fkey;

ALTER TABLE public.organization_survey
    ADD CONSTRAINT organization_survey_id_organization_fkey
    FOREIGN KEY (id_organization)
    REFERENCES public.organization (id_organization)
    ON DELETE RESTRICT;

ALTER TABLE public.organization_survey
    DROP CONSTRAINT IF EXISTS organization_survey_id_survey_fkey;

ALTER TABLE public.organization_survey
    ADD CONSTRAINT organization_survey_id_survey_fkey
    FOREIGN KEY (id_survey)
    REFERENCES public.survey (id_survey)
    ON DELETE RESTRICT;

ALTER TABLE public.answer
    DROP CONSTRAINT IF EXISTS answer_id_organization_survey_fkey;

ALTER TABLE public.answer
    ADD CONSTRAINT answer_id_organization_survey_fkey
    FOREIGN KEY (id_organization_survey)
    REFERENCES public.organization_survey (id_organization_survey)
    ON DELETE RESTRICT;

ALTER TABLE public.answer_draft
    DROP CONSTRAINT IF EXISTS answer_draft_id_organization_survey_fkey;

ALTER TABLE public.answer_draft
    ADD CONSTRAINT answer_draft_id_organization_survey_fkey
    FOREIGN KEY (id_organization_survey)
    REFERENCES public.organization_survey (id_organization_survey)
    ON DELETE RESTRICT;

INSERT INTO public.schema_migrations (version, name)
VALUES ('036', 'protect_referenced_records_from_deletion')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 036_protect_referenced_records_from_deletion
\endif
