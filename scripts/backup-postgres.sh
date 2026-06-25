#!/usr/bin/env bash
set -euo pipefail

: "${SURVEY_BACKUP_CONNECTION:?Set SURVEY_BACKUP_CONNECTION to the PostgreSQL connection string.}"
: "${SURVEY_BACKUP_ENCRYPTION_PASSPHRASE_FILE:?Set SURVEY_BACKUP_ENCRYPTION_PASSPHRASE_FILE to a protected GPG passphrase file.}"

backup_dir="${SURVEY_BACKUP_DIRECTORY:-./backups}"
retention_days="${SURVEY_BACKUP_RETENTION_DAYS:-30}"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
passphrase_file="$SURVEY_BACKUP_ENCRYPTION_PASSPHRASE_FILE"

require_command() {
    command -v "$1" >/dev/null 2>&1 || {
        printf 'Required command is not available: %s\n' "$1" >&2
        exit 1
    }
}

if ! [[ "$retention_days" =~ ^[1-9][0-9]*$ ]]; then
    printf 'SURVEY_BACKUP_RETENTION_DAYS must be a positive integer.\n' >&2
    exit 1
fi

if [[ ! -r "$passphrase_file" ]]; then
    printf 'GPG passphrase file is not readable: %s\n' "$passphrase_file" >&2
    exit 1
fi

require_command pg_dump
require_command gpg

umask 077
mkdir -p "$backup_dir"

backup_file="$backup_dir/survey_${timestamp}.dump.gpg"
temporary_file="$(mktemp "$backup_dir/.survey_${timestamp}.XXXXXX.dump.gpg")"
trap 'rm -f "$temporary_file"' EXIT

pg_dump \
    --format=custom \
    --no-owner \
    --no-privileges \
    --dbname="$SURVEY_BACKUP_CONNECTION" \
    | gpg \
        --batch \
        --yes \
        --pinentry-mode loopback \
        --passphrase-file "$passphrase_file" \
        --symmetric \
        --cipher-algo AES256 \
        --output "$temporary_file"

test -s "$temporary_file"
mv "$temporary_file" "$backup_file"
trap - EXIT

while IFS= read -r -d '' expired_backup; do
    rm -f "$expired_backup"
    printf 'Expired backup removed: %s\n' "$expired_backup"
done < <(find "$backup_dir" -maxdepth 1 -type f -name 'survey_*.dump.gpg' -mtime "+$retention_days" -print0)

printf 'Encrypted backup created: %s\n' "$backup_file"
