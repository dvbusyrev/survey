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
