DO $$
DECLARE
    table_name text;
BEGIN
    FOR table_name IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = 'public'
    LOOP
        EXECUTE format(
            'ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS date_update timestamp without time zone',
            table_name
        );

        EXECUTE format(
            'ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS user_update integer',
            table_name
        );

        EXECUTE format(
            'UPDATE public.%I SET date_update = NOW() WHERE date_update IS NULL',
            table_name
        );

        EXECUTE format(
            'ALTER TABLE public.%I ALTER COLUMN date_update SET DEFAULT NOW()',
            table_name
        );

        EXECUTE format(
            'ALTER TABLE public.%I ALTER COLUMN date_update SET NOT NULL',
            table_name
        );
    END LOOP;
END $$;

CREATE OR REPLACE FUNCTION public.set_update_metadata()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.date_update := NOW();
    NEW.user_update := public.audit_current_user_id();

    RETURN NEW;
END;
$$;

DO $$
DECLARE
    table_name text;
    trigger_name text;
BEGIN
    FOR table_name IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = 'public'
    LOOP
        trigger_name := format('trg_%s_update_metadata', table_name);

        EXECUTE format(
            'DROP TRIGGER IF EXISTS %I ON public.%I',
            trigger_name,
            table_name
        );

        EXECUTE format(
            'CREATE TRIGGER %I
                BEFORE INSERT OR UPDATE ON public.%I
                FOR EACH ROW
                EXECUTE FUNCTION public.set_update_metadata()',
            trigger_name,
            table_name
        );
    END LOOP;
END $$;
