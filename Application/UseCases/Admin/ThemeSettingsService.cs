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
                    WHERE id_config = @configId
                    LIMIT 1;
                    """,
                    new { configId = DefaultConfigId },
                    cancellationToken: cancellationToken));

            if (row == null)
            {
                return new ThemeSettings();
            }

            var backgroundImageDataUrl = BuildBackgroundImageDataUrl(
                row.BackgroundImage,
                row.BackgroundImageContentType);

            return new ThemeSettings
            {
                FontColor = NormalizeColorOrDefault(row.FontColor, DefaultFontColor),
                BackgroundColor = NormalizeColorOrDefault(row.BackgroundColor, DefaultBackgroundColor),
                EffectSnow = row.EffectSnow,
                EffectFireworks = row.EffectFireworks,
                EffectGrass = row.EffectGrass,
                EffectRain = row.EffectRain,
                BackgroundImageDataUrl = backgroundImageDataUrl,
                BackgroundImageFileName = NormalizeBackgroundImageFileName(row.BackgroundImageFileName, backgroundImageDataUrl),
                BackgroundImageOpacity = NormalizeOpacity(row.BackgroundImageOpacity),
                HeaderDarkenPercent = NormalizePercent(row.HeaderDarkenPercent),
                FooterDarkenPercent = NormalizePercent(row.FooterDarkenPercent),
                ButtonDarkenPercent = NormalizePercent(row.ButtonDarkenPercent),
                SurfaceTintOpacityPercent = NormalizePercent(row.SurfaceTintOpacityPercent)
            };
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
                    effect_snow,
                    effect_fireworks,
                    effect_grass,
                    effect_rain,
                    background_image,
                    background_image_file_name,
                    background_image_content_type,
                    background_image_opacity,
                    header_darken_percent,
                    footer_darken_percent,
                    button_darken_percent,
                    surface_tint_opacity_percent
                )
                VALUES
                (
                    @ConfigId,
                    @FontColor,
                    @BackgroundColor,
                    @EffectSnow,
                    @EffectFireworks,
                    @EffectGrass,
                    @EffectRain,
                    @BackgroundImage,
                    @BackgroundImageFileName,
                    @BackgroundImageContentType,
                    @BackgroundImageOpacity,
                    @HeaderDarkenPercent,
                    @FooterDarkenPercent,
                    @ButtonDarkenPercent,
                    @SurfaceTintOpacityPercent
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
                    ConfigId = DefaultConfigId,
                    normalized.FontColor,
                    normalized.BackgroundColor,
                    normalized.EffectSnow,
                    normalized.EffectFireworks,
                    normalized.EffectGrass,
                    normalized.EffectRain,
                    BackgroundImage = backgroundImage.Bytes,
                    BackgroundImageFileName = backgroundImage.FileName,
                    BackgroundImageContentType = backgroundImage.ContentType,
                    normalized.BackgroundImageOpacity,
                    normalized.HeaderDarkenPercent,
                    normalized.FooterDarkenPercent,
                    normalized.ButtonDarkenPercent,
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
            EffectSnow = settings.EffectSnow,
            EffectFireworks = settings.EffectFireworks,
            EffectGrass = settings.EffectGrass,
            EffectRain = settings.EffectRain,
            BackgroundImageDataUrl = NormalizeBackgroundImage(settings.BackgroundImageDataUrl),
            BackgroundImageFileName = NormalizeBackgroundImageFileName(settings.BackgroundImageFileName, settings.BackgroundImageDataUrl),
            BackgroundImageOpacity = NormalizeOpacity(settings.BackgroundImageOpacity),
            HeaderDarkenPercent = NormalizePercent(settings.HeaderDarkenPercent),
            FooterDarkenPercent = NormalizePercent(settings.FooterDarkenPercent),
            ButtonDarkenPercent = NormalizePercent(settings.ButtonDarkenPercent),
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

        return normalized;
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

    private static string BuildBackgroundImageDataUrl(byte[]? imageBytes, string? contentType)
    {
        var normalizedContentType = NormalizeImageContentType(contentType);
        if (imageBytes is { Length: > 0 } && DefaultImageFileNamesByContentType.ContainsKey(normalizedContentType))
        {
            return $"data:{normalizedContentType};base64,{Convert.ToBase64String(imageBytes)}";
        }

        return string.Empty;
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

    private static void ValidatePercent(int value, string fieldName, ICollection<string> errors)
    {
        if (value < 0 || value > 100)
        {
            errors.Add($"Поле «{fieldName}» должно быть от 0 до 100.");
        }
    }

    private sealed class ThemeSettingsRow
    {
        public string FontColor { get; init; } = string.Empty;
        public string BackgroundColor { get; init; } = string.Empty;
        public bool EffectSnow { get; init; }
        public bool EffectFireworks { get; init; }
        public bool EffectGrass { get; init; }
        public bool EffectRain { get; init; }
        public byte[]? BackgroundImage { get; init; }
        public string BackgroundImageFileName { get; init; } = string.Empty;
        public string BackgroundImageContentType { get; init; } = string.Empty;
        public int BackgroundImageOpacity { get; init; }
        public int HeaderDarkenPercent { get; init; }
        public int FooterDarkenPercent { get; init; }
        public int ButtonDarkenPercent { get; init; }
        public int SurfaceTintOpacityPercent { get; init; }
    }

    private sealed record BackgroundImagePayload(byte[]? Bytes, string FileName, string ContentType);
}
