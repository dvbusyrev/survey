#!/usr/bin/env bash
set -euo pipefail

: "${SURVEY_EXPLAIN_CONNECTION:?Set SURVEY_EXPLAIN_CONNECTION to an isolated PostgreSQL connection string.}"
: "${SURVEY_EXPLAIN_DATABASE:?Set SURVEY_EXPLAIN_DATABASE to the isolated database name.}"

psql_bin="${PSQL_BIN:-psql}"
plan_limit="${SURVEY_EXPLAIN_LIMIT:-10}"
database_name="$(printf '%s' "$SURVEY_EXPLAIN_DATABASE" | tr '[:upper:]' '[:lower:]')"
connection_arguments=("$SURVEY_EXPLAIN_CONNECTION")
connection_password=""
connection_ssl_mode=""

if [[ ! "$SURVEY_EXPLAIN_DATABASE" =~ ^[A-Za-z0-9_]+$ ]] \
    || [[ ! "$database_name" =~ (rehearsal|perf|benchmark|test) ]]; then
  printf 'Refusing to analyze database "%s": use an isolated name containing rehearsal, perf, benchmark, or test.\n' \
    "$SURVEY_EXPLAIN_DATABASE" >&2
  exit 1
fi

if [[ ! "$plan_limit" =~ ^[1-9][0-9]*$ ]] || (( plan_limit > 1000 )); then
  printf 'SURVEY_EXPLAIN_LIMIT must be an integer between 1 and 1000.\n' >&2
  exit 1
fi

if [[ "$SURVEY_EXPLAIN_CONNECTION" == *";"* && "$SURVEY_EXPLAIN_CONNECTION" == *"="* ]]; then
  connection_host=""
  connection_port=""
  connection_database=""
  connection_username=""

  IFS=';' read -r -a connection_parts <<< "$SURVEY_EXPLAIN_CONNECTION"
  for connection_part in "${connection_parts[@]}"; do
    connection_key="${connection_part%%=*}"
    connection_value="${connection_part#*=}"
    connection_key="$(printf '%s' "$connection_key" | tr -d '[:space:]' | tr '[:upper:]' '[:lower:]')"

    case "$connection_key" in
      host) connection_host="$connection_value" ;;
      port) connection_port="$connection_value" ;;
      database|initialcatalog) connection_database="$connection_value" ;;
      username|user|userid) connection_username="$connection_value" ;;
      password) connection_password="$connection_value" ;;
      sslmode) connection_ssl_mode="$connection_value" ;;
    esac
  done

  if [[ -z "$connection_host" || -z "$connection_database" ]]; then
    printf 'SURVEY_EXPLAIN_CONNECTION must include Host and Database when using .NET connection-string syntax.\n' >&2
    exit 1
  fi

  connection_arguments=(--host "$connection_host" --dbname "$connection_database")
  [[ -n "$connection_port" ]] && connection_arguments+=(--port "$connection_port")
  [[ -n "$connection_username" ]] && connection_arguments+=(--username "$connection_username")
fi

run_psql() {
  if [[ -n "$connection_password" || -n "$connection_ssl_mode" ]]; then
    PGPASSWORD="$connection_password" PGSSLMODE="$connection_ssl_mode" \
      "$psql_bin" "${connection_arguments[@]}" "$@"
  else
    "$psql_bin" "${connection_arguments[@]}" "$@"
  fi
}

actual_database="$(run_psql --tuples-only --no-align --command 'SELECT current_database();')"
actual_database="${actual_database//$'\n'/}"

if [[ "$actual_database" != "$SURVEY_EXPLAIN_DATABASE" ]]; then
  printf 'Refusing to analyze "%s": connection is currently using "%s".\n' \
    "$SURVEY_EXPLAIN_DATABASE" "$actual_database" >&2
  exit 1
fi

run_psql \
  --set ON_ERROR_STOP=1 \
  --set "plan_limit=$plan_limit" \
  --file db/performance/explain_read_paths.sql
