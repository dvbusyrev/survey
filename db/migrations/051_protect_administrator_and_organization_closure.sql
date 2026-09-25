\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW()
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '051') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 051_protect_administrator_and_organization_closure

BEGIN;

CREATE OR REPLACE FUNCTION public.ensure_administrator_is_closed_before_delete()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF LOWER(BTRIM(COALESCE(OLD.role, ''))) = 'admin'
       AND (OLD.date_end IS NULL OR OLD.date_end >= CURRENT_DATE) THEN
        RAISE EXCEPTION 'Нельзя удалить действующего администратора. Сначала закройте его учётную запись.'
            USING ERRCODE = '23514',
                  CONSTRAINT = 'ck_app_user_delete_closed_admin_only';
    END IF;

    RETURN OLD;
END;
$$;

CREATE OR REPLACE FUNCTION public.ensure_active_permanent_administrator()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    proposed_admin_is_active boolean;
BEGIN
    IF LOWER(BTRIM(COALESCE(OLD.role, ''))) <> 'admin'
       OR OLD.date_end IS NOT NULL THEN
        RETURN NEW;
    END IF;

    IF LOWER(BTRIM(COALESCE(NEW.role, ''))) = 'admin'
       AND NEW.date_end IS NULL THEN
        SELECT
            (NEW.date_begin IS NULL OR NEW.date_begin <= CURRENT_DATE)
            AND EXISTS (
                SELECT 1
                FROM public.organization organization
                WHERE organization.id_organization = NEW.id_organization
                  AND (organization.date_begin IS NULL OR organization.date_begin <= CURRENT_DATE)
                  AND (organization.date_end IS NULL OR organization.date_end >= CURRENT_DATE)
            )
        INTO proposed_admin_is_active;

        IF proposed_admin_is_active THEN
            RETURN NEW;
        END IF;
    END IF;

    PERFORM pg_advisory_xact_lock(hashtext('app_user_required_permanent_admin'));

    IF NOT EXISTS (
        SELECT 1
        FROM public.app_user administrator
        INNER JOIN public.organization organization
            ON organization.id_organization = administrator.id_organization
        WHERE administrator.id_user <> OLD.id_user
          AND LOWER(BTRIM(COALESCE(administrator.role, ''))) = 'admin'
          AND administrator.date_end IS NULL
          AND (administrator.date_begin IS NULL OR administrator.date_begin <= CURRENT_DATE)
          AND (organization.date_begin IS NULL OR organization.date_begin <= CURRENT_DATE)
          AND (organization.date_end IS NULL OR organization.date_end >= CURRENT_DATE)
    ) THEN
        RAISE EXCEPTION 'Нельзя закрыть администратора: в системе должен оставаться хотя бы один действующий администратор без даты конца.'
            USING ERRCODE = '23514',
                  CONSTRAINT = 'ck_app_user_required_permanent_admin';
    END IF;

    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.ensure_organization_has_no_active_users_before_closure()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.date_end IS NOT NULL
       AND NEW.date_end IS DISTINCT FROM OLD.date_end
       AND EXISTS (
           SELECT 1
           FROM public.app_user app_user
           WHERE app_user.id_organization = OLD.id_organization
             AND (app_user.date_begin IS NULL OR app_user.date_begin <= CURRENT_DATE)
             AND (app_user.date_end IS NULL OR app_user.date_end >= CURRENT_DATE)
       ) THEN
        RAISE EXCEPTION 'Нельзя закрыть организацию: в ней есть действующие пользователи.'
            USING ERRCODE = '23514',
                  CONSTRAINT = 'ck_organization_close_without_active_users';
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_app_user_delete_closed_admin_only ON public.app_user;
CREATE TRIGGER trg_app_user_delete_closed_admin_only
BEFORE DELETE ON public.app_user
FOR EACH ROW
EXECUTE FUNCTION public.ensure_administrator_is_closed_before_delete();

DROP TRIGGER IF EXISTS trg_app_user_required_permanent_admin ON public.app_user;
CREATE TRIGGER trg_app_user_required_permanent_admin
BEFORE UPDATE OF role, id_organization, date_begin, date_end ON public.app_user
FOR EACH ROW
EXECUTE FUNCTION public.ensure_active_permanent_administrator();

DROP TRIGGER IF EXISTS trg_organization_close_without_active_users ON public.organization;
CREATE TRIGGER trg_organization_close_without_active_users
BEFORE UPDATE OF date_end ON public.organization
FOR EACH ROW
EXECUTE FUNCTION public.ensure_organization_has_no_active_users_before_closure();

INSERT INTO public.schema_migrations (version, name)
VALUES ('051', 'protect_administrator_and_organization_closure')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 051_protect_administrator_and_organization_closure
\endif
