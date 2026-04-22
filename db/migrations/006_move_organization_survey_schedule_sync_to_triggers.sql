\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '006') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 006_move_organization_survey_schedule_sync_to_triggers

BEGIN;

CREATE TABLE IF NOT EXISTS public.organization_survey_schedule_sync (
    organization_id integer NOT NULL,
    id_survey integer NOT NULL,
    sync_date_open boolean NOT NULL DEFAULT true,
    sync_date_close boolean NOT NULL DEFAULT true,
    PRIMARY KEY (organization_id, id_survey),
    CONSTRAINT fk_organization_survey_schedule_sync_assignment
        FOREIGN KEY (organization_id, id_survey)
        REFERENCES public.organization_survey (organization_id, id_survey)
        ON DELETE CASCADE
);

INSERT INTO public.organization_survey_schedule_sync (
    organization_id,
    id_survey,
    sync_date_open,
    sync_date_close
)
SELECT
    os.organization_id,
    os.id_survey,
    (os.date_open = s.date_open) AS sync_date_open,
    NOT COALESCE(os.is_custom_end_date, false) AS sync_date_close
FROM public.organization_survey os
INNER JOIN public.survey s
    ON s.id_survey = os.id_survey
ON CONFLICT (organization_id, id_survey) DO UPDATE
SET
    sync_date_open = EXCLUDED.sync_date_open,
    sync_date_close = EXCLUDED.sync_date_close;

CREATE OR REPLACE FUNCTION public.organization_survey_apply_schedule_defaults()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    survey_schedule RECORD;
BEGIN
    SELECT
        date_open,
        date_close
    INTO survey_schedule
    FROM public.survey
    WHERE id_survey = NEW.id_survey;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Анкета % не найдена.', NEW.id_survey
            USING ERRCODE = '23503';
    END IF;

    IF NEW.date_open IS NULL THEN
        NEW.date_open := survey_schedule.date_open;
    END IF;

    IF NEW.date_close IS NULL THEN
        NEW.date_close := survey_schedule.date_close;
    END IF;

    IF NEW.date_close <= NEW.date_open THEN
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
       AND (OLD.organization_id <> NEW.organization_id OR OLD.id_survey <> NEW.id_survey) THEN
        DELETE FROM public.organization_survey_schedule_sync
        WHERE organization_id = OLD.organization_id
          AND id_survey = OLD.id_survey;
    END IF;

    SELECT
        date_open,
        date_close
    INTO survey_schedule
    FROM public.survey
    WHERE id_survey = NEW.id_survey;

    INSERT INTO public.organization_survey_schedule_sync (
        organization_id,
        id_survey,
        sync_date_open,
        sync_date_close
    )
    VALUES (
        NEW.organization_id,
        NEW.id_survey,
        NEW.date_open = survey_schedule.date_open,
        NEW.date_close = survey_schedule.date_close
    )
    ON CONFLICT (organization_id, id_survey) DO UPDATE
    SET
        sync_date_open = EXCLUDED.sync_date_open,
        sync_date_close = EXCLUDED.sync_date_close;

    IF NEW.date_close > survey_schedule.date_close THEN
        UPDATE public.survey
        SET date_close = NEW.date_close
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
    SET date_open = NEW.date_open
    FROM public.organization_survey_schedule_sync sync
    WHERE sync.organization_id = os.organization_id
      AND sync.id_survey = os.id_survey
      AND sync.id_survey = NEW.id_survey
      AND sync.sync_date_open = true
      AND os.date_open IS DISTINCT FROM NEW.date_open;

    UPDATE public.organization_survey os
    SET date_close = NEW.date_close
    FROM public.organization_survey_schedule_sync sync
    WHERE sync.organization_id = os.organization_id
      AND sync.id_survey = os.id_survey
      AND sync.id_survey = NEW.id_survey
      AND sync.sync_date_close = true
      AND os.date_close IS DISTINCT FROM NEW.date_close;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_organization_survey_apply_schedule_defaults ON public.organization_survey;
CREATE TRIGGER trg_organization_survey_apply_schedule_defaults
BEFORE INSERT OR UPDATE OF id_survey, date_open, date_close
ON public.organization_survey
FOR EACH ROW
EXECUTE FUNCTION public.organization_survey_apply_schedule_defaults();

DROP TRIGGER IF EXISTS trg_organization_survey_track_schedule_sync ON public.organization_survey;
CREATE TRIGGER trg_organization_survey_track_schedule_sync
AFTER INSERT OR UPDATE OF organization_id, id_survey, date_open, date_close
ON public.organization_survey
FOR EACH ROW
EXECUTE FUNCTION public.organization_survey_track_schedule_sync();

DROP TRIGGER IF EXISTS trg_survey_propagate_schedule_to_assignments ON public.survey;
CREATE TRIGGER trg_survey_propagate_schedule_to_assignments
AFTER UPDATE OF date_open, date_close
ON public.survey
FOR EACH ROW
WHEN (OLD.date_open IS DISTINCT FROM NEW.date_open OR OLD.date_close IS DISTINCT FROM NEW.date_close)
EXECUTE FUNCTION public.survey_propagate_schedule_to_assignments();

ALTER TABLE public.organization_survey
    DROP COLUMN IF EXISTS is_custom_end_date,
    DROP COLUMN IF EXISTS extended_until;

INSERT INTO public.schema_migrations (version, name)
VALUES ('006', 'move_organization_survey_schedule_sync_to_triggers')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 006_move_organization_survey_schedule_sync_to_triggers
\endif
