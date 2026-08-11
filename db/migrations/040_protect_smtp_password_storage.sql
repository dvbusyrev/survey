\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '040') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 040_protect_smtp_password_storage

BEGIN;

UPDATE public.email_config_l
SET smtp_password = '[REDACTED]'
WHERE NULLIF(smtp_password, '') IS NOT NULL
  AND smtp_password <> '[REDACTED]';

CREATE OR REPLACE FUNCTION public.write_crud_audit()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    audit_table_name text := TG_TABLE_NAME || '_l';
    changed_by_user_id integer;
    source_row_data jsonb;
    parent_row_data jsonb;
    parent_audit_id bigint;
    source_column_list text;
    source_select_list text;
    parent_where text;
    pk_name text;
BEGIN
    IF TG_OP = 'INSERT' THEN
        source_row_data := to_jsonb(NEW);
        parent_row_data := NULL;
    ELSIF TG_OP = 'UPDATE' THEN
        source_row_data := to_jsonb(NEW);
        parent_row_data := to_jsonb(OLD);
    ELSE
        source_row_data := to_jsonb(OLD);
        parent_row_data := to_jsonb(OLD);
    END IF;

    IF TG_TABLE_NAME = 'email_config' THEN
        source_row_data := jsonb_set(
            source_row_data,
            '{smtp_password}',
            to_jsonb('[REDACTED]'::text),
            true);

        IF parent_row_data IS NOT NULL THEN
            parent_row_data := jsonb_set(
                parent_row_data,
                '{smtp_password}',
                to_jsonb('[REDACTED]'::text),
                true);
        END IF;
    END IF;

    changed_by_user_id := public.audit_current_user_id();

    IF parent_row_data IS NOT NULL AND TG_NARGS > 0 THEN
        parent_where := '';

        FOREACH pk_name IN ARRAY TG_ARGV LOOP
            IF parent_where <> '' THEN
                parent_where := parent_where || ' AND ';
            END IF;

            parent_where := parent_where || format(
                'to_jsonb(a.%I) IS NOT DISTINCT FROM ($1 -> %L)',
                pk_name,
                pk_name
            );
        END LOOP;

        EXECUTE format(
            'SELECT a.id_audit
             FROM public.%I a
             WHERE %s
             ORDER BY a.changed_at DESC, a.id_audit DESC
             LIMIT 1',
            audit_table_name,
            parent_where
        )
        INTO parent_audit_id
        USING parent_row_data;
    END IF;

    SELECT
        string_agg(format('%I', column_name), ', ' ORDER BY ordinal_position),
        string_agg(format('source.%I', column_name), ', ' ORDER BY ordinal_position)
    INTO source_column_list, source_select_list
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = TG_TABLE_NAME;

    EXECUTE format(
        'INSERT INTO public.%1$I (
             operation,
             changed_at,
             changed_by_user_id,
             parent_audit_id,
             %2$s
         )
         SELECT
             $1,
             NOW(),
             $2,
             $3,
             %3$s
         FROM jsonb_populate_record(NULL::public.%4$I, $4) AS source',
        audit_table_name,
        source_column_list,
        source_select_list,
        TG_TABLE_NAME
    )
    USING TG_OP, changed_by_user_id, parent_audit_id, source_row_data;

    RETURN COALESCE(NEW, OLD);
END;
$function$;

INSERT INTO public.schema_migrations (version, name)
VALUES ('040', 'protect_smtp_password_storage')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 040_protect_smtp_password_storage
\endif
