\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '026') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 026_rebuild_audit_tables_as_structured_snapshots

BEGIN;

CREATE OR REPLACE FUNCTION public.rebuild_structured_audit_table(
    source_table_name text,
    primary_key_columns text[]
)
RETURNS void
LANGUAGE plpgsql
AS $function$
DECLARE
    audit_table_name text := source_table_name || '_l';
    source_regclass regclass := format('public.%I', source_table_name)::regclass;
    audit_regclass regclass := format('public.%I', audit_table_name)::regclass;
    source_column record;
    update_assignments text;
    partition_columns text;
    index_columns text;
BEGIN
    EXECUTE format(
        'ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS parent_audit_id bigint',
        audit_table_name
    );

    FOR source_column IN
        SELECT
            attname AS column_name,
            pg_catalog.format_type(atttypid, atttypmod) AS column_type
        FROM pg_attribute
        WHERE attrelid = source_regclass
          AND attnum > 0
          AND NOT attisdropped
        ORDER BY attnum
    LOOP
        EXECUTE format(
            'ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS %I %s',
            audit_table_name,
            source_column.column_name,
            source_column.column_type
        );
    END LOOP;

    SELECT string_agg(
        format('%1$I = COALESCE(a.%1$I, source.%1$I)', attname),
        ', ' ORDER BY attnum)
    INTO update_assignments
    FROM pg_attribute
    WHERE attrelid = source_regclass
      AND attnum > 0
      AND NOT attisdropped;

    IF update_assignments IS NOT NULL
       AND EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = audit_table_name
              AND column_name IN ('row_data', 'old_row_data', 'new_row_data')
       )
    THEN
        EXECUTE format(
            $sql$
            UPDATE public.%1$I a
            SET %2$s
            FROM (
                SELECT
                    id_audit,
                    (jsonb_populate_record(
                        NULL::public.%3$I,
                        COALESCE(new_row_data, row_data, old_row_data)
                    )).*
                FROM public.%1$I
            ) source
            WHERE a.id_audit = source.id_audit
            $sql$,
            audit_table_name,
            update_assignments,
            source_table_name
        );
    END IF;

    SELECT string_agg(format('%I', column_name), ', ')
    INTO partition_columns
    FROM unnest(primary_key_columns) AS column_name;

    IF partition_columns IS NOT NULL THEN
        EXECUTE format(
            $sql$
            WITH ranked AS (
                SELECT
                    id_audit,
                    LAG(id_audit) OVER (
                        PARTITION BY %1$s
                        ORDER BY changed_at, id_audit
                    ) AS previous_audit_id
                FROM public.%2$I
            )
            UPDATE public.%2$I audit_row
            SET parent_audit_id = ranked.previous_audit_id
            FROM ranked
            WHERE audit_row.id_audit = ranked.id_audit
              AND audit_row.parent_audit_id IS NULL
            $sql$,
            partition_columns,
            audit_table_name
        );
    END IF;

    EXECUTE format(
        'ALTER TABLE public.%I
            DROP COLUMN IF EXISTS record_pk,
            DROP COLUMN IF EXISTS row_data,
            DROP COLUMN IF EXISTS old_row_data,
            DROP COLUMN IF EXISTS new_row_data',
        audit_table_name
    );

    EXECUTE format(
        'CREATE INDEX IF NOT EXISTS %I ON public.%I (changed_at DESC, id_audit DESC)',
        'idx_' || audit_table_name || '_changed_at_id',
        audit_table_name
    );

    EXECUTE format(
        'CREATE INDEX IF NOT EXISTS %I ON public.%I (parent_audit_id)',
        'idx_' || audit_table_name || '_parent_audit_id',
        audit_table_name
    );

    SELECT string_agg(format('%I', column_name), ', ')
    INTO index_columns
    FROM unnest(primary_key_columns) AS column_name;

    IF index_columns IS NOT NULL THEN
        EXECUTE format(
            'CREATE INDEX IF NOT EXISTS %I ON public.%I (%s)',
            'idx_' || audit_table_name || '_record_key',
            audit_table_name,
            index_columns
        );
    END IF;
END;
$function$;

SELECT public.rebuild_structured_audit_table('app_user', ARRAY['id_user']);
SELECT public.rebuild_structured_audit_table('organization', ARRAY['id_organization']);
SELECT public.rebuild_structured_audit_table('survey', ARRAY['id_survey']);
SELECT public.rebuild_structured_audit_table('survey_question', ARRAY['id_question']);
SELECT public.rebuild_structured_audit_table('organization_survey', ARRAY['id_organization_survey']);
SELECT public.rebuild_structured_audit_table('answer', ARRAY['id_answer']);
SELECT public.rebuild_structured_audit_table('answer_item', ARRAY['id_item']);
SELECT public.rebuild_structured_audit_table('auto_creation_config', ARRAY['id_config']);
SELECT public.rebuild_structured_audit_table('survey_auto_creation_config', ARRAY['id_config', 'id_survey']);
SELECT public.rebuild_structured_audit_table('email_config', ARRAY['id_config']);
SELECT public.rebuild_structured_audit_table('theme_config', ARRAY['id_config']);

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

DROP TRIGGER IF EXISTS trg_app_user_crud_audit ON public.app_user;
CREATE TRIGGER trg_app_user_crud_audit
AFTER INSERT OR UPDATE OR DELETE ON public.app_user
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_user');

DROP TRIGGER IF EXISTS trg_organization_crud_audit ON public.organization;
CREATE TRIGGER trg_organization_crud_audit
AFTER INSERT OR UPDATE OR DELETE ON public.organization
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_organization');

DROP TRIGGER IF EXISTS trg_survey_crud_audit ON public.survey;
CREATE TRIGGER trg_survey_crud_audit
AFTER INSERT OR UPDATE OR DELETE ON public.survey
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_survey');

DROP TRIGGER IF EXISTS trg_survey_question_crud_audit ON public.survey_question;
CREATE TRIGGER trg_survey_question_crud_audit
AFTER INSERT OR UPDATE OR DELETE ON public.survey_question
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_question');

DROP TRIGGER IF EXISTS trg_organization_survey_crud_audit ON public.organization_survey;
CREATE TRIGGER trg_organization_survey_crud_audit
AFTER INSERT OR UPDATE OR DELETE ON public.organization_survey
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_organization_survey');

DROP TRIGGER IF EXISTS trg_answer_crud_audit ON public.answer;
CREATE TRIGGER trg_answer_crud_audit
AFTER INSERT OR UPDATE OR DELETE ON public.answer
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_answer');

DROP TRIGGER IF EXISTS trg_answer_item_crud_audit ON public.answer_item;
CREATE TRIGGER trg_answer_item_crud_audit
AFTER INSERT OR UPDATE OR DELETE ON public.answer_item
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_item');

DROP TRIGGER IF EXISTS trg_auto_creation_config_audit ON public.auto_creation_config;
CREATE TRIGGER trg_auto_creation_config_audit
AFTER INSERT OR UPDATE OR DELETE ON public.auto_creation_config
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_config');

DROP TRIGGER IF EXISTS trg_survey_auto_creation_config_audit ON public.survey_auto_creation_config;
CREATE TRIGGER trg_survey_auto_creation_config_audit
AFTER INSERT OR UPDATE OR DELETE ON public.survey_auto_creation_config
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_config', 'id_survey');

DROP FUNCTION IF EXISTS public.write_survey_auto_creation_config_audit();

DROP TRIGGER IF EXISTS trg_email_config_audit ON public.email_config;
CREATE TRIGGER trg_email_config_audit
AFTER INSERT OR UPDATE OR DELETE ON public.email_config
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_config');

DROP TRIGGER IF EXISTS trg_theme_config_audit ON public.theme_config;
CREATE TRIGGER trg_theme_config_audit
AFTER INSERT OR UPDATE OR DELETE ON public.theme_config
FOR EACH ROW
EXECUTE FUNCTION public.write_crud_audit('id_config');

DROP FUNCTION IF EXISTS public.rebuild_structured_audit_table(text, text[]);

INSERT INTO public.schema_migrations (version, name)
VALUES ('026', 'rebuild_audit_tables_as_structured_snapshots')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 026_rebuild_audit_tables_as_structured_snapshots
\endif
