\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '001') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 001_unified_schema

BEGIN;

\ir 001_current_schema.sql

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
    (15, 'Friday', 'Пятница', 3)
ON CONFLICT (id_day) DO UPDATE
SET
    en_name_day = EXCLUDED.en_name_day,
    rus_name_day = EXCLUDED.rus_name_day,
    week_number = EXCLUDED.week_number;

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
    NULL,
    false
)
ON CONFLICT (id_config) DO NOTHING;

INSERT INTO public.schema_migrations (version, name)
VALUES
    ('001', 'unified_schema'),
    ('002', 'repair_survey_foreign_keys'),
    ('003', 'add_update_metadata'),
    ('004', 'add_organization_short_name'),
    ('005', 'add_organization_survey_schedule'),
    ('006', 'move_organization_survey_schedule_sync_to_triggers'),
    ('007', 'rename_schedule_dates_and_remove_legacy_columns'),
    ('008', 'convert_dates_and_rename_organization_id'),
    ('009', 'derive_survey_schedule_from_assignments'),
    ('010', 'add_email_template_storage'),
    ('011', 'add_email_template_audit_log'),
    ('012', 'add_survey_auto_creation'),
    ('013', 'transform_survey_auto_creation_config'),
    ('014', 'remove_auto_creation_config_metadata'),
    ('015', 'link_answers_to_organization_survey'),
    ('016', 'rename_config_tables'),
    ('017', 'rename_app_user_credentials'),
    ('018', 'add_audit_log_current_tables'),
    ('019', 'store_audit_old_new_rows'),
    ('020', 'limit_auto_creation_schedule_options'),
    ('021', 'allow_empty_auto_creation_period')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 001_unified_schema
\endif
