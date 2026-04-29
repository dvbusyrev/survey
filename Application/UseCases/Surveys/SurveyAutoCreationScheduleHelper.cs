using System.Text.RegularExpressions;

namespace MainProject.Application.UseCases.Surveys;

public static class SurveyAutoCreationScheduleHelper
{
    private static readonly Regex PatternRegex = new(
        "^(?<occurrence>[1-4])-(?<weekday>monday|tuesday|wednesday|thursday|friday)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly (string Value, string Label)[] MonthWeekdayOptions = BuildMonthWeekdayOptions();

    public static IReadOnlyList<(string Value, string Label)> GetMonthWeekdayOptions()
        => MonthWeekdayOptions;

    public static IReadOnlyList<(int Value, string Label)> GetBusinessDayOffsetOptions(int minInclusive = 1, int maxInclusive = 20)
    {
        var options = new List<(int Value, string Label)>();
        for (var value = minInclusive; value <= maxInclusive; value++)
        {
            options.Add((value, $"{value} рабочих дней"));
        }

        return options;
    }

    public static bool IsValidMonthWeekdayPattern(string? pattern)
        => TryParsePattern(pattern, out _, out _);

    public static bool TryResolveMonthWeekdayDate(int year, int month, string? pattern, out DateTime date)
    {
        date = default;
        if (!TryParsePattern(pattern, out var occurrence, out var dayOfWeek))
        {
            return false;
        }

        var current = new DateTime(year, month, 1);
        var matches = 0;
        while (current.Month == month)
        {
            if (current.DayOfWeek == dayOfWeek)
            {
                matches += 1;
                if (matches == occurrence)
                {
                    date = current.Date;
                    return true;
                }
            }

            current = current.AddDays(1);
        }

        return false;
    }

    public static DateTime AddBusinessDays(DateTime startDate, int businessDays)
    {
        if (businessDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(businessDays));
        }

        var current = startDate.Date;
        var remaining = businessDays;
        while (remaining > 0)
        {
            current = current.AddDays(1);
            if (current.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            remaining -= 1;
        }

        return current;
    }

    private static bool TryParsePattern(string? pattern, out int occurrence, out DayOfWeek dayOfWeek)
    {
        occurrence = default;
        dayOfWeek = default;

        var match = PatternRegex.Match(pattern ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        occurrence = int.Parse(match.Groups["occurrence"].Value);
        return match.Groups["weekday"].Value.ToLowerInvariant() switch
        {
            "monday" => SetDayOfWeek(DayOfWeek.Monday, out dayOfWeek),
            "tuesday" => SetDayOfWeek(DayOfWeek.Tuesday, out dayOfWeek),
            "wednesday" => SetDayOfWeek(DayOfWeek.Wednesday, out dayOfWeek),
            "thursday" => SetDayOfWeek(DayOfWeek.Thursday, out dayOfWeek),
            "friday" => SetDayOfWeek(DayOfWeek.Friday, out dayOfWeek),
            _ => false
        };
    }

    private static bool SetDayOfWeek(DayOfWeek value, out DayOfWeek dayOfWeek)
    {
        dayOfWeek = value;
        return true;
    }

    private static (string Value, string Label)[] BuildMonthWeekdayOptions()
    {
        var weekdays = new[]
        {
            ("monday", "понедельник"),
            ("tuesday", "вторник"),
            ("wednesday", "среда"),
            ("thursday", "четверг"),
            ("friday", "пятница")
        };

        var prefixes = new[] { "1-й", "2-й", "3-й", "4-й" };
        var options = new List<(string Value, string Label)>();

        for (var occurrence = 1; occurrence <= 4; occurrence++)
        {
            foreach (var weekday in weekdays)
            {
                options.Add(($"{occurrence}-{weekday.Item1}", $"{prefixes[occurrence - 1]} {weekday.Item2}"));
            }
        }

        return options.ToArray();
    }
}
