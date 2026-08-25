\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW()
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '043') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 043_remove_redundant_date_update

BEGIN;

DO $migration$
DECLARE
    trigger_record record;
BEGIN
    FOR trigger_record IN
        SELECT
            table_namespace.nspname AS table_schema,
            table_class.relname AS table_name,
            trigger_definition.tgname AS trigger_name
        FROM pg_trigger trigger_definition
        INNER JOIN pg_class table_class
            ON table_class.oid = trigger_definition.tgrelid
        INNER JOIN pg_namespace table_namespace
            ON table_namespace.oid = table_class.relnamespace
        INNER JOIN pg_proc trigger_function
            ON trigger_function.oid = trigger_definition.tgfoid
        INNER JOIN pg_namespace function_namespace
            ON function_namespace.oid = trigger_function.pronamespace
        WHERE NOT trigger_definition.tgisinternal
          AND table_namespace.nspname = 'public'
          AND function_namespace.nspname = 'public'
          AND trigger_function.proname = 'set_update_metadata'
          AND table_class.relname <> 'email_config'
    LOOP
        EXECUTE format(
            'DROP TRIGGER %I ON %I.%I',
            trigger_record.trigger_name,
            trigger_record.table_schema,
            trigger_record.table_name
        );
    END LOOP;
END;
$migration$;

DO $migration$
DECLARE
    table_record record;
BEGIN
    FOR table_record IN
        SELECT column_definition.table_schema, column_definition.table_name
        FROM information_schema.columns column_definition
        INNER JOIN information_schema.tables table_definition
            ON table_definition.table_schema = column_definition.table_schema
           AND table_definition.table_name = column_definition.table_name
        WHERE column_definition.table_schema = 'public'
          AND column_definition.column_name = 'date_update'
          AND table_definition.table_type = 'BASE TABLE'
          AND column_definition.table_name NOT IN ('email_config', 'email_config_l')
    LOOP
        EXECUTE format(
            'ALTER TABLE %I.%I DROP COLUMN date_update',
            table_record.table_schema,
            table_record.table_name
        );
    END LOOP;
END;
$migration$;

INSERT INTO public.schema_migrations (version, name)
VALUES ('043', 'remove_redundant_date_update')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 043_remove_redundant_date_update
\endif
