using System.Text.RegularExpressions;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Theme;
using MainProject.Infrastructure.Persistence;
using Npgsql;

namespace MainProject.Application.UseCases.Admin;

public sealed class ThemeSettingsService : IThemeSettingsService
{
    private const int DefaultConfigId = 1;
    private const int MaxBackgroundImageBytes = 4_000_000;
    private const string DefaultFontColor = "#343D4B";
    private const string DefaultBackgroundColor = "#B2A8FF";
    private const int DefaultDetailsBrightness = 59;
    private const int CurrentThemeScaleMarker = 50;
    private const string DefaultGradientStartColor = "#B2A8FF";
    private const string DefaultGradientEndColor = "#B2A8FF";
    private static readonly Regex HexColorRegex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<string, string> DefaultImageFileNamesByContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = "background-image.png",
            ["image/jpeg"] = "background-image.jpg",
            ["image/jpg"] = "background-image.jpg",
            ["image/webp"] = "background-image.webp"
        };

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<ThemeSettingsService> _logger;

    public ThemeSettingsService(
        IDbConnectionFactory connectionFactory,
        ILogger<ThemeSettingsService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<ThemeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        try
        {
            var row = await connection.QueryFirstOrDefaultAsync<ThemeSettingsRow>(
                new CommandDefinition(
                    """
                    SELECT
                        font_color AS FontColor,
                        background_color AS BackgroundColor,
                        gradient_enabled AS GradientEnabled,
                        gradient_start_color AS GradientStartColor,
                        gradient_end_color AS GradientEndColor,
                        effect_snow AS EffectSnow,
                        effect_fireworks AS EffectFireworks,
                        effect_grass AS EffectGrass,
                        effect_rain AS EffectRain,
                        background_image AS BackgroundImage,
                        background_image_file_name AS BackgroundImageFileName,
                        background_image_content_type AS BackgroundImageContentType,
                        background_image_data_url AS BackgroundImageDataUrl,
                        background_image_opacity AS BackgroundImageOpacity,
                        soft_lighten_percent AS SoftLightenPercent,
                        header_darken_percent AS HeaderDarkenPercent,
                        footer_darken_percent AS FooterDarkenPercent,
                        button_darken_percent AS ButtonDarkenPercent,
                        button_strong_darken_percent AS ButtonStrongDarkenPercent,
                        surface_tint_opacity_percent AS SurfaceTintOpacityPercent
                    FROM public.theme_config
                    WHERE id_config = @configId
                    LIMIT 1;
                    """,
                    new { configId = DefaultConfigId },
                    cancellationToken: cancellationToken));

            if (row == null)
            {
                return new ThemeSettings();
            }

            var isLegacyDarkenScale = row.ButtonStrongDarkenPercent != CurrentThemeScaleMarker;
            var backgroundImageDataUrl = BuildBackgroundImageDataUrl(
                row.BackgroundImage,
                row.BackgroundImageContentType,
                row.BackgroundImageDataUrl);

            return ApplyDerivedGradientColors(new ThemeSettings
            {
                FontColor = NormalizeColorOrDefault(row.FontColor, DefaultFontColor),
                BackgroundColor = NormalizeColorOrDefault(row.BackgroundColor, DefaultBackgroundColor),
                GradientEnabled = false,
                GradientStartColor = NormalizeColorOrDefault(row.GradientStartColor, DefaultGradientStartColor),
                GradientEndColor = NormalizeColorOrDefault(row.GradientEndColor, DefaultGradientEndColor),
                EffectSnow = row.EffectSnow,
                EffectFireworks = row.EffectFireworks,
                EffectGrass = row.EffectGrass,
                EffectRain = row.EffectRain,
                BackgroundImageDataUrl = backgroundImageDataUrl,
                BackgroundImageFileName = NormalizeBackgroundImageFileName(row.BackgroundImageFileName, backgroundImageDataUrl),
                BackgroundImageOpacity = NormalizeOpacity(row.BackgroundImageOpacity),
                SoftLightenPercent = NormalizePercent(row.SoftLightenPercent),
                HeaderDarkenPercent = NormalizeBrightnessPercent(row.HeaderDarkenPercent, isLegacyDarkenScale),
                FooterDarkenPercent = NormalizeBrightnessPercent(row.FooterDarkenPercent, isLegacyDarkenScale),
                ButtonDarkenPercent = NormalizeBrightnessPercent(row.ButtonDarkenPercent, isLegacyDarkenScale),
                ButtonStrongDarkenPercent = CurrentThemeScaleMarker,
                SurfaceTintOpacityPercent = isLegacyDarkenScale
                    ? DefaultDetailsBrightness
                    : NormalizePercent(row.SurfaceTintOpacityPercent)
            });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogWarning(ex, "Таблица theme_config ещё не создана, возвращены настройки темы по умолчанию.");
            return new ThemeSettings();
        }
    }

    public async Task SaveAsync(ThemeSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(settings);
        var backgroundImage = ParseBackgroundImage(normalized.BackgroundImageDataUrl, normalized.BackgroundImageFileName);

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO public.theme_config
                (
                    id_config,
                    font_color,
                    background_color,
                    gradient_enabled,
                    gradient_start_color,
                    gradient_end_color,
                    effect_snow,
                    effect_fireworks,
                    effect_grass,
                    effect_rain,
                    background_image,
                    background_image_file_name,
                    background_image_content_type,
                    background_image_data_url,
                    background_image_opacity,
                    soft_lighten_percent,
                    header_darken_percent,
                    footer_darken_percent,
                    button_darken_percent,
                    button_strong_darken_percent,
                    surface_tint_opacity_percent
                )
                VALUES
                (
                    @ConfigId,
                    @FontColor,
                    @BackgroundColor,
                    @GradientEnabled,
                    @GradientStartColor,
                    @GradientEndColor,
                    @EffectSnow,
                    @EffectFireworks,
                    @EffectGrass,
                    @EffectRain,
                    @BackgroundImage,
                    @BackgroundImageFileName,
                    @BackgroundImageContentType,
                    @BackgroundImageDataUrl,
                    @BackgroundImageOpacity,
                    @SoftLightenPercent,
                    @HeaderDarkenPercent,
                    @FooterDarkenPercent,
                    @ButtonDarkenPercent,
                    @ButtonStrongDarkenPercent,
                    @SurfaceTintOpacityPercent
                )
                ON CONFLICT (id_config) DO UPDATE
                SET
                    font_color = EXCLUDED.font_color,
                    background_color = EXCLUDED.background_color,
                    gradient_enabled = EXCLUDED.gradient_enabled,
                    gradient_start_color = EXCLUDED.gradient_start_color,
                    gradient_end_color = EXCLUDED.gradient_end_color,
                    effect_snow = EXCLUDED.effect_snow,
                    effect_fireworks = EXCLUDED.effect_fireworks,
                    effect_grass = EXCLUDED.effect_grass,
                    effect_rain = EXCLUDED.effect_rain,
                    background_image = EXCLUDED.background_image,
                    background_image_file_name = EXCLUDED.background_image_file_name,
                    background_image_content_type = EXCLUDED.background_image_content_type,
                    background_image_data_url = EXCLUDED.background_image_data_url,
                    background_image_opacity = EXCLUDED.background_image_opacity,
                    soft_lighten_percent = EXCLUDED.soft_lighten_percent,
                    header_darken_percent = EXCLUDED.header_darken_percent,
                    footer_darken_percent = EXCLUDED.footer_darken_percent,
                    button_darken_percent = EXCLUDED.button_darken_percent,
                    button_strong_darken_percent = EXCLUDED.button_strong_darken_percent,
                    surface_tint_opacity_percent = EXCLUDED.surface_tint_opacity_percent;
                """,
                new
                {
                    ConfigId = DefaultConfigId,
                    normalized.FontColor,
                    normalized.BackgroundColor,
                    normalized.GradientEnabled,
                    normalized.GradientStartColor,
                    normalized.GradientEndColor,
                    normalized.EffectSnow,
                    normalized.EffectFireworks,
                    normalized.EffectGrass,
                    normalized.EffectRain,
                    BackgroundImage = backgroundImage.Bytes,
                    BackgroundImageFileName = backgroundImage.FileName,
                    BackgroundImageContentType = backgroundImage.ContentType,
                    BackgroundImageDataUrl = string.Empty,
                    normalized.BackgroundImageOpacity,
                    normalized.SoftLightenPercent,
                    normalized.HeaderDarkenPercent,
                    normalized.FooterDarkenPercent,
                    normalized.ButtonDarkenPercent,
                    normalized.ButtonStrongDarkenPercent,
                    normalized.SurfaceTintOpacityPercent
                },
                cancellationToken: cancellationToken));
    }

    private static ThemeSettings NormalizeAndValidate(ThemeSettings? settings)
    {
        if (settings == null)
        {
            throw new ThemeSettingsValidationException(["Параметры темы не переданы"]);
        }

        var normalized = new ThemeSettings
        {
            FontColor = NormalizeColorOrDefault(settings.FontColor, DefaultFontColor),
            BackgroundColor = NormalizeColorOrDefault(settings.BackgroundColor, DefaultBackgroundColor),
            GradientEnabled = false,
            GradientStartColor = NormalizeColorOrDefault(settings.GradientStartColor, DefaultGradientStartColor),
            GradientEndColor = NormalizeColorOrDefault(settings.GradientEndColor, DefaultGradientEndColor),
            EffectSnow = settings.EffectSnow,
            EffectFireworks = settings.EffectFireworks,
            EffectGrass = settings.EffectGrass,
            EffectRain = settings.EffectRain,
            BackgroundImageDataUrl = NormalizeBackgroundImage(settings.BackgroundImageDataUrl),
            BackgroundImageFileName = NormalizeBackgroundImageFileName(settings.BackgroundImageFileName, settings.BackgroundImageDataUrl),
            BackgroundImageOpacity = NormalizeOpacity(settings.BackgroundImageOpacity),
            SoftLightenPercent = NormalizePercent(settings.SoftLightenPercent),
            HeaderDarkenPercent = NormalizePercent(settings.HeaderDarkenPercent),
            FooterDarkenPercent = NormalizePercent(settings.FooterDarkenPercent),
            ButtonDarkenPercent = NormalizePercent(settings.ButtonDarkenPercent),
            ButtonStrongDarkenPercent = CurrentThemeScaleMarker,
            SurfaceTintOpacityPercent = NormalizePercent(settings.SurfaceTintOpacityPercent)
        };

        var errors = new List<string>();

        ValidateColor(normalized.FontColor, "Цвет шрифта", errors);
        ValidateColor(normalized.BackgroundColor, "Цвет фона", errors);

        ParseBackgroundImage(normalized.BackgroundImageDataUrl, normalized.BackgroundImageFileName, errors);

        if (normalized.BackgroundImageOpacity < 0 || normalized.BackgroundImageOpacity > 100)
        {
            errors.Add("Прозрачность фонового изображения должна быть от 0 до 100.");
        }

        ValidatePercent(normalized.HeaderDarkenPercent, "Яркость шапки", errors);
        ValidatePercent(normalized.FooterDarkenPercent, "Яркость подвала", errors);
        ValidatePercent(normalized.ButtonDarkenPercent, "Яркость кнопок", errors);
        ValidatePercent(normalized.SurfaceTintOpacityPercent, "Яркость деталей", errors);

        if (errors.Count > 0)
        {
            throw new ThemeSettingsValidationException(errors);
        }

        return ApplyDerivedGradientColors(normalized);
    }

    private static void ValidateColor(string value, string fieldName, ICollection<string> errors)
    {
        if (!HexColorRegex.IsMatch(value))
        {
            errors.Add($"Поле «{fieldName}» заполнено некорректно");
        }
    }

    private static string NormalizeColorOrDefault(string? value, string fallback)
    {
        var normalized = (value ?? string.Empty).Trim();
        return HexColorRegex.IsMatch(normalized) ? normalized.ToUpperInvariant() : fallback;
    }

    private static string NormalizeBackgroundImage(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeBackgroundImageFileName(string? value, string? backgroundImageDataUrl)
    {
        var normalized = Path.GetFileName((value ?? string.Empty).Trim());
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized.Length <= 255 ? normalized : normalized[..255];
        }

        var contentType = GetContentTypeFromDataUrl(backgroundImageDataUrl);
        return GetDefaultImageFileName(contentType);
    }

    private static BackgroundImagePayload ParseBackgroundImage(
        string? dataUrl,
        string? fileName,
        ICollection<string>? errors = null)
    {
        var normalizedDataUrl = NormalizeBackgroundImage(dataUrl);
        if (string.IsNullOrWhiteSpace(normalizedDataUrl))
        {
            return new BackgroundImagePayload(null, string.Empty, string.Empty);
        }

        var separatorIndex = normalizedDataUrl.IndexOf(',', StringComparison.Ordinal);
        var contentType = GetContentTypeFromDataUrl(normalizedDataUrl);
        if (separatorIndex < 0 || !DefaultImageFileNamesByContentType.ContainsKey(contentType))
        {
            errors?.Add("Фоновое изображение должно быть PNG, JPEG или WebP.");
            return new BackgroundImagePayload(null, string.Empty, string.Empty);
        }

        try
        {
            var bytes = Convert.FromBase64String(normalizedDataUrl[(separatorIndex + 1)..]);
            if (bytes.Length > MaxBackgroundImageBytes)
            {
                errors?.Add("Фоновое изображение слишком большое. Уменьшите файл.");
                return new BackgroundImagePayload(null, string.Empty, string.Empty);
            }

            return new BackgroundImagePayload(
                bytes,
                NormalizeBackgroundImageFileName(fileName, normalizedDataUrl),
                NormalizeImageContentType(contentType));
        }
        catch (FormatException)
        {
            errors?.Add("Фоновое изображение заполнено некорректно.");
            return new BackgroundImagePayload(null, string.Empty, string.Empty);
        }
    }

    private static string BuildBackgroundImageDataUrl(byte[]? imageBytes, string? contentType, string? legacyDataUrl)
    {
        var normalizedContentType = NormalizeImageContentType(contentType);
        if (imageBytes is { Length: > 0 } && DefaultImageFileNamesByContentType.ContainsKey(normalizedContentType))
        {
            return $"data:{normalizedContentType};base64,{Convert.ToBase64String(imageBytes)}";
        }

        return NormalizeBackgroundImage(legacyDataUrl);
    }

    private static string GetContentTypeFromDataUrl(string? dataUrl)
    {
        var normalized = NormalizeBackgroundImage(dataUrl);
        if (!normalized.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var contentTypeEnd = normalized.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
        return contentTypeEnd > 5
            ? NormalizeImageContentType(normalized[5..contentTypeEnd])
            : string.Empty;
    }

    private static string NormalizeImageContentType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized == "image/jpg" ? "image/jpeg" : normalized;
    }

    private static string GetDefaultImageFileName(string? contentType)
    {
        var normalizedContentType = NormalizeImageContentType(contentType);
        return DefaultImageFileNamesByContentType.TryGetValue(normalizedContentType, out var fileName)
            ? fileName
            : string.Empty;
    }

    private static int NormalizeOpacity(int value)
    {
        return Math.Clamp(value, 0, 100);
    }

    private static int NormalizePercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }

    private static int NormalizeBrightnessPercent(int value, bool isLegacyDarkenScale)
    {
        var normalized = NormalizePercent(value);
        if (!isLegacyDarkenScale)
        {
            return normalized;
        }

        return Math.Clamp(50 - (normalized / 2), 0, 100);
    }

    private static void ValidatePercent(int value, string fieldName, ICollection<string> errors)
    {
        if (value < 0 || value > 100)
        {
            errors.Add($"Поле «{fieldName}» должно быть от 0 до 100.");
        }
    }

    private static ThemeSettings ApplyDerivedGradientColors(ThemeSettings settings)
    {
        var gradientStartColor = settings.BackgroundColor;
        var gradientEndColor = settings.BackgroundColor;

        settings.GradientEnabled = false;
        settings.GradientStartColor = gradientStartColor;
        settings.GradientEndColor = gradientEndColor;
        return settings;
    }

    private static string MixHexColors(string primaryColor, string secondaryColor, double secondaryWeight)
    {
        var (primaryRed, primaryGreen, primaryBlue) = ParseColor(primaryColor);
        var (secondaryRed, secondaryGreen, secondaryBlue) = ParseColor(secondaryColor);
        var weight = Math.Clamp(secondaryWeight, 0d, 1d);
        var primaryWeight = 1d - weight;

        return $"#{ToHexChannel((primaryRed * primaryWeight) + (secondaryRed * weight))}{ToHexChannel((primaryGreen * primaryWeight) + (secondaryGreen * weight))}{ToHexChannel((primaryBlue * primaryWeight) + (secondaryBlue * weight))}";
    }

    private static string ShiftHexColor(
        string sourceColor,
        double hueDelta = 0d,
        double saturationDelta = 0d,
        double lightnessDelta = 0d)
    {
        var (red, green, blue) = ParseColor(sourceColor);
        var (hue, saturation, lightness) = RgbToHsl(red, green, blue);
        var shiftedHue = NormalizeHue(hue + hueDelta);
        var shiftedSaturation = Math.Clamp(saturation + saturationDelta, 0d, 100d);
        var shiftedLightness = Math.Clamp(lightness + lightnessDelta, 0d, 100d);
        var (shiftedRed, shiftedGreen, shiftedBlue) = HslToRgb(shiftedHue, shiftedSaturation, shiftedLightness);

        return $"#{ToHexChannel(shiftedRed)}{ToHexChannel(shiftedGreen)}{ToHexChannel(shiftedBlue)}";
    }

    private static (int Red, int Green, int Blue) ParseColor(string value)
    {
        var normalized = NormalizeColorOrDefault(value, "#000000");
        return
        (
            Convert.ToInt32(normalized[1..3], 16),
            Convert.ToInt32(normalized[3..5], 16),
            Convert.ToInt32(normalized[5..7], 16)
        );
    }

    private static (double Hue, double Saturation, double Lightness) RgbToHsl(int red, int green, int blue)
    {
        var normalizedRed = red / 255d;
        var normalizedGreen = green / 255d;
        var normalizedBlue = blue / 255d;
        var max = Math.Max(normalizedRed, Math.Max(normalizedGreen, normalizedBlue));
        var min = Math.Min(normalizedRed, Math.Min(normalizedGreen, normalizedBlue));
        var lightness = (max + min) / 2d;

        if (Math.Abs(max - min) < double.Epsilon)
        {
            return (0d, 0d, lightness * 100d);
        }

        var delta = max - min;
        var saturation = lightness > 0.5d
            ? delta / (2d - max - min)
            : delta / (max + min);

        double hue;
        if (Math.Abs(max - normalizedRed) < double.Epsilon)
        {
            hue = ((normalizedGreen - normalizedBlue) / delta) + (normalizedGreen < normalizedBlue ? 6d : 0d);
        }
        else if (Math.Abs(max - normalizedGreen) < double.Epsilon)
        {
            hue = ((normalizedBlue - normalizedRed) / delta) + 2d;
        }
        else
        {
            hue = ((normalizedRed - normalizedGreen) / delta) + 4d;
        }

        return (hue * 60d, saturation * 100d, lightness * 100d);
    }

    private static (double Red, double Green, double Blue) HslToRgb(double hue, double saturation, double lightness)
    {
        var normalizedHue = NormalizeHue(hue) / 360d;
        var normalizedSaturation = Math.Clamp(saturation, 0d, 100d) / 100d;
        var normalizedLightness = Math.Clamp(lightness, 0d, 100d) / 100d;

        if (normalizedSaturation <= 0d)
        {
            var channel = normalizedLightness * 255d;
            return (channel, channel, channel);
        }

        var q = normalizedLightness < 0.5d
            ? normalizedLightness * (1d + normalizedSaturation)
            : normalizedLightness + normalizedSaturation - (normalizedLightness * normalizedSaturation);
        var p = (2d * normalizedLightness) - q;

        return
        (
            HueToRgb(p, q, normalizedHue + (1d / 3d)) * 255d,
            HueToRgb(p, q, normalizedHue) * 255d,
            HueToRgb(p, q, normalizedHue - (1d / 3d)) * 255d
        );
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0d)
        {
            t += 1d;
        }
        else if (t > 1d)
        {
            t -= 1d;
        }

        if (t < 1d / 6d)
        {
            return p + ((q - p) * 6d * t);
        }

        if (t < 1d / 2d)
        {
            return q;
        }

        if (t < 2d / 3d)
        {
            return p + ((q - p) * ((2d / 3d) - t) * 6d);
        }

        return p;
    }

    private static double NormalizeHue(double hue)
    {
        var normalized = hue % 360d;
        return normalized < 0d ? normalized + 360d : normalized;
    }

    private static string ToHexChannel(double value)
    {
        var channel = Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
        return channel.ToString("X2");
    }

    private sealed class ThemeSettingsRow
    {
        public string FontColor { get; init; } = string.Empty;
        public string BackgroundColor { get; init; } = string.Empty;
        public bool GradientEnabled { get; init; }
        public string GradientStartColor { get; init; } = string.Empty;
        public string GradientEndColor { get; init; } = string.Empty;
        public bool EffectSnow { get; init; }
        public bool EffectFireworks { get; init; }
        public bool EffectGrass { get; init; }
        public bool EffectRain { get; init; }
        public byte[]? BackgroundImage { get; init; }
        public string BackgroundImageFileName { get; init; } = string.Empty;
        public string BackgroundImageContentType { get; init; } = string.Empty;
        public string BackgroundImageDataUrl { get; init; } = string.Empty;
        public int BackgroundImageOpacity { get; init; }
        public int SoftLightenPercent { get; init; }
        public int HeaderDarkenPercent { get; init; }
        public int FooterDarkenPercent { get; init; }
        public int ButtonDarkenPercent { get; init; }
        public int ButtonStrongDarkenPercent { get; init; }
        public int SurfaceTintOpacityPercent { get; init; }
    }

    private sealed record BackgroundImagePayload(byte[]? Bytes, string FileName, string ContentType);
}
