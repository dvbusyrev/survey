using System.Text.RegularExpressions;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Configuration;
using MainProject.Application.DTO.Theme;
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

    private readonly IThemeConfigRepository _themeConfigRepository;
    private readonly ILogger<ThemeSettingsService> _logger;

    public ThemeSettingsService(
        IThemeConfigRepository themeConfigRepository,
        ILogger<ThemeSettingsService> logger)
    {
        _themeConfigRepository = themeConfigRepository;
        _logger = logger;
    }

    public async Task<ThemeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var row = await _themeConfigRepository.GetAsync(DefaultConfigId, cancellationToken);

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

        await _themeConfigRepository.SaveAsync(
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

    private sealed record BackgroundImagePayload(byte[]? Bytes, string FileName, string ContentType);
}
