namespace MainProject.Infrastructure.External.Calendar;

public sealed class ProductionCalendarOptions
{
    public string BaseUrl { get; set; } = "https://isdayoff.ru/";
    public string DataPath { get; set; } = string.Empty;
    public bool RemoteDownloadEnabled { get; set; } = true;
}
