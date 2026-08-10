\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '037') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 037_store_answer_participants

BEGIN;

ALTER TABLE public.answer_l
    DROP CONSTRAINT IF EXISTS answer_l_changed_by_user_id_fkey;

CREATE TABLE IF NOT EXISTS public.answer_participant (
    id_answer integer NOT NULL,
    id_user integer NOT NULL,
    participation_type text NOT NULL,
    date_created timestamp without time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT answer_participant_pkey
        PRIMARY KEY (id_answer, id_user, participation_type),
    CONSTRAINT answer_participant_type_check
        CHECK (participation_type IN ('submitted', 'edited', 'signed', 'legacy')),
    CONSTRAINT answer_participant_id_answer_fkey
        FOREIGN KEY (id_answer)
        REFERENCES public.answer (id_answer)
        ON DELETE CASCADE,
    CONSTRAINT answer_participant_id_user_fkey
        FOREIGN KEY (id_user)
        REFERENCES public.app_user (id_user)
        ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS public.answer_draft_participant (
    id_answer_draft integer NOT NULL,
    id_user integer NOT NULL,
    participation_type text NOT NULL,
    date_created timestamp without time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT answer_draft_participant_pkey
        PRIMARY KEY (id_answer_draft, id_user, participation_type),
    CONSTRAINT answer_draft_participant_type_check
        CHECK (participation_type IN ('saved', 'signed', 'legacy')),
    CONSTRAINT answer_draft_participant_id_answer_draft_fkey
        FOREIGN KEY (id_answer_draft)
        REFERENCES public.answer_draft (id_answer_draft)
        ON DELETE CASCADE,
    CONSTRAINT answer_draft_participant_id_user_fkey
        FOREIGN KEY (id_user)
        REFERENCES public.app_user (id_user)
        ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS idx_answer_participant_id_user
    ON public.answer_participant (id_user);

CREATE INDEX IF NOT EXISTS idx_answer_draft_participant_id_user
    ON public.answer_draft_participant (id_user);

INSERT INTO public.answer_participant (id_answer, id_user, participation_type)
SELECT answer.id_answer, app_user.id_user, 'legacy'
FROM public.answer answer
INNER JOIN public.organization_survey assignment
    ON assignment.id_organization_survey = answer.id_organization_survey
INNER JOIN public.app_user app_user
    ON app_user.id_organization = assignment.id_organization
ON CONFLICT DO NOTHING;

INSERT INTO public.answer_draft_participant (id_answer_draft, id_user, participation_type)
SELECT draft.id_answer_draft, app_user.id_user, 'legacy'
FROM public.answer_draft draft
INNER JOIN public.organization_survey assignment
    ON assignment.id_organization_survey = draft.id_organization_survey
INNER JOIN public.app_user app_user
    ON app_user.id_organization = assignment.id_organization
ON CONFLICT DO NOTHING;

INSERT INTO public.schema_migrations (version, name)
VALUES ('037', 'store_answer_participants')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 037_store_answer_participants
\endif
