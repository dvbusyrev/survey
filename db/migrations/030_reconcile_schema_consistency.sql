\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '030') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 030_reconcile_schema_consistency

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

-- The bootstrap snapshot marks migrations 002-021 as applied. Reconcile the
-- metadata columns that migration 003 added to databases upgraded in place.
DO $migration$
DECLARE
    source_table text;
    trigger_name text;
BEGIN
    FOREACH source_table IN ARRAY ARRAY[
        'schema_migrations',
        'answer',
        'answer_item',
        'app_user',
        'organization',
        'organization_survey',
        'survey',
        'survey_auto_creation_config',
        'survey_question',
        'week_day'
    ]
    LOOP
        IF to_regclass(format('public.%I', source_table)) IS NULL THEN
            CONTINUE;
        END IF;

        EXECUTE format(
            'ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS date_update timestamp without time zone NOT NULL DEFAULT NOW()',
            source_table
        );
        EXECUTE format(
            'ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS user_update integer',
            source_table
        );

        trigger_name := format('trg_%s_set_update_metadata', source_table);
        EXECUTE format('DROP TRIGGER IF EXISTS %I ON public.%I', trigger_name, source_table);
        EXECUTE format(
            'CREATE TRIGGER %I BEFORE INSERT OR UPDATE ON public.%I FOR EACH ROW EXECUTE FUNCTION public.set_update_metadata()',
            trigger_name,
            source_table
        );
    END LOOP;
END;
$migration$;

DO $migration$
DECLARE
    audit_table text;
BEGIN
    FOREACH audit_table IN ARRAY ARRAY[
        'answer_l',
        'answer_item_l',
        'app_user_l',
        'organization_l',
        'organization_survey_l',
        'survey_l',
        'survey_auto_creation_config_l',
        'survey_question_l'
    ]
    LOOP
        IF to_regclass(format('public.%I', audit_table)) IS NULL THEN
            CONTINUE;
        END IF;

        EXECUTE format(
            'ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS date_update timestamp without time zone',
            audit_table
        );
        EXECUTE format(
            'ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS user_update integer',
            audit_table
        );
    END LOOP;
END;
$migration$;

-- Migration 014 intentionally removed update metadata from this singleton.
DROP TRIGGER IF EXISTS trg_auto_creation_config_set_update_metadata ON public.auto_creation_config;

ALTER TABLE public.auto_creation_config
    DROP COLUMN IF EXISTS date_update,
    DROP COLUMN IF EXISTS user_update;

ALTER TABLE public.auto_creation_config_l
    DROP COLUMN IF EXISTS date_update,
    DROP COLUMN IF EXISTS user_update;

-- Keep database defaults aligned with ThemeSettingsService.
ALTER TABLE public.theme_config
    ALTER COLUMN font_color SET DEFAULT '#343D4B',
    ALTER COLUMN background_color SET DEFAULT '#B2A8FF';

-- Remove indexes already covered by a primary/unique index with the same prefix.
DROP INDEX IF EXISTS public.idx_answer_id_organization_survey;
DROP INDEX IF EXISTS public.idx_answer_draft_id_organization_survey;
DROP INDEX IF EXISTS public.idx_answer_draft_item_id_answer_draft;
DROP INDEX IF EXISTS public.idx_answer_item_id_answer;
DROP INDEX IF EXISTS public.idx_organization_survey_id_organization;
DROP INDEX IF EXISTS public.idx_survey_question_id_survey;

CREATE INDEX IF NOT EXISTS idx_survey_auto_creation_config_id_survey
    ON public.survey_auto_creation_config (id_survey);

DO $migration$
DECLARE
    item record;
BEGIN
    FOR item IN
        SELECT *
        FROM (VALUES
            ('answer_item', 'history_answer_item_id_item_not_null', 'answer_item_id_item_not_null'),
            ('answer_item', 'history_answer_item_id_answer_not_null', 'answer_item_id_answer_not_null'),
            ('answer_item', 'history_answer_item_question_order_not_null', 'answer_item_question_order_not_null'),
            ('answer_item', 'history_answer_item_question_text_not_null', 'answer_item_question_text_not_null'),
            ('answer_item', 'chk_history_answer_item_rating', 'ck_answer_item_rating'),
            ('answer_item', 'history_answer_item_id_answer_question_order_key', 'answer_item_id_answer_question_order_key'),
            ('answer_item', 'history_answer_item_pkey', 'answer_item_pkey'),
            ('answer_item', 'history_answer_item_id_answer_fkey', 'answer_item_id_answer_fkey'),
            ('email_config', 'email_template_id_email_template_not_null', 'email_config_id_config_not_null'),
            ('email_config', 'email_template_recipient_emails_not_null', 'email_config_recipient_emails_not_null'),
            ('email_config', 'email_template_subject_text_not_null', 'email_config_subject_text_not_null'),
            ('email_config', 'email_template_body_text_not_null', 'email_config_body_text_not_null'),
            ('email_config', 'email_template_smtp_host_not_null', 'email_config_smtp_host_not_null'),
            ('email_config', 'email_template_smtp_port_not_null', 'email_config_smtp_port_not_null'),
            ('email_config', 'email_template_smtp_enable_ssl_not_null', 'email_config_smtp_enable_ssl_not_null'),
            ('email_config', 'email_template_smtp_user_name_not_null', 'email_config_smtp_user_name_not_null'),
            ('email_config', 'email_template_smtp_password_not_null', 'email_config_smtp_password_not_null'),
            ('email_config', 'email_template_from_address_not_null', 'email_config_from_address_not_null'),
            ('email_config', 'email_template_from_display_name_not_null', 'email_config_from_display_name_not_null'),
            ('email_config', 'email_template_date_update_not_null', 'email_config_date_update_not_null'),
            ('email_config_l', 'email_template_l_id_audit_not_null', 'email_config_l_id_audit_not_null'),
            ('email_config_l', 'email_template_l_operation_not_null', 'email_config_l_operation_not_null'),
            ('email_config_l', 'email_template_l_changed_at_not_null', 'email_config_l_changed_at_not_null'),
            ('email_config_l', 'email_template_l_pkey', 'email_config_l_pkey'),
            ('organization', 'organization_organization_id_not_null', 'organization_id_organization_not_null'),
            ('organization_survey', 'organization_survey_date_open_not_null', 'organization_survey_date_begin_not_null'),
            ('organization_survey', 'organization_survey_organization_id_not_null', 'organization_survey_id_organization_not_null'),
            ('organization_survey', 'organization_survey_organization_id_fkey', 'organization_survey_id_organization_fkey'),
            ('organization_survey', 'organization_survey_organization_id_survey_key', 'organization_survey_id_organization_id_survey_key'),
            ('app_user', 'app_user_organization_id_fkey', 'app_user_id_organization_fkey'),
            ('survey_auto_creation_config_l', 'l_survey_auto_creation_config_id_audit_not_null', 'survey_auto_creation_config_l_id_audit_not_null'),
            ('survey_auto_creation_config_l', 'l_survey_auto_creation_config_operation_not_null', 'survey_auto_creation_config_l_operation_not_null'),
            ('survey_auto_creation_config_l', 'l_survey_auto_creation_config_changed_at_not_null', 'survey_auto_creation_config_l_changed_at_not_null'),
            ('survey_auto_creation_config_l', 'l_survey_auto_creation_config_pkey', 'survey_auto_creation_config_l_pkey')
        ) AS renames(table_name, old_name, new_name)
    LOOP
        IF EXISTS (
            SELECT 1
            FROM pg_constraint
            WHERE conrelid = format('public.%I', item.table_name)::regclass
              AND conname = item.old_name
        ) AND NOT EXISTS (
            SELECT 1
            FROM pg_constraint
            WHERE conrelid = format('public.%I', item.table_name)::regclass
              AND conname = item.new_name
        ) THEN
            EXECUTE format(
                'ALTER TABLE public.%I RENAME CONSTRAINT %I TO %I',
                item.table_name,
                item.old_name,
                item.new_name
            );
        END IF;
    END LOOP;
END;
$migration$;

DO $migration$
BEGIN
    IF to_regclass('public.organization_organization_id_seq') IS NOT NULL
        AND to_regclass('public.organization_id_organization_seq') IS NULL
    THEN
        ALTER SEQUENCE public.organization_organization_id_seq
            RENAME TO organization_id_organization_seq;
    END IF;

    IF to_regclass('public.email_template_l_id_audit_seq') IS NOT NULL
        AND to_regclass('public.email_config_l_id_audit_seq') IS NULL
    THEN
        ALTER SEQUENCE public.email_template_l_id_audit_seq
            RENAME TO email_config_l_id_audit_seq;
    END IF;

    IF to_regclass('public.l_survey_auto_creation_config_id_audit_seq') IS NOT NULL
        AND to_regclass('public.survey_auto_creation_config_l_id_audit_seq') IS NULL
    THEN
        ALTER SEQUENCE public.l_survey_auto_creation_config_id_audit_seq
            RENAME TO survey_auto_creation_config_l_id_audit_seq;
    END IF;
END;
$migration$;

INSERT INTO public.schema_migrations (version, name)
VALUES ('030', 'reconcile_schema_consistency')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 030_reconcile_schema_consistency
\endif
