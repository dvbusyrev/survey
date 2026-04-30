\set ON_ERROR_STOP on

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '017') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 017_rename_app_user_credentials

BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'app_user'
          AND column_name = 'name_user'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'app_user'
          AND column_name = 'login'
    ) THEN
        ALTER TABLE public.app_user RENAME COLUMN name_user TO login;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'app_user'
          AND column_name = 'name_role'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'app_user'
          AND column_name = 'role'
    ) THEN
        ALTER TABLE public.app_user RENAME COLUMN name_role TO role;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'app_user'
          AND column_name = 'hash_password'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'app_user'
          AND column_name = 'password'
    ) THEN
        ALTER TABLE public.app_user RENAME COLUMN hash_password TO password;
    END IF;
END;
$$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.app_user'::regclass
          AND conname = 'app_user_name_user_key'
    ) THEN
        ALTER TABLE public.app_user
            RENAME CONSTRAINT app_user_name_user_key TO app_user_login_key;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.app_user'::regclass
          AND conname = 'chk_app_user_name_role'
    ) THEN
        ALTER TABLE public.app_user
            RENAME CONSTRAINT chk_app_user_name_role TO chk_app_user_role;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.app_user'::regclass
          AND conname = 'app_user_name_user_not_null'
    ) THEN
        ALTER TABLE public.app_user
            RENAME CONSTRAINT app_user_name_user_not_null TO app_user_login_not_null;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.app_user'::regclass
          AND conname = 'app_user_name_role_not_null'
    ) THEN
        ALTER TABLE public.app_user
            RENAME CONSTRAINT app_user_name_role_not_null TO app_user_role_not_null;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.app_user'::regclass
          AND conname = 'app_user_hash_password_not_null'
    ) THEN
        ALTER TABLE public.app_user
            RENAME CONSTRAINT app_user_hash_password_not_null TO app_user_password_not_null;
    END IF;
END;
$$;

INSERT INTO public.schema_migrations (version, name)
VALUES ('017', 'rename_app_user_credentials')
ON CONFLICT (version) DO NOTHING;

COMMIT;
\else
\echo Skipping migration 017_rename_app_user_credentials
\endif
