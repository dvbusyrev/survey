\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW()
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '046') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 046_store_answer_submitter

BEGIN;

ALTER TABLE public.answer
    ADD COLUMN IF NOT EXISTS id_user integer;

ALTER TABLE public.answer_l
    ADD COLUMN IF NOT EXISTS id_user integer;

CREATE TEMP TABLE migration_046_answer_audit_trigger_state
ON COMMIT DROP
AS
SELECT trigger_definition.tgenabled
FROM pg_trigger trigger_definition
WHERE trigger_definition.tgrelid = 'public.answer'::regclass
  AND trigger_definition.tgname = 'trg_answer_crud_audit'
  AND NOT trigger_definition.tgisinternal;

DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM migration_046_answer_audit_trigger_state
        WHERE tgenabled <> 'D'
    ) THEN
        ALTER TABLE public.answer DISABLE TRIGGER trg_answer_crud_audit;
    END IF;
END;
$migration$;

DO $migration$
BEGIN
    IF to_regclass('public.answer_participant') IS NOT NULL THEN
        WITH ranked_participant AS (
            SELECT
                participant.id_answer,
                participant.id_user,
                ROW_NUMBER() OVER (
                    PARTITION BY participant.id_answer
                    ORDER BY
                        CASE participant.participation_type
                            WHEN 'submitted' THEN 0
                            WHEN 'signed' THEN 1
                            WHEN 'legacy' THEN 2
                            ELSE 3
                        END,
                        participant.date_created DESC,
                        participant.id_user
                ) AS participant_rank
            FROM public.answer_participant participant
        )
        UPDATE public.answer answer
        SET id_user = participant.id_user
        FROM ranked_participant participant
        WHERE participant.id_answer = answer.id_answer
          AND participant.participant_rank = 1
          AND answer.id_user IS NULL;
    END IF;
END;
$migration$;

WITH fallback_submitter AS (
    SELECT
        answer.id_answer,
        (
            SELECT app_user.id_user
            FROM public.app_user app_user
            WHERE app_user.id_organization = assignment.id_organization
            ORDER BY
                CASE WHEN app_user.role = 'user' THEN 0 ELSE 1 END,
                app_user.id_user
            LIMIT 1
        ) AS id_user
    FROM public.answer answer
    INNER JOIN public.organization_survey assignment
        ON assignment.id_organization_survey = answer.id_organization_survey
    WHERE answer.id_user IS NULL
)
UPDATE public.answer answer
SET id_user = fallback.id_user
FROM fallback_submitter fallback
WHERE fallback.id_answer = answer.id_answer
  AND fallback.id_user IS NOT NULL;

DO $migration$
DECLARE
    missing_submitter_count integer;
BEGIN
    SELECT COUNT(*)
    INTO missing_submitter_count
    FROM public.answer
    WHERE id_user IS NULL;

    IF missing_submitter_count > 0 THEN
        RAISE EXCEPTION
            'Нельзя завершить миграцию 046: для % ответов не удалось определить отправителя.',
            missing_submitter_count;
    END IF;
END;
$migration$;

UPDATE public.answer_l answer_audit
SET id_user = answer.id_user
FROM public.answer answer
WHERE answer.id_answer = answer_audit.id_answer
  AND answer_audit.id_user IS NULL;

ALTER TABLE public.answer
    DROP CONSTRAINT IF EXISTS answer_id_user_fkey;

ALTER TABLE public.answer
    ALTER COLUMN id_user SET NOT NULL,
    ADD CONSTRAINT answer_id_user_fkey
        FOREIGN KEY (id_user)
        REFERENCES public.app_user (id_user)
        ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS idx_answer_id_user
    ON public.answer (id_user);

DROP TABLE IF EXISTS public.answer_draft_participant;
DROP TABLE IF EXISTS public.answer_participant;

DO $migration$
DECLARE
    previous_trigger_state "char";
BEGIN
    SELECT tgenabled
    INTO previous_trigger_state
    FROM migration_046_answer_audit_trigger_state
    LIMIT 1;

    CASE previous_trigger_state
        WHEN 'O' THEN ALTER TABLE public.answer ENABLE TRIGGER trg_answer_crud_audit;
        WHEN 'A' THEN ALTER TABLE public.answer ENABLE ALWAYS TRIGGER trg_answer_crud_audit;
        WHEN 'R' THEN ALTER TABLE public.answer ENABLE REPLICA TRIGGER trg_answer_crud_audit;
        ELSE NULL;
    END CASE;
END;
$migration$;

INSERT INTO public.schema_migrations (version, name)
VALUES ('046', 'store_answer_submitter')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 046_store_answer_submitter
\endif
