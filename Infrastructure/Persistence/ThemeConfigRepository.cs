using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Configuration;

namespace MainProject.Infrastructure.Persistence;

public sealed class ThemeConfigRepository : IThemeConfigRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ThemeConfigRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ThemeConfigRecord?> GetAsync(int configId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<ThemeConfigRecord>(new CommandDefinition(
            """
            SELECT
                font_color AS FontColor,
                background_color AS BackgroundColor,
                effect_snow AS EffectSnow,
                effect_fireworks AS EffectFireworks,
                effect_grass AS EffectGrass,
                effect_rain AS EffectRain,
                background_image AS BackgroundImage,
                background_image_file_name AS BackgroundImageFileName,
                background_image_content_type AS BackgroundImageContentType,
                background_image_opacity AS BackgroundImageOpacity,
                header_darken_percent AS HeaderDarkenPercent,
                footer_darken_percent AS FooterDarkenPercent,
                button_darken_percent AS ButtonDarkenPercent,
                surface_tint_opacity_percent AS SurfaceTintOpacityPercent
            FROM public.theme_config
            WHERE id_config = @ConfigId
            LIMIT 1;
            """,
            new { ConfigId = configId },
            cancellationToken: cancellationToken));
    }

    public async Task SaveAsync(int configId, ThemeConfigRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.theme_config
            (
                id_config, font_color, background_color, effect_snow, effect_fireworks, effect_grass,
                effect_rain, background_image, background_image_file_name, background_image_content_type,
                background_image_opacity, header_darken_percent, footer_darken_percent,
                button_darken_percent, surface_tint_opacity_percent
            )
            VALUES
            (
                @ConfigId, @FontColor, @BackgroundColor, @EffectSnow, @EffectFireworks, @EffectGrass,
                @EffectRain, @BackgroundImage, @BackgroundImageFileName, @BackgroundImageContentType,
                @BackgroundImageOpacity, @HeaderDarkenPercent, @FooterDarkenPercent,
                @ButtonDarkenPercent, @SurfaceTintOpacityPercent
            )
            ON CONFLICT (id_config) DO UPDATE
            SET
                font_color = EXCLUDED.font_color,
                background_color = EXCLUDED.background_color,
                effect_snow = EXCLUDED.effect_snow,
                effect_fireworks = EXCLUDED.effect_fireworks,
                effect_grass = EXCLUDED.effect_grass,
                effect_rain = EXCLUDED.effect_rain,
                background_image = EXCLUDED.background_image,
                background_image_file_name = EXCLUDED.background_image_file_name,
                background_image_content_type = EXCLUDED.background_image_content_type,
                background_image_opacity = EXCLUDED.background_image_opacity,
                header_darken_percent = EXCLUDED.header_darken_percent,
                footer_darken_percent = EXCLUDED.footer_darken_percent,
                button_darken_percent = EXCLUDED.button_darken_percent,
                surface_tint_opacity_percent = EXCLUDED.surface_tint_opacity_percent;
            """,
            new
            {
                ConfigId = configId,
                record.FontColor,
                record.BackgroundColor,
                record.EffectSnow,
                record.EffectFireworks,
                record.EffectGrass,
                record.EffectRain,
                record.BackgroundImage,
                record.BackgroundImageFileName,
                record.BackgroundImageContentType,
                record.BackgroundImageOpacity,
                record.HeaderDarkenPercent,
                record.FooterDarkenPercent,
                record.ButtonDarkenPercent,
                record.SurfaceTintOpacityPercent
            },
            cancellationToken: cancellationToken));
    }
}
