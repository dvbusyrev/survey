\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '034') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 034_repair_audit_id_generators

BEGIN;

DO $migration$
DECLARE
    audit_table text;
    audit_sequence text;
    id_is_identity boolean;
    id_default text;
    max_id bigint;
BEGIN
    FOREACH audit_table IN ARRAY ARRAY[
        'answer_l',
        'answer_item_l',
        'app_user_l',
        'auto_creation_config_l',
        'email_config_l',
        'organization_l',
        'organization_survey_l',
        'survey_auto_creation_config_l',
        'survey_l',
        'survey_question_l',
        'theme_config_l'
    ]
    LOOP
        IF to_regclass(format('public.%I', audit_table)) IS NULL THEN
            CONTINUE;
        END IF;

        SELECT
            is_identity = 'YES',
            column_default
        INTO id_is_identity, id_default
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = audit_table
          AND column_name = 'id_audit';

        IF id_is_identity OR id_default IS NOT NULL THEN
            CONTINUE;
        END IF;

        audit_sequence := audit_table || '_id_audit_seq';

        EXECUTE format(
            'CREATE SEQUENCE IF NOT EXISTS public.%I AS bigint',
            audit_sequence
        );
        EXECUTE format(
            'ALTER SEQUENCE public.%I OWNED BY public.%I.id_audit',
            audit_sequence,
            audit_table
        );
        EXECUTE format(
            'ALTER TABLE public.%I ALTER COLUMN id_audit SET DEFAULT nextval(%L::regclass)',
            audit_table,
            'public.' || audit_sequence
        );
        EXECUTE format(
            'SELECT COALESCE(MAX(id_audit), 0) FROM public.%I',
            audit_table
        ) INTO max_id;

        IF max_id > 0 THEN
            PERFORM pg_catalog.setval(
                format('public.%I', audit_sequence)::regclass,
                max_id,
                true
            );
        ELSE
            PERFORM pg_catalog.setval(
                format('public.%I', audit_sequence)::regclass,
                1,
                false
            );
        END IF;
    END LOOP;
END;
$migration$;

INSERT INTO public.schema_migrations (version, name)
VALUES ('034', 'repair_audit_id_generators')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 034_repair_audit_id_generators
\endif
