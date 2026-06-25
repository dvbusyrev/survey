#!/usr/bin/env bash
set -euo pipefail

: "${SURVEY_MIGRATION_CONNECTION:?Set SURVEY_MIGRATION_CONNECTION to a libpq PostgreSQL connection string.}"

target_version="${1:?Pass the inclusive three-digit migration version.}"
psql_bin="${PSQL_BIN:-psql}"

if [[ ! "$target_version" =~ ^[0-9]{3}$ ]]; then
  printf 'Target migration version must contain exactly three digits.\n' >&2
  exit 1
fi

for migration in db/migrations/[0-9][0-9][0-9]_*.sql; do
  filename="${migration##*/}"
  version="${filename%%_*}"

  if [[ "$version" == "000" ]]; then
    continue
  fi

  if [[ "$version" > "$target_version" ]]; then
    break
  fi

  "$psql_bin" "$SURVEY_MIGRATION_CONNECTION" --set ON_ERROR_STOP=1 --file "$migration"
done
