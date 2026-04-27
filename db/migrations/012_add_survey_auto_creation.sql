\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '012') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 012_add_survey_auto_creation

BEGIN;

CREATE OR REPLACE FUNCTION public.set_update_metadata()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.date_update = NOW();

    IF current_setting('app.current_user_id', true) IS NOT NULL
        AND current_setting('app.current_user_id', true) <> ''
    THEN
        NEW.user_update = current_setting('app.current_user_id', true)::integer;
    END IF;

    RETURN NEW;
END;
$function$;

CREATE TABLE IF NOT EXISTS public.survey_auto_creation_config (
    id_config integer PRIMARY KEY,
    creation_pattern text NOT NULL DEFAULT '1-monday',
    start_pattern text NOT NULL DEFAULT '1-monday',
    end_offset_business_days integer NOT NULL DEFAULT 8,
    is_enabled boolean NOT NULL DEFAULT false,
    last_processed_schedule_date date,
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer,
    CONSTRAINT ck_survey_auto_creation_end_offset CHECK (end_offset_business_days BETWEEN 8 AND 20)
);

CREATE TABLE IF NOT EXISTS public.survey_auto_creation_config_survey (
    id_config integer NOT NULL,
    id_survey integer NOT NULL,
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer,
    CONSTRAINT pk_survey_auto_creation_config_survey PRIMARY KEY (id_config, id_survey),
    CONSTRAINT fk_survey_auto_creation_config_survey_config
        FOREIGN KEY (id_config)
        REFERENCES public.survey_auto_creation_config (id_config)
        ON DELETE CASCADE,
    CONSTRAINT fk_survey_auto_creation_config_survey_survey
        FOREIGN KEY (id_survey)
        REFERENCES public.survey (id_survey)
        ON DELETE CASCADE
);

DROP TRIGGER IF EXISTS trg_survey_auto_creation_config_set_update_metadata ON public.survey_auto_creation_config;
CREATE TRIGGER trg_survey_auto_creation_config_set_update_metadata
BEFORE INSERT OR UPDATE ON public.survey_auto_creation_config
FOR EACH ROW
EXECUTE FUNCTION public.set_update_metadata();

DROP TRIGGER IF EXISTS trg_survey_auto_creation_config_survey_set_update_metadata ON public.survey_auto_creation_config_survey;
CREATE TRIGGER trg_survey_auto_creation_config_survey_set_update_metadata
BEFORE INSERT OR UPDATE ON public.survey_auto_creation_config_survey
FOR EACH ROW
EXECUTE FUNCTION public.set_update_metadata();

INSERT INTO public.survey_auto_creation_config
(
    id_config,
    creation_pattern,
    start_pattern,
    end_offset_business_days,
    is_enabled
)
VALUES
(
    1,
    '1-monday',
    '1-monday',
    8,
    false
)
ON CONFLICT (id_config) DO NOTHING;

INSERT INTO public.schema_migrations (version, name)
VALUES ('012', 'add_survey_auto_creation')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 012_add_survey_auto_creation
\endif
