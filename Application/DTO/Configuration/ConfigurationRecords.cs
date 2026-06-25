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
    public string? SmtpPassword { get; init; }
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

public sealed class AutoCreationConfigRecord
{
    public int IdConfig { get; init; }
    public int CreationDayId { get; init; }
    public int BeginDayId { get; init; }
    public int? WorkingPeriod { get; init; }
    public string CreationPattern { get; init; } = "1-monday";
    public string StartPattern { get; init; } = "1-monday";
    public string CreationDayName { get; init; } = "Monday";
    public int CreationWeekNumber { get; init; } = 1;
    public string BeginDayName { get; init; } = "Monday";
    public int BeginWeekNumber { get; init; } = 1;
    public bool IsEnabled { get; init; }
}
