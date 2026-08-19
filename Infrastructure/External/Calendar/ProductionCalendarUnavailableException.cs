namespace MainProject.Infrastructure.External.Calendar;

public sealed class ProductionCalendarUnavailableException : Exception
{
    public const string UserMessage =
        "Производственный календарь недоступен. Загрузите данные нужного года на сервер.";

    public ProductionCalendarUnavailableException(string message)
        : base(message)
    {
    }

    public ProductionCalendarUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
