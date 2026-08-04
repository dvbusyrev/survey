\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '031') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 031_require_user_organization

BEGIN;

DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM public.app_user
        WHERE id_organization IS NULL
    ) THEN
        RAISE EXCEPTION
            'Нельзя сделать app_user.id_organization обязательным: существуют пользователи без организации';
    END IF;
END;
$migration$;

ALTER TABLE public.app_user
    ALTER COLUMN id_organization SET NOT NULL;

ALTER TABLE public.app_user
    DROP CONSTRAINT IF EXISTS app_user_id_organization_fkey,
    DROP CONSTRAINT IF EXISTS app_user_organization_id_fkey;

ALTER TABLE public.app_user
    ADD CONSTRAINT app_user_id_organization_fkey
    FOREIGN KEY (id_organization)
    REFERENCES public.organization (id_organization)
    ON DELETE RESTRICT;

INSERT INTO public.schema_migrations (version, name)
VALUES ('031', 'require_user_organization')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 031_require_user_organization
\endif
