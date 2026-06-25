#!/usr/bin/env bash
set -euo pipefail

: "${SURVEY_BACKUP_DIRECTORY:?Set SURVEY_BACKUP_DIRECTORY to the encrypted backup directory.}"
: "${SURVEY_BACKUP_ENCRYPTION_PASSPHRASE_FILE:?Set SURVEY_BACKUP_ENCRYPTION_PASSPHRASE_FILE to a protected GPG passphrase file.}"
: "${SURVEY_RESTORE_VERIFY_CONNECTION:?Set SURVEY_RESTORE_VERIFY_CONNECTION to an isolated verification database.}"
: "${SURVEY_RESTORE_VERIFY_ADMIN_CONNECTION:?Set SURVEY_RESTORE_VERIFY_ADMIN_CONNECTION to a maintenance PostgreSQL database.}"
: "${SURVEY_RESTORE_VERIFY_DATABASE:?Set SURVEY_RESTORE_VERIFY_DATABASE to the isolated verification database name.}"

backup_dir="$SURVEY_BACKUP_DIRECTORY"
passphrase_file="$SURVEY_BACKUP_ENCRYPTION_PASSPHRASE_FILE"
verify_database="$SURVEY_RESTORE_VERIFY_DATABASE"

require_command() {
    command -v "$1" >/dev/null 2>&1 || {
        printf 'Required command is not available: %s\n' "$1" >&2
        exit 1
    }
}

if [[ ! "$verify_database" =~ ^[A-Za-z0-9_]+$ ]] || [[ "$verify_database" != *restore_verify* ]]; then
    printf 'Refusing to reset database "%s": use letters, digits, underscores and include "restore_verify".\n' "$verify_database" >&2
    exit 1
fi

if [[ ! -r "$passphrase_file" ]]; then
    printf 'GPG passphrase file is not readable: %s\n' "$passphrase_file" >&2
    exit 1
fi

require_command createdb
require_command dropdb
require_command gpg
require_command pg_restore
require_command psql

latest_backup="$(find "$backup_dir" -maxdepth 1 -type f -name 'survey_*.dump.gpg' -print | sort | tail -n 1)"
if [[ -z "$latest_backup" ]]; then
    printf 'No encrypted backup found in %s.\n' "$backup_dir" >&2
    exit 1
fi

cleanup() {
    dropdb \
        --if-exists \
        --maintenance-db="$SURVEY_RESTORE_VERIFY_ADMIN_CONNECTION" \
        "$verify_database" >/dev/null 2>&1 || true
}
trap cleanup EXIT

dropdb \
    --if-exists \
    --maintenance-db="$SURVEY_RESTORE_VERIFY_ADMIN_CONNECTION" \
    "$verify_database"
createdb \
    --maintenance-db="$SURVEY_RESTORE_VERIFY_ADMIN_CONNECTION" \
    "$verify_database"

gpg \
    --batch \
    --yes \
    --pinentry-mode loopback \
    --passphrase-file "$passphrase_file" \
    --decrypt "$latest_backup" \
    | pg_restore \
        --exit-on-error \
        --no-owner \
        --no-privileges \
        --dbname="$SURVEY_RESTORE_VERIFY_CONNECTION"

has_migrations_table="$(psql \
    "$SURVEY_RESTORE_VERIFY_CONNECTION" \
    --set ON_ERROR_STOP=1 \
    --tuples-only \
    --no-align \
    --command "SELECT to_regclass('public.schema_migrations') IS NOT NULL;")"

if [[ "$has_migrations_table" != "t" ]]; then
    printf 'Restore verification failed: public.schema_migrations was not restored.\n' >&2
    exit 1
fi

latest_migration="$(psql \
    "$SURVEY_RESTORE_VERIFY_CONNECTION" \
    --set ON_ERROR_STOP=1 \
    --tuples-only \
    --no-align \
    --command 'SELECT version FROM public.schema_migrations ORDER BY version DESC LIMIT 1;')"

if [[ -z "$latest_migration" ]]; then
    printf 'Restore verification failed: no migrations were restored.\n' >&2
    exit 1
fi

printf 'Restore verified for %s from %s (schema migration %s).\n' \
    "$verify_database" \
    "$latest_backup" \
    "$latest_migration"
