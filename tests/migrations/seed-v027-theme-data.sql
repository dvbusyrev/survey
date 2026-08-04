\set ON_ERROR_STOP on

INSERT INTO public.theme_config
(
    id_config,
    font_color,
    background_color,
    gradient_enabled,
    gradient_start_color,
    gradient_end_color,
    background_image_data_url,
    background_image_opacity,
    header_darken_percent,
    footer_darken_percent,
    button_darken_percent,
    surface_tint_opacity_percent
)
VALUES
(
    1,
    '#112233',
    '#445566',
    true,
    '#445566',
    '#778899',
    'data:image/png;base64,cHJldmlvdXMtdmVyc2lvbi1kYXRh',
    40,
    45,
    46,
    47,
    48
)
ON CONFLICT (id_config) DO UPDATE
SET
    font_color = EXCLUDED.font_color,
    background_color = EXCLUDED.background_color,
    gradient_enabled = EXCLUDED.gradient_enabled,
    gradient_start_color = EXCLUDED.gradient_start_color,
    gradient_end_color = EXCLUDED.gradient_end_color,
    background_image_data_url = EXCLUDED.background_image_data_url,
    background_image_opacity = EXCLUDED.background_image_opacity,
    header_darken_percent = EXCLUDED.header_darken_percent,
    footer_darken_percent = EXCLUDED.footer_darken_percent,
    button_darken_percent = EXCLUDED.button_darken_percent,
    surface_tint_opacity_percent = EXCLUDED.surface_tint_opacity_percent;

-- Reproduce an upgraded database where the audit identity generator was lost
-- while email_template_l was renamed to email_config_l.
DO $seed$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'email_config_l'
          AND column_name = 'id_audit'
          AND is_identity = 'YES'
    ) THEN
        ALTER TABLE public.email_config_l
            ALTER COLUMN id_audit DROP IDENTITY IF EXISTS;
    END IF;

    ALTER TABLE public.email_config_l
        ALTER COLUMN id_audit DROP DEFAULT;
END;
$seed$;
