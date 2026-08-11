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
\ir 018_add_audit_log_current_tables.sql
\ir 019_store_audit_old_new_rows.sql
\ir 020_limit_auto_creation_schedule_options.sql
\ir 021_allow_empty_auto_creation_period.sql
\ir 022_store_signed_answer_content.sql
\ir 023_add_theme_config.sql
\ir 024_add_theme_palette_controls.sql
\ir 025_add_answer_drafts.sql
\ir 026_rebuild_audit_tables_as_structured_snapshots.sql
\ir 027_store_theme_background_image_blob.sql
\ir 028_remove_legacy_theme_columns.sql
\ir 029_redesign_auto_creation_reporting_period.sql
\ir 030_reconcile_schema_consistency.sql
\ir 031_require_user_organization.sql
\ir 032_allow_arbitrary_auto_creation_periods.sql
\ir 033_remove_obsolete_week_day.sql
\ir 034_repair_audit_id_generators.sql
\ir 035_disallow_comments_for_top_rating.sql
\ir 036_protect_referenced_records_from_deletion.sql
\ir 037_store_answer_participants.sql
\ir 038_repair_answer_participants.sql
\ir 039_restore_survey_base_schedule.sql
\ir 040_protect_smtp_password_storage.sql
