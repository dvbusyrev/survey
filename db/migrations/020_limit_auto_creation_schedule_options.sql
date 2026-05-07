\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '020') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 020_limit_auto_creation_schedule_options

BEGIN;

UPDATE public.auto_creation_config
SET working_period = 14
WHERE working_period > 14;

UPDATE public.auto_creation_config c
SET id_creation_day = replacement_day.id_day
FROM public.week_day current_day
INNER JOIN public.week_day replacement_day
    ON lower(replacement_day.en_name_day) = lower(current_day.en_name_day)
   AND replacement_day.week_number = 3
WHERE c.id_creation_day = current_day.id_day
  AND current_day.week_number > 3;

UPDATE public.auto_creation_config c
SET id_begin_day = replacement_day.id_day
FROM public.week_day current_day
INNER JOIN public.week_day replacement_day
    ON lower(replacement_day.en_name_day) = lower(current_day.en_name_day)
   AND replacement_day.week_number = 3
WHERE c.id_begin_day = current_day.id_day
  AND current_day.week_number > 3;

DELETE FROM public.week_day
WHERE week_number > 3;

ALTER TABLE public.auto_creation_config
    DROP CONSTRAINT IF EXISTS ck_auto_creation_config_working_period;

ALTER TABLE public.auto_creation_config
    ADD CONSTRAINT ck_auto_creation_config_working_period
    CHECK (working_period BETWEEN 1 AND 14);

ALTER TABLE public.week_day
    DROP CONSTRAINT IF EXISTS ck_week_day_week_number;

ALTER TABLE public.week_day
    ADD CONSTRAINT ck_week_day_week_number
    CHECK (week_number BETWEEN 1 AND 3);

INSERT INTO public.schema_migrations (version, name)
VALUES ('020', 'limit_auto_creation_schedule_options')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 020_limit_auto_creation_schedule_options
\endif
