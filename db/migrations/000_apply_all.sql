\set ON_ERROR_STOP on

BEGIN;

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

COMMIT;

\ir 001_unified_schema.sql
\ir 002_repair_survey_foreign_keys.sql
\ir 003_add_update_metadata.sql
\ir 004_add_organization_short_name.sql
\ir 005_add_organization_survey_schedule.sql
\ir 006_move_organization_survey_schedule_sync_to_triggers.sql
\ir 007_rename_schedule_dates_and_remove_legacy_columns.sql
\ir 008_convert_dates_and_rename_organization_id.sql
\ir 009_derive_survey_schedule_from_assignments.sql
\ir 010_add_email_template_storage.sql
\ir 011_add_email_template_audit_log.sql
\ir 012_add_survey_auto_creation.sql
\ir 013_transform_survey_auto_creation_config.sql
\ir 014_remove_auto_creation_config_metadata.sql
\ir 015_link_answers_to_organization_survey.sql
\ir 016_rename_config_tables.sql
\ir 017_rename_app_user_credentials.sql
