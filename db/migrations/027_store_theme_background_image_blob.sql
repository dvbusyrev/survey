\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '027') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 027_store_theme_background_image_blob

BEGIN;

ALTER TABLE public.theme_config
    ADD COLUMN IF NOT EXISTS background_image bytea,
    ADD COLUMN IF NOT EXISTS background_image_file_name text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS background_image_content_type text NOT NULL DEFAULT '';

ALTER TABLE public.theme_config_l
    ADD COLUMN IF NOT EXISTS background_image bytea,
    ADD COLUMN IF NOT EXISTS background_image_file_name text,
    ADD COLUMN IF NOT EXISTS background_image_content_type text;

UPDATE public.theme_config
SET
    background_image = decode(split_part(background_image_data_url, ',', 2), 'base64'),
    background_image_content_type = COALESCE(
        NULLIF(substring(background_image_data_url FROM '^data:([^;]+);base64,'), ''),
        'image/png'
    ),
    background_image_file_name = CASE
        WHEN background_image_data_url LIKE 'data:image/webp;base64,%' THEN 'background-image.webp'
        WHEN background_image_data_url LIKE 'data:image/jpeg;base64,%'
            OR background_image_data_url LIKE 'data:image/jpg;base64,%' THEN 'background-image.jpg'
        ELSE 'background-image.png'
    END,
    background_image_data_url = ''
WHERE background_image IS NULL
    AND background_image_data_url ~ '^data:image/(png|jpeg|jpg|webp);base64,[A-Za-z0-9+/=]+$';

INSERT INTO public.schema_migrations (version, name)
VALUES ('027', 'store_theme_background_image_blob')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 027_store_theme_background_image_blob
\endif
