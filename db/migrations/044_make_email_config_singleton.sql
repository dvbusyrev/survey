\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW()
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '044') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 044_make_email_config_singleton

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

WITH current_config AS MATERIALIZED
(
    SELECT
        recipient_emails,
        subject_text,
        body_text,
        smtp_host,
        smtp_port,
        smtp_enable_ssl,
        smtp_user_name,
        smtp_password,
        from_address,
        from_display_name
    FROM public.email_config
    ORDER BY date_update DESC, id_config DESC
    LIMIT 1
)
INSERT INTO public.email_config
(
    id_config,
    recipient_emails,
    subject_text,
    body_text,
    smtp_host,
    smtp_port,
    smtp_enable_ssl,
    smtp_user_name,
    smtp_password,
    from_address,
    from_display_name
)
SELECT
    1,
    recipient_emails,
    subject_text,
    body_text,
    smtp_host,
    smtp_port,
    smtp_enable_ssl,
    smtp_user_name,
    smtp_password,
    from_address,
    from_display_name
FROM current_config
ON CONFLICT (id_config) DO UPDATE
SET
    recipient_emails = EXCLUDED.recipient_emails,
    subject_text = EXCLUDED.subject_text,
    body_text = EXCLUDED.body_text,
    smtp_host = EXCLUDED.smtp_host,
    smtp_port = EXCLUDED.smtp_port,
    smtp_enable_ssl = EXCLUDED.smtp_enable_ssl,
    smtp_user_name = EXCLUDED.smtp_user_name,
    smtp_password = EXCLUDED.smtp_password,
    from_address = EXCLUDED.from_address,
    from_display_name = EXCLUDED.from_display_name;

INSERT INTO public.email_config (id_config)
VALUES (1)
ON CONFLICT (id_config) DO NOTHING;

DELETE FROM public.email_config
WHERE id_config <> 1;

ALTER TABLE public.email_config
    DROP CONSTRAINT IF EXISTS ck_email_config_singleton;

ALTER TABLE public.email_config
    ADD CONSTRAINT ck_email_config_singleton CHECK (id_config = 1);

ALTER TABLE public.email_config
    DROP COLUMN IF EXISTS date_update;

ALTER TABLE public.email_config_l
    DROP COLUMN IF EXISTS date_update;

DROP FUNCTION IF EXISTS public.set_update_metadata();

INSERT INTO public.schema_migrations (version, name)
VALUES ('044', 'make_email_config_singleton')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 044_make_email_config_singleton
\endif
