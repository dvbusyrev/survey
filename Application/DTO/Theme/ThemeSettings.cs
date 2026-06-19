namespace MainProject.Application.DTO.Theme;

public sealed class ThemeSettings
{
    public string FontColor { get; set; } = "#343D4B";
    public string BackgroundColor { get; set; } = "#B2A8FF";
    public bool GradientEnabled { get; set; }
    public string GradientStartColor { get; set; } = "#B2A8FF";
    public string GradientEndColor { get; set; } = "#B2A8FF";
    public bool EffectSnow { get; set; }
    public bool EffectFireworks { get; set; }
    public bool EffectGrass { get; set; }
    public bool EffectRain { get; set; }
    public string BackgroundImageDataUrl { get; set; } = string.Empty;
    public string BackgroundImageFileName { get; set; } = string.Empty;
    public int BackgroundImageOpacity { get; set; } = 35;
    public int SoftLightenPercent { get; set; }
    public int HeaderDarkenPercent { get; set; } = 42;
    public int FooterDarkenPercent { get; set; } = 42;
    public int ButtonDarkenPercent { get; set; } = 42;
    public int ButtonStrongDarkenPercent { get; set; } = 50;
    public int SurfaceTintOpacityPercent { get; set; } = 59;
}
