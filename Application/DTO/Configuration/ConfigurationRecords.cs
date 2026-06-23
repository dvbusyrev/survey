namespace MainProject.Application.DTO.Configuration;

public sealed class EmailConfigRecord
{
    public string? To { get; init; }
    public string? Subject { get; init; }
    public string? Content { get; init; }
    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; }
    public bool SmtpEnableSsl { get; init; }
    public string? SmtpUserName { get; init; }
    public string? SmtpPasswordEncrypted { get; init; }
    public string? FromAddress { get; init; }
    public string? FromDisplayName { get; init; }
}

public sealed class ThemeConfigRecord
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
