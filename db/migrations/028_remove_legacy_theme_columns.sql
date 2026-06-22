\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '028') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 028_remove_legacy_theme_columns

BEGIN;

ALTER TABLE public.theme_config
    DROP CONSTRAINT IF EXISTS ck_theme_config_soft_lighten_percent,
    DROP CONSTRAINT IF EXISTS ck_theme_config_button_strong_darken_percent;

ALTER TABLE public.theme_config
    DROP COLUMN IF EXISTS gradient_enabled,
    DROP COLUMN IF EXISTS gradient_start_color,
    DROP COLUMN IF EXISTS gradient_end_color,
    DROP COLUMN IF EXISTS background_image_data_url,
    DROP COLUMN IF EXISTS soft_lighten_percent,
    DROP COLUMN IF EXISTS button_strong_darken_percent;

ALTER TABLE public.theme_config_l
    DROP COLUMN IF EXISTS gradient_enabled,
    DROP COLUMN IF EXISTS gradient_start_color,
    DROP COLUMN IF EXISTS gradient_end_color,
    DROP COLUMN IF EXISTS background_image_data_url,
    DROP COLUMN IF EXISTS soft_lighten_percent,
    DROP COLUMN IF EXISTS button_strong_darken_percent;

INSERT INTO public.schema_migrations (version, name)
VALUES ('028', 'remove_legacy_theme_columns')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 028_remove_legacy_theme_columns
\endif
