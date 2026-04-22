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

Each migration records its version in `public.schema_migrations` and is skipped on the next run.

Migration sources:

- `001_unified_schema` uses `recovery/reconstruct_schema.sql`
- `002_repair_survey_foreign_keys` uses `recovery/repair_live_constraints.sql`
- `003_add_update_metadata` uses `recovery/update_metadata_support.sql`
- `004_add_organization_short_name` adds `organization.organization_short_name`
- `005_add_organization_survey_schedule` adds assignment-level schedule fields to `organization_survey`
- `006_move_organization_survey_schedule_sync_to_triggers` moves schedule sync logic into DB triggers and removes legacy schedule columns
- `007_rename_schedule_dates_and_remove_legacy_columns` renames survey schedule columns to `date_begin/date_end` and removes unused legacy columns
- `008_convert_dates_and_rename_organization_id` converts all `date_begin/date_end` schedules to `date`, removes `survey_question.created_at`, and renames `organization_id` to `id_organization`
- `009_derive_survey_schedule_from_assignments` removes persisted schedule columns from `survey`, drops obsolete schedule sync trigger infrastructure, and exposes survey dates through `MIN(date_begin)` / `MAX(date_end)` from `organization_survey`
