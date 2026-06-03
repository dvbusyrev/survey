# Database Migrations

This project no longer mutates the schema from application startup or request-time connection code.

Apply schema changes explicitly with PostgreSQL tooling:

```sh
/opt/homebrew/opt/postgresql@18/bin/psql -d survey_recovered -f db/migrations/000_apply_all.sql
```

What this does:

- creates `public.schema_migrations` if needed
- applies `001_unified_schema`
- applies `002_repair_survey_foreign_keys`
- applies `003_add_update_metadata`
- applies `004_add_organization_short_name`
- applies `005_add_organization_survey_schedule`
- applies `006_move_organization_survey_schedule_sync_to_triggers`
- applies `007_rename_schedule_dates_and_remove_legacy_columns`
- applies `008_convert_dates_and_rename_organization_id`
- applies `009_derive_survey_schedule_from_assignments`
- applies `010_add_email_template_storage`
- applies `011_add_email_template_audit_log`
- applies `012_add_survey_auto_creation`
- applies `013_transform_survey_auto_creation_config`
- applies `014_remove_auto_creation_config_metadata`
- applies `015_link_answers_to_organization_survey`
- applies `016_rename_config_tables`
- applies `017_rename_app_user_credentials`
- applies `018_add_audit_log_current_tables`
- applies `019_store_audit_old_new_rows`
- applies `020_limit_auto_creation_schedule_options`
- applies `021_allow_empty_auto_creation_period`
- applies `022_store_signed_answer_content`
- applies `023_add_theme_config`
- applies `024_add_theme_palette_controls`

Each migration records its version in `public.schema_migrations` and is skipped on the next run.

Migration sources:

- `001_unified_schema` builds a portable current-schema baseline from `001_current_schema.sql` and records migrations through `021`
- `002_repair_survey_foreign_keys` is retained as a historical no-op because the repaired constraints are now part of the baseline
- `003_add_update_metadata` adds `date_update/user_update` support without external recovery scripts
- `004_add_organization_short_name` adds `organization.organization_short_name`
- `005_add_organization_survey_schedule` adds assignment-level schedule fields to `organization_survey`
- `006_move_organization_survey_schedule_sync_to_triggers` moves schedule sync logic into DB triggers and removes legacy schedule columns
- `007_rename_schedule_dates_and_remove_legacy_columns` renames survey schedule columns to `date_begin/date_end` and removes unused legacy columns
- `008_convert_dates_and_rename_organization_id` converts all `date_begin/date_end` schedules to `date`, removes `survey_question.created_at`, and renames `organization_id` to `id_organization`
- `009_derive_survey_schedule_from_assignments` removes persisted schedule columns from `survey`, drops obsolete schedule sync trigger infrastructure, and exposes survey dates through `MIN(date_begin)` / `MAX(date_end)` from `organization_survey`
- `010_add_email_template_storage` creates email settings storage
- `011_add_email_template_audit_log` adds email settings audit logging
- `012_add_survey_auto_creation` creates the first auto-creation configuration storage
- `013_transform_survey_auto_creation_config` normalizes auto-creation configuration and seeds `week_day`
- `014_remove_auto_creation_config_metadata` removes obsolete auto-creation metadata columns
- `015_link_answers_to_organization_survey` links answers to `organization_survey`
- `016_rename_config_tables` renames email and auto-creation audit/config tables to final names
- `017_rename_app_user_credentials` renames `app_user` credential columns to `login`, `role`, and `password`
- `018_add_audit_log_current_tables` adds audit logging for current detail/config tables used by chained actions
- `019_store_audit_old_new_rows` stores `OLD` and `NEW` row snapshots directly in audit tables
- `020_limit_auto_creation_schedule_options` limits auto-creation weekday options to the first three weekdays and working period values to 14 days
- `021_allow_empty_auto_creation_period` allows blank auto-creation period values and open-ended survey assignments
- `022_store_signed_answer_content` stores the exact signed PDF bytes so detached signatures are archived with the same content that was signed
- `023_add_theme_config` creates theme settings storage and audit logging for interface appearance
- `024_add_theme_palette_controls` adds configurable shade and tint controls for derived theme colors
