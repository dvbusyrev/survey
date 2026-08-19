namespace MainProject.Application.UseCases.Surveys;

public sealed class ReportDataNotFoundException : InvalidOperationException
{
    private ReportDataNotFoundException(string message)
        : base(message)
    {
    }

    public static ReportDataNotFoundException ForMonth() =>
        new("За выбранный месяц и год нет ответов для формирования отчёта.");

    public static ReportDataNotFoundException ForQuarter() =>
        new("За выбранный квартал и год нет ответов для формирования отчёта.");
}
