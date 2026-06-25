#!/usr/bin/env bash
set -euo pipefail

: "${SURVEY_SOURCE_CONNECTION:?Set SURVEY_SOURCE_CONNECTION to the source PostgreSQL connection string.}"
: "${SURVEY_REHEARSAL_CONNECTION:?Set SURVEY_REHEARSAL_CONNECTION to an isolated rehearsal database.}"
: "${SURVEY_REHEARSAL_ADMIN_CONNECTION:?Set SURVEY_REHEARSAL_ADMIN_CONNECTION to a maintenance PostgreSQL database.}"
: "${SURVEY_REHEARSAL_DATABASE:?Set SURVEY_REHEARSAL_DATABASE to the isolated database name.}"

if [[ ! "$SURVEY_REHEARSAL_DATABASE" =~ ^[A-Za-z0-9_]+$ ]] || [[ "$SURVEY_REHEARSAL_DATABASE" != *rehearsal* ]]; then
  printf 'Refusing to reset database "%s": use letters, digits, underscores and include "rehearsal".\n' "$SURVEY_REHEARSAL_DATABASE" >&2
  exit 1
fi

backup_file="$(mktemp "${TMPDIR:-/tmp}/survey-rehearsal.XXXXXX.dump")"
trap 'rm -f "$backup_file"' EXIT

pg_dump --format=custom --no-owner --no-privileges --file="$backup_file" "$SURVEY_SOURCE_CONNECTION"
dropdb --if-exists --maintenance-db="$SURVEY_REHEARSAL_ADMIN_CONNECTION" "$SURVEY_REHEARSAL_DATABASE"
createdb --maintenance-db="$SURVEY_REHEARSAL_ADMIN_CONNECTION" "$SURVEY_REHEARSAL_DATABASE"
pg_restore --no-owner --no-privileges --dbname="$SURVEY_REHEARSAL_CONNECTION" "$backup_file"
psql "$SURVEY_REHEARSAL_CONNECTION" --set ON_ERROR_STOP=1 --file db/migrations/000_apply_all.sql
psql "$SURVEY_REHEARSAL_CONNECTION" --tuples-only --no-align --command 'SELECT version FROM public.schema_migrations ORDER BY version DESC LIMIT 1;'
printf 'Migration rehearsal completed for %s.\n' "$SURVEY_REHEARSAL_DATABASE"
