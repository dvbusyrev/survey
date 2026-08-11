using System.Text.RegularExpressions;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Theme;
using MainProject.Infrastructure.Persistence;
using Npgsql;

namespace MainProject.Application.UseCases.Admin;

public class ThemeSettingsService
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

    protected ThemeSettingsService()
    {
        _connectionFactory = null!;
        _logger = null!;
    }

    public ThemeSettingsService(
        IDbConnectionFactory connectionFactory,
        ILogger<ThemeSettingsService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public virtual async Task<ThemeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var row = await GetThemeConfigAsync(DefaultConfigId, cancellationToken);

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

    public virtual async Task SaveAsync(ThemeSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(settings);
        var backgroundImage = ParseBackgroundImage(normalized.BackgroundImageDataUrl, normalized.BackgroundImageFileName);

        await SaveThemeConfigAsync(
            DefaultConfigId,
            new ThemeConfigRecord
            {
                FontColor = normalized.FontColor,
                BackgroundColor = normalized.BackgroundColor,
                EffectSnow = normalized.EffectSnow,
                EffectFireworks = normalized.EffectFireworks,
                EffectGrass = normalized.EffectGrass,
                EffectRain = normalized.EffectRain,
                BackgroundImage = backgroundImage.Bytes,
                BackgroundImageFileName = backgroundImage.FileName,
                BackgroundImageContentType = backgroundImage.ContentType,
                BackgroundImageOpacity = normalized.BackgroundImageOpacity,
                HeaderDarkenPercent = normalized.HeaderDarkenPercent,
                FooterDarkenPercent = normalized.FooterDarkenPercent,
                ButtonDarkenPercent = normalized.ButtonDarkenPercent,
                SurfaceTintOpacityPercent = normalized.SurfaceTintOpacityPercent
            },
            cancellationToken);
    }

    private async Task<ThemeConfigRecord?> GetThemeConfigAsync(
        int configId,
        CancellationToken cancellationToken)
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

    private async Task SaveThemeConfigAsync(
        int configId,
        ThemeConfigRecord record,
        CancellationToken cancellationToken)
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

    private static ThemeSettings NormalizeAndValidate(ThemeSettings? settings)
    {
        if (settings == null)
        {
            throw new ThemeSettingsValidationException(["Параметры темы не переданы."]);
        }

        var errors = new List<string>();
        var fontColor = (settings.FontColor ?? string.Empty).Trim();
        var backgroundColor = (settings.BackgroundColor ?? string.Empty).Trim();
        var backgroundImageDataUrl = NormalizeBackgroundImage(settings.BackgroundImageDataUrl);
        var submittedImageFileName = (settings.BackgroundImageFileName ?? string.Empty).Trim();

        ValidateColor(fontColor, "Цвет шрифта", errors);
        ValidateColor(backgroundColor, "Цвет фона", errors);

        if (submittedImageFileName.Length > 255)
        {
            errors.Add("Имя файла фонового изображения должно содержать не более 255 символов.");
        }

        ParseBackgroundImage(backgroundImageDataUrl, submittedImageFileName, errors);

        if (settings.BackgroundImageOpacity < 0 || settings.BackgroundImageOpacity > 100)
        {
            errors.Add("Непрозрачность фонового изображения должна быть от 0 до 100.");
        }

        ValidatePercent(settings.HeaderDarkenPercent, "Яркость шапки", errors);
        ValidatePercent(settings.FooterDarkenPercent, "Яркость подвала", errors);
        ValidatePercent(settings.ButtonDarkenPercent, "Яркость кнопок", errors);
        ValidatePercent(settings.SurfaceTintOpacityPercent, "Яркость деталей", errors);

        if (errors.Count > 0)
        {
            throw new ThemeSettingsValidationException(errors);
        }

        return new ThemeSettings
        {
            FontColor = fontColor.ToUpperInvariant(),
            BackgroundColor = backgroundColor.ToUpperInvariant(),
            EffectSnow = settings.EffectSnow,
            EffectFireworks = settings.EffectFireworks,
            EffectGrass = settings.EffectGrass,
            EffectRain = settings.EffectRain,
            BackgroundImageDataUrl = backgroundImageDataUrl,
            BackgroundImageFileName = NormalizeBackgroundImageFileName(submittedImageFileName, backgroundImageDataUrl),
            BackgroundImageOpacity = settings.BackgroundImageOpacity,
            HeaderDarkenPercent = settings.HeaderDarkenPercent,
            FooterDarkenPercent = settings.FooterDarkenPercent,
            ButtonDarkenPercent = settings.ButtonDarkenPercent,
            SurfaceTintOpacityPercent = settings.SurfaceTintOpacityPercent
        };
    }

    private static void ValidateColor(string value, string fieldName, ICollection<string> errors)
    {
        if (!HexColorRegex.IsMatch(value))
        {
            errors.Add($"Проверьте значение поля «{fieldName}».");
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

    private sealed record BackgroundImagePayload(byte[]? Bytes, string FileName, string ContentType);
}

internal sealed class ThemeConfigRecord
{
    public string? FontColor { get; init; }
    public string? BackgroundColor { get; init; }
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
