\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '013') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 013_transform_survey_auto_creation_config

BEGIN;

CREATE TABLE IF NOT EXISTS public.week_day (
    id_day integer PRIMARY KEY,
    en_name_day text NOT NULL,
    rus_name_day text NOT NULL,
    week_number integer NOT NULL,
    CONSTRAINT ck_week_day_week_number CHECK (week_number BETWEEN 1 AND 4),
    CONSTRAINT ck_week_day_en_name CHECK (en_name_day IN ('Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday')),
    CONSTRAINT ux_week_day_name_week UNIQUE (en_name_day, week_number)
);

INSERT INTO public.week_day (id_day, en_name_day, rus_name_day, week_number)
VALUES
    (1, 'Monday', 'Понедельник', 1),
    (2, 'Tuesday', 'Вторник', 1),
    (3, 'Wednesday', 'Среда', 1),
    (4, 'Thursday', 'Четверг', 1),
    (5, 'Friday', 'Пятница', 1),
    (6, 'Monday', 'Понедельник', 2),
    (7, 'Tuesday', 'Вторник', 2),
    (8, 'Wednesday', 'Среда', 2),
    (9, 'Thursday', 'Четверг', 2),
    (10, 'Friday', 'Пятница', 2),
    (11, 'Monday', 'Понедельник', 3),
    (12, 'Tuesday', 'Вторник', 3),
    (13, 'Wednesday', 'Среда', 3),
    (14, 'Thursday', 'Четверг', 3),
    (15, 'Friday', 'Пятница', 3),
    (16, 'Monday', 'Понедельник', 4),
    (17, 'Tuesday', 'Вторник', 4),
    (18, 'Wednesday', 'Среда', 4),
    (19, 'Thursday', 'Четверг', 4),
    (20, 'Friday', 'Пятница', 4)
ON CONFLICT (id_day) DO UPDATE
SET
    en_name_day = EXCLUDED.en_name_day,
    rus_name_day = EXCLUDED.rus_name_day,
    week_number = EXCLUDED.week_number;

CREATE TABLE IF NOT EXISTS public.auto_creation_config (
    id_config integer PRIMARY KEY,
    id_creation_day integer NOT NULL,
    id_begin_day integer NOT NULL,
    working_period integer NOT NULL DEFAULT 8,
    is_enabled boolean NOT NULL DEFAULT false,
    CONSTRAINT fk_auto_creation_config_creation_day
        FOREIGN KEY (id_creation_day)
        REFERENCES public.week_day (id_day),
    CONSTRAINT fk_auto_creation_config_begin_day
        FOREIGN KEY (id_begin_day)
        REFERENCES public.week_day (id_day),
    CONSTRAINT ck_auto_creation_config_working_period CHECK (working_period > 0)
);

DO $$
BEGIN
    IF to_regclass('public.survey_auto_creation_config') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'survey_auto_creation_config'
             AND column_name = 'creation_pattern'
       )
    THEN
        EXECUTE $sql$
            INSERT INTO public.auto_creation_config
            (
                id_config,
                id_creation_day,
                id_begin_day,
                working_period,
                is_enabled
            )
            SELECT
                legacy.id_config,
                COALESCE(creation_day.id_day, 1),
                COALESCE(begin_day.id_day, 1),
                GREATEST(COALESCE(legacy.end_offset_business_days, 8), 1),
                COALESCE(legacy.is_enabled, false)
            FROM public.survey_auto_creation_config legacy
            LEFT JOIN public.week_day creation_day
                ON creation_day.week_number = CASE
                    WHEN legacy.creation_pattern ~ '^[1-4]-[a-z]+$'
                    THEN split_part(legacy.creation_pattern, '-', 1)::integer
                    ELSE 1
                END
               AND lower(creation_day.en_name_day) = CASE
                    WHEN legacy.creation_pattern ~ '^[1-4]-[a-z]+$'
                    THEN lower(split_part(legacy.creation_pattern, '-', 2))
                    ELSE 'monday'
                END
            LEFT JOIN public.week_day begin_day
                ON begin_day.week_number = CASE
                    WHEN legacy.start_pattern ~ '^[1-4]-[a-z]+$'
                    THEN split_part(legacy.start_pattern, '-', 1)::integer
                    ELSE 1
                END
               AND lower(begin_day.en_name_day) = CASE
                    WHEN legacy.start_pattern ~ '^[1-4]-[a-z]+$'
                    THEN lower(split_part(legacy.start_pattern, '-', 2))
                    ELSE 'monday'
                END
            ON CONFLICT (id_config) DO UPDATE
            SET
                id_creation_day = EXCLUDED.id_creation_day,
                id_begin_day = EXCLUDED.id_begin_day,
                working_period = EXCLUDED.working_period,
                is_enabled = EXCLUDED.is_enabled;
        $sql$;
    END IF;
END;
$$;

INSERT INTO public.auto_creation_config
(
    id_config,
    id_creation_day,
    id_begin_day,
    working_period,
    is_enabled
)
VALUES
(
    1,
    1,
    1,
    8,
    false
)
ON CONFLICT (id_config) DO NOTHING;

CREATE TEMP TABLE tmp_survey_auto_creation_config_selection (
    id_config integer NOT NULL,
    id_survey integer NOT NULL
) ON COMMIT DROP;

DO $$
BEGIN
    IF to_regclass('public.survey_auto_creation_config_survey') IS NOT NULL THEN
        INSERT INTO tmp_survey_auto_creation_config_selection (id_config, id_survey)
        SELECT id_config, id_survey
        FROM public.survey_auto_creation_config_survey;
    END IF;

    IF to_regclass('public.survey_auto_creation_config') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'survey_auto_creation_config'
             AND column_name = 'id_survey'
       )
    THEN
        INSERT INTO tmp_survey_auto_creation_config_selection (id_config, id_survey)
        SELECT id_config, id_survey
        FROM public.survey_auto_creation_config;
    END IF;
END;
$$;

DROP TABLE IF EXISTS public.survey_auto_creation_config_survey;

DO $$
BEGIN
    IF to_regclass('public.survey_auto_creation_config') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'survey_auto_creation_config'
             AND column_name = 'creation_pattern'
       )
    THEN
        DROP TABLE public.survey_auto_creation_config;
    END IF;
END;
$$;

CREATE TABLE IF NOT EXISTS public.survey_auto_creation_config (
    id_config integer NOT NULL,
    id_survey integer NOT NULL,
    CONSTRAINT pk_survey_auto_creation_config PRIMARY KEY (id_config, id_survey),
    CONSTRAINT fk_survey_auto_creation_config_config
        FOREIGN KEY (id_config)
        REFERENCES public.auto_creation_config (id_config)
        ON DELETE CASCADE,
    CONSTRAINT fk_survey_auto_creation_config_survey
        FOREIGN KEY (id_survey)
        REFERENCES public.survey (id_survey)
        ON DELETE CASCADE
);

INSERT INTO public.survey_auto_creation_config (id_config, id_survey)
SELECT DISTINCT selection.id_config, selection.id_survey
FROM tmp_survey_auto_creation_config_selection selection
INNER JOIN public.auto_creation_config config
    ON config.id_config = selection.id_config
INNER JOIN public.survey survey
    ON survey.id_survey = selection.id_survey
WHERE NOT EXISTS (
    SELECT 1
    FROM public.survey_auto_creation_config existing
    WHERE existing.id_config = selection.id_config
      AND existing.id_survey = selection.id_survey
);

CREATE TABLE IF NOT EXISTS public.l_survey_auto_creation_config (
    id_audit bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    operation text NOT NULL,
    changed_at timestamp without time zone NOT NULL DEFAULT NOW(),
    changed_by_user_id integer,
    record_pk jsonb NOT NULL,
    row_data jsonb NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_l_survey_auto_creation_config_changed_at
    ON public.l_survey_auto_creation_config (changed_at DESC);

CREATE INDEX IF NOT EXISTS idx_l_survey_auto_creation_config_record_pk
    ON public.l_survey_auto_creation_config USING gin (record_pk);

CREATE OR REPLACE FUNCTION public.write_survey_auto_creation_config_audit()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    changed_by integer;
    audit_record_pk jsonb;
    audit_row_data jsonb;
BEGIN
    IF current_setting('app.current_user_id', true) IS NOT NULL
        AND current_setting('app.current_user_id', true) <> ''
    THEN
        changed_by := current_setting('app.current_user_id', true)::integer;
    END IF;

    IF TG_OP = 'DELETE' THEN
        audit_record_pk := jsonb_build_object('id_config', OLD.id_config, 'id_survey', OLD.id_survey);
        audit_row_data := to_jsonb(OLD);
    ELSE
        audit_record_pk := jsonb_build_object('id_config', NEW.id_config, 'id_survey', NEW.id_survey);
        audit_row_data := to_jsonb(NEW);
    END IF;

    INSERT INTO public.l_survey_auto_creation_config
    (
        operation,
        changed_by_user_id,
        record_pk,
        row_data
    )
    VALUES
    (
        TG_OP,
        changed_by,
        audit_record_pk,
        audit_row_data
    );

    RETURN COALESCE(NEW, OLD);
END;
$function$;

DROP TRIGGER IF EXISTS trg_survey_auto_creation_config_audit ON public.survey_auto_creation_config;
CREATE TRIGGER trg_survey_auto_creation_config_audit
AFTER INSERT OR UPDATE OR DELETE ON public.survey_auto_creation_config
FOR EACH ROW
EXECUTE FUNCTION public.write_survey_auto_creation_config_audit();

INSERT INTO public.schema_migrations (version, name)
VALUES ('013', 'transform_survey_auto_creation_config')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 013_transform_survey_auto_creation_config
\endif
