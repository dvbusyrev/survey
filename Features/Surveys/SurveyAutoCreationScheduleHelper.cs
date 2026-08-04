namespace MainProject.Application.UseCases.Surveys;

public static class SurveyAutoCreationScheduleHelper
{
    public const int MinBusinessDayPeriod = 1;

    private static readonly (string Value, string Label)[] ReportingPeriodOptions =
    [
        ("month", "Месяц"),
        ("quarter", "Квартал"),
        ("half-year", "Полугодие"),
        ("year", "Год")
    ];

    public static IReadOnlyList<(string Value, string Label)> GetReportingPeriodOptions()
        => ReportingPeriodOptions;

    public static bool TryNormalizeReportingPeriod(string? value, out string normalized)
    {
        normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "month" or "quarter" or "half-year" or "year";
    }

    public static bool IsLastMonthOfReportingPeriod(int month, string reportingPeriod)
        => reportingPeriod switch
        {
            "month" => true,
            "quarter" => month % 3 == 0,
            "half-year" => month % 6 == 0,
            "year" => month == 12,
            _ => false
        };

    public static async Task<SurveyAutoCreationSchedule> CalculateAsync(
        DateTime currentDate,
        string reportingPeriod,
        int reportingOffsetBusinessDays,
        int activePeriodBusinessDays,
        Func<DateTime, CancellationToken, Task<bool>> isBusinessDayAsync,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeReportingPeriod(reportingPeriod, out var normalizedPeriod))
        {
            throw new ArgumentException("Некорректный период отчётности.", nameof(reportingPeriod));
        }

        ValidateBusinessDayPeriod(reportingOffsetBusinessDays, nameof(reportingOffsetBusinessDays));
        ValidateBusinessDayPeriod(activePeriodBusinessDays, nameof(activePeriodBusinessDays));

        var month = currentDate.Date;
        var endDate = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
        endDate = await MoveToBusinessDayAsync(endDate, -1, isBusinessDayAsync, cancellationToken);

        if (IsLastMonthOfReportingPeriod(month.Month, normalizedPeriod))
        {
            endDate = await SubtractBusinessDaysAsync(
                endDate,
                reportingOffsetBusinessDays,
                isBusinessDayAsync,
                cancellationToken);
        }

        var startDate = await SubtractBusinessDaysAsync(
            endDate,
            activePeriodBusinessDays - 1,
            isBusinessDayAsync,
            cancellationToken);

        return new SurveyAutoCreationSchedule(startDate, endDate);
    }

    private static void ValidateBusinessDayPeriod(int value, string parameterName)
    {
        if (value < MinBusinessDayPeriod)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static async Task<DateTime> MoveToBusinessDayAsync(
        DateTime date,
        int direction,
        Func<DateTime, CancellationToken, Task<bool>> isBusinessDayAsync,
        CancellationToken cancellationToken)
    {
        var current = date.Date;
        while (!await isBusinessDayAsync(current, cancellationToken))
        {
            current = current.AddDays(direction);
        }

        return current;
    }

    private static async Task<DateTime> SubtractBusinessDaysAsync(
        DateTime date,
        int businessDays,
        Func<DateTime, CancellationToken, Task<bool>> isBusinessDayAsync,
        CancellationToken cancellationToken)
    {
        var current = date.Date;
        var remaining = businessDays;
        while (remaining > 0)
        {
            current = current.AddDays(-1);
            if (await isBusinessDayAsync(current, cancellationToken))
            {
                remaining--;
            }
        }

        return current;
    }
}

public sealed record SurveyAutoCreationSchedule(DateTime StartDate, DateTime EndDate);
