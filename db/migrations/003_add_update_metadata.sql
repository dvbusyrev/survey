\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '003') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 003_add_update_metadata

BEGIN;

CREATE OR REPLACE FUNCTION public.set_update_metadata()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.date_update = NOW();

    IF current_setting('app.current_user_id', true) IS NOT NULL
        AND current_setting('app.current_user_id', true) <> ''
    THEN
        NEW.user_update = current_setting('app.current_user_id', true)::integer;
    END IF;

    RETURN NEW;
END;
$function$;

DO $$
DECLARE
    table_record record;
    trigger_name text;
BEGIN
    FOR table_record IN
        SELECT table_schema, table_name
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_type = 'BASE TABLE'
          AND table_name NOT LIKE '%\_l' ESCAPE '\'
    LOOP
        EXECUTE format(
            'ALTER TABLE %I.%I ADD COLUMN IF NOT EXISTS date_update timestamp without time zone NOT NULL DEFAULT NOW()',
            table_record.table_schema,
            table_record.table_name
        );

        EXECUTE format(
            'ALTER TABLE %I.%I ADD COLUMN IF NOT EXISTS user_update integer',
            table_record.table_schema,
            table_record.table_name
        );

        trigger_name := format('trg_%s_set_update_metadata', table_record.table_name);

        EXECUTE format(
            'DROP TRIGGER IF EXISTS %I ON %I.%I',
            trigger_name,
            table_record.table_schema,
            table_record.table_name
        );

        EXECUTE format(
            'CREATE TRIGGER %I BEFORE INSERT OR UPDATE ON %I.%I FOR EACH ROW EXECUTE FUNCTION public.set_update_metadata()',
            trigger_name,
            table_record.table_schema,
            table_record.table_name
        );
    END LOOP;
END;
$$;

INSERT INTO public.schema_migrations (version, name)
VALUES ('003', 'add_update_metadata')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 003_add_update_metadata
\endif
