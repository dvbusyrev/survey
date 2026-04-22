\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '008') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 008_convert_dates_and_rename_organization_id

BEGIN;

DROP TRIGGER IF EXISTS trg_organization_survey_apply_schedule_defaults ON public.organization_survey;
DROP TRIGGER IF EXISTS trg_organization_survey_track_schedule_sync ON public.organization_survey;
DROP TRIGGER IF EXISTS trg_survey_propagate_schedule_to_assignments ON public.survey;
DROP TRIGGER IF EXISTS trg_organization_crud_audit ON public.organization;
DROP TRIGGER IF EXISTS trg_organization_survey_crud_audit ON public.organization_survey;

ALTER TABLE public.survey_question
    DROP COLUMN IF EXISTS created_at;

ALTER TABLE public.survey
    ALTER COLUMN date_begin TYPE date USING date_begin::date,
    ALTER COLUMN date_end TYPE date USING date_end::date;

ALTER TABLE public.app_user
    ALTER COLUMN date_begin TYPE date USING date_begin::date,
    ALTER COLUMN date_end TYPE date USING date_end::date;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'organization'
          AND column_name = 'organization_id'
    ) THEN
        EXECUTE 'ALTER TABLE public.organization RENAME COLUMN organization_id TO id_organization';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'app_user'
          AND column_name = 'organization_id'
    ) THEN
        EXECUTE 'ALTER TABLE public.app_user RENAME COLUMN organization_id TO id_organization';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'answer'
          AND column_name = 'organization_id'
    ) THEN
        EXECUTE 'ALTER TABLE public.answer RENAME COLUMN organization_id TO id_organization';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'organization_survey'
          AND column_name = 'organization_id'
    ) THEN
        EXECUTE 'ALTER TABLE public.organization_survey RENAME COLUMN organization_id TO id_organization';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'organization_survey_schedule_sync'
          AND column_name = 'organization_id'
    ) THEN
        EXECUTE 'ALTER TABLE public.organization_survey_schedule_sync RENAME COLUMN organization_id TO id_organization';
    END IF;
END;
$$;

CREATE OR REPLACE FUNCTION public.organization_survey_apply_schedule_defaults()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    survey_schedule RECORD;
BEGIN
    SELECT
        date_begin,
        date_end
    INTO survey_schedule
    FROM public.survey
    WHERE id_survey = NEW.id_survey;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Анкета % не найдена.', NEW.id_survey
            USING ERRCODE = '23503';
    END IF;

    IF NEW.date_begin IS NULL THEN
        NEW.date_begin := survey_schedule.date_begin;
    END IF;

    IF NEW.date_end IS NULL THEN
        NEW.date_end := survey_schedule.date_end;
    END IF;

    IF NEW.date_end <= NEW.date_begin THEN
        RAISE EXCEPTION 'Дата конца назначения должна быть позже даты начала.'
            USING ERRCODE = '22007';
    END IF;

    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.organization_survey_track_schedule_sync()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    survey_schedule RECORD;
BEGIN
    IF TG_OP = 'UPDATE'
       AND (OLD.id_organization <> NEW.id_organization OR OLD.id_survey <> NEW.id_survey) THEN
        DELETE FROM public.organization_survey_schedule_sync
        WHERE id_organization = OLD.id_organization
          AND id_survey = OLD.id_survey;
    END IF;

    SELECT
        date_begin,
        date_end
    INTO survey_schedule
    FROM public.survey
    WHERE id_survey = NEW.id_survey;

    INSERT INTO public.organization_survey_schedule_sync (
        id_organization,
        id_survey,
        sync_date_begin,
        sync_date_end
    )
    VALUES (
        NEW.id_organization,
        NEW.id_survey,
        NEW.date_begin = survey_schedule.date_begin,
        NEW.date_end = survey_schedule.date_end
    )
    ON CONFLICT (id_organization, id_survey) DO UPDATE
    SET
        sync_date_begin = EXCLUDED.sync_date_begin,
        sync_date_end = EXCLUDED.sync_date_end;

    IF NEW.date_end > survey_schedule.date_end THEN
        UPDATE public.survey
        SET date_end = NEW.date_end
        WHERE id_survey = NEW.id_survey;
    END IF;

    RETURN NULL;
END;
$$;

CREATE OR REPLACE FUNCTION public.survey_propagate_schedule_to_assignments()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF pg_trigger_depth() > 1 THEN
        RETURN NEW;
    END IF;

    UPDATE public.organization_survey os
    SET date_begin = NEW.date_begin
    FROM public.organization_survey_schedule_sync sync
    WHERE sync.id_organization = os.id_organization
      AND sync.id_survey = os.id_survey
      AND sync.id_survey = NEW.id_survey
      AND sync.sync_date_begin = true
      AND os.date_begin IS DISTINCT FROM NEW.date_begin;

    UPDATE public.organization_survey os
    SET date_end = NEW.date_end
    FROM public.organization_survey_schedule_sync sync
    WHERE sync.id_organization = os.id_organization
      AND sync.id_survey = os.id_survey
      AND sync.id_survey = NEW.id_survey
      AND sync.sync_date_end = true
      AND os.date_end IS DISTINCT FROM NEW.date_end;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_organization_crud_audit
AFTER INSERT OR DELETE OR UPDATE ON public.organization
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_organization');

CREATE TRIGGER trg_organization_survey_crud_audit
AFTER INSERT OR DELETE OR UPDATE ON public.organization_survey
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_organization', 'id_survey');

CREATE TRIGGER trg_organization_survey_apply_schedule_defaults
BEFORE INSERT OR UPDATE OF id_survey, date_begin, date_end
ON public.organization_survey
FOR EACH ROW
EXECUTE FUNCTION public.organization_survey_apply_schedule_defaults();

CREATE TRIGGER trg_organization_survey_track_schedule_sync
AFTER INSERT OR UPDATE OF id_organization, id_survey, date_begin, date_end
ON public.organization_survey
FOR EACH ROW
EXECUTE FUNCTION public.organization_survey_track_schedule_sync();

CREATE TRIGGER trg_survey_propagate_schedule_to_assignments
AFTER UPDATE OF date_begin, date_end
ON public.survey
FOR EACH ROW
WHEN (OLD.date_begin IS DISTINCT FROM NEW.date_begin OR OLD.date_end IS DISTINCT FROM NEW.date_end)
EXECUTE FUNCTION public.survey_propagate_schedule_to_assignments();

INSERT INTO public.schema_migrations (version, name)
VALUES ('008', 'convert_dates_and_rename_organization_id')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 008_convert_dates_and_rename_organization_id
\endif
