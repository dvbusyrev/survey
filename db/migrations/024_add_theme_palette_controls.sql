\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '024') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 024_add_theme_palette_controls

BEGIN;

ALTER TABLE public.theme_config
    ADD COLUMN IF NOT EXISTS soft_lighten_percent integer NOT NULL DEFAULT 16,
    ADD COLUMN IF NOT EXISTS header_darken_percent integer NOT NULL DEFAULT 16,
    ADD COLUMN IF NOT EXISTS footer_darken_percent integer NOT NULL DEFAULT 16,
    ADD COLUMN IF NOT EXISTS button_darken_percent integer NOT NULL DEFAULT 16,
    ADD COLUMN IF NOT EXISTS button_strong_darken_percent integer NOT NULL DEFAULT 28,
    ADD COLUMN IF NOT EXISTS surface_tint_opacity_percent integer NOT NULL DEFAULT 24;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_theme_config_soft_lighten_percent'
            AND conrelid = 'public.theme_config'::regclass
    ) THEN
        ALTER TABLE public.theme_config
            ADD CONSTRAINT ck_theme_config_soft_lighten_percent
            CHECK (soft_lighten_percent BETWEEN 0 AND 100);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_theme_config_header_darken_percent'
            AND conrelid = 'public.theme_config'::regclass
    ) THEN
        ALTER TABLE public.theme_config
            ADD CONSTRAINT ck_theme_config_header_darken_percent
            CHECK (header_darken_percent BETWEEN 0 AND 100);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_theme_config_footer_darken_percent'
            AND conrelid = 'public.theme_config'::regclass
    ) THEN
        ALTER TABLE public.theme_config
            ADD CONSTRAINT ck_theme_config_footer_darken_percent
            CHECK (footer_darken_percent BETWEEN 0 AND 100);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_theme_config_button_darken_percent'
            AND conrelid = 'public.theme_config'::regclass
    ) THEN
        ALTER TABLE public.theme_config
            ADD CONSTRAINT ck_theme_config_button_darken_percent
            CHECK (button_darken_percent BETWEEN 0 AND 100);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_theme_config_button_strong_darken_percent'
            AND conrelid = 'public.theme_config'::regclass
    ) THEN
        ALTER TABLE public.theme_config
            ADD CONSTRAINT ck_theme_config_button_strong_darken_percent
            CHECK (button_strong_darken_percent BETWEEN 0 AND 100);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_theme_config_surface_tint_opacity_percent'
            AND conrelid = 'public.theme_config'::regclass
    ) THEN
        ALTER TABLE public.theme_config
            ADD CONSTRAINT ck_theme_config_surface_tint_opacity_percent
            CHECK (surface_tint_opacity_percent BETWEEN 0 AND 100);
    END IF;
END $$;

INSERT INTO public.schema_migrations (version, name)
VALUES ('024', 'add_theme_palette_controls')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 024_add_theme_palette_controls
\endif
