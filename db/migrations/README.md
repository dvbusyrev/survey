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

Each migration records its version in `public.schema_migrations` and is skipped on the next run.

Migration sources:

- `001_unified_schema` uses `recovery/reconstruct_schema.sql`
- `002_repair_survey_foreign_keys` uses `recovery/repair_live_constraints.sql`
- `003_add_update_metadata` uses `recovery/update_metadata_support.sql`
