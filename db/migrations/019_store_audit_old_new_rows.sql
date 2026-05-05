\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '019') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 019_store_audit_old_new_rows

BEGIN;

DO $$
DECLARE
    audit_table text;
BEGIN
    FOREACH audit_table IN ARRAY ARRAY[
        'app_user_l',
        'organization_l',
        'survey_l',
        'survey_question_l',
        'organization_survey_l',
        'answer_l',
        'answer_item_l',
        'auto_creation_config_l',
        'survey_auto_creation_config_l',
        'email_config_l'
    ] LOOP
        IF to_regclass('public.' || audit_table) IS NOT NULL THEN
            EXECUTE format('ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS old_row_data jsonb', audit_table);
            EXECUTE format('ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS new_row_data jsonb', audit_table);

            EXECUTE format(
                $sql$
                UPDATE public.%I
                SET
                    old_row_data = COALESCE(old_row_data, CASE WHEN operation = 'DELETE' THEN row_data END),
                    new_row_data = COALESCE(new_row_data, CASE WHEN operation IN ('INSERT', 'UPDATE') THEN row_data END)
                WHERE old_row_data IS NULL
                   OR new_row_data IS NULL
                $sql$,
                audit_table
            );
        END IF;
    END LOOP;
END;
$$;

CREATE OR REPLACE FUNCTION public.write_crud_audit()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    source_row jsonb;
    old_row jsonb;
    new_row jsonb;
    record_pk jsonb := '{}'::jsonb;
    pk_name text;
    changed_by_user_id integer;
BEGIN
    IF TG_OP = 'INSERT' THEN
        new_row := to_jsonb(NEW);
        source_row := new_row;
    ELSIF TG_OP = 'UPDATE' THEN
        old_row := to_jsonb(OLD);
        new_row := to_jsonb(NEW);
        source_row := new_row;
    ELSE
        old_row := to_jsonb(OLD);
        source_row := old_row;
    END IF;

    changed_by_user_id := public.audit_current_user_id();

    FOREACH pk_name IN ARRAY TG_ARGV LOOP
        record_pk := record_pk || jsonb_build_object(pk_name, source_row -> pk_name);
    END LOOP;

    EXECUTE format(
        'INSERT INTO public.%I_l (operation, changed_at, changed_by_user_id, record_pk, row_data, old_row_data, new_row_data)
         VALUES ($1, NOW(), $2, $3, $4, $5, $6)',
        TG_TABLE_NAME
    )
    USING TG_OP, changed_by_user_id, record_pk, source_row, old_row, new_row;

    RETURN COALESCE(NEW, OLD);
END;
$function$;

CREATE OR REPLACE FUNCTION public.write_survey_auto_creation_config_audit()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    changed_by integer;
    audit_record_pk jsonb;
    audit_row_data jsonb;
    audit_old_row_data jsonb;
    audit_new_row_data jsonb;
BEGIN
    IF current_setting('app.current_user_id', true) IS NOT NULL
        AND current_setting('app.current_user_id', true) <> ''
    THEN
        changed_by := current_setting('app.current_user_id', true)::integer;
    END IF;

    IF TG_OP = 'INSERT' THEN
        audit_record_pk := jsonb_build_object('id_config', NEW.id_config, 'id_survey', NEW.id_survey);
        audit_new_row_data := to_jsonb(NEW);
        audit_row_data := audit_new_row_data;
    ELSIF TG_OP = 'UPDATE' THEN
        audit_record_pk := jsonb_build_object('id_config', NEW.id_config, 'id_survey', NEW.id_survey);
        audit_old_row_data := to_jsonb(OLD);
        audit_new_row_data := to_jsonb(NEW);
        audit_row_data := audit_new_row_data;
    ELSE
        audit_record_pk := jsonb_build_object('id_config', OLD.id_config, 'id_survey', OLD.id_survey);
        audit_old_row_data := to_jsonb(OLD);
        audit_row_data := audit_old_row_data;
    END IF;

    INSERT INTO public.survey_auto_creation_config_l
    (
        operation,
        changed_by_user_id,
        record_pk,
        row_data,
        old_row_data,
        new_row_data
    )
    VALUES
    (
        TG_OP,
        changed_by,
        audit_record_pk,
        audit_row_data,
        audit_old_row_data,
        audit_new_row_data
    );

    RETURN COALESCE(NEW, OLD);
END;
$function$;

INSERT INTO public.schema_migrations (version, name)
VALUES ('019', 'store_audit_old_new_rows')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 019_store_audit_old_new_rows
\endif
