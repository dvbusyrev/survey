using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.External.Calendar;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MainProject.Tests.Services;

public sealed class SurveyAutoCreationScheduleHelperTests
{
    [Fact]
    public void GetReportingPeriodOptions_ReturnsSupportedPeriods()
    {
        var options = SurveyAutoCreationScheduleHelper.GetReportingPeriodOptions();

        Assert.Equal(["month", "quarter", "half-year", "year"], options.Select(static option => option.Value));
    }

    [Fact]
    public async Task CalculateAsync_ActivePeriodContainsExactlyRequestedBusinessDays()
    {
        var result = await SurveyAutoCreationScheduleHelper.CalculateAsync(
            new DateTime(2026, 8, 10),
            "month",
            reportingOffsetBusinessDays: 1,
            activePeriodBusinessDays: 8,
            (date, _) => Task.FromResult(IsWeekday(date)));

        var businessDayCount = Enumerable.Range(0, (result.EndDate - result.StartDate).Days + 1)
            .Select(offset => result.StartDate.AddDays(offset))
            .Count(IsWeekday);

        Assert.Equal(8, businessDayCount);
    }

    [Fact]
    public async Task CalculateAsync_FinalPeriodMonth_SubtractsReportingAndActivePeriods()
    {
        var holidays = new HashSet<DateTime> { new(2026, 5, 27) };

        var result = await SurveyAutoCreationScheduleHelper.CalculateAsync(
            new DateTime(2026, 5, 10),
            "month",
            reportingOffsetBusinessDays: 1,
            activePeriodBusinessDays: 2,
            (date, _) => Task.FromResult(IsWeekday(date) && !holidays.Contains(date)));

        Assert.Equal(new DateTime(2026, 5, 26), result.StartDate);
        Assert.Equal(new DateTime(2026, 5, 28), result.EndDate);
    }

    [Fact]
    public async Task CalculateAsync_NonFinalQuarterMonth_UsesLastBusinessDayOfMonth()
    {
        var result = await SurveyAutoCreationScheduleHelper.CalculateAsync(
            new DateTime(2026, 4, 20),
            "quarter",
            reportingOffsetBusinessDays: 10,
            activePeriodBusinessDays: 3,
            (date, _) => Task.FromResult(IsWeekday(date)));

        Assert.Equal(new DateTime(2026, 4, 28), result.StartDate);
        Assert.Equal(new DateTime(2026, 4, 30), result.EndDate);
    }

    [Fact]
    public async Task CalculateAsync_AllowsPeriodsGreaterThanFourteenBusinessDays()
    {
        var result = await SurveyAutoCreationScheduleHelper.CalculateAsync(
            new DateTime(2026, 8, 10),
            "month",
            reportingOffsetBusinessDays: 20,
            activePeriodBusinessDays: 20,
            (date, _) => Task.FromResult(IsWeekday(date)));

        Assert.True(result.StartDate < result.EndDate);
    }

    [Fact]
    public async Task ProductionCalendar_UsesServiceCodesAndCachesYear()
    {
        var response = new string('0', 365).ToCharArray();
        response[0] = '8';
        response[1] = '2';
        var handler = new CalendarHandler(new string(response));
        var calendar = new ProductionCalendarService(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://calendar.test/")
            },
            Options.Create(new ProductionCalendarOptions()),
            NullLogger<ProductionCalendarService>.Instance);

        Assert.False(await calendar.IsBusinessDayAsync(new DateTime(2026, 1, 1)));
        Assert.True(await calendar.IsBusinessDayAsync(new DateTime(2026, 1, 2)));
        Assert.True(await calendar.IsBusinessDayAsync(new DateTime(2026, 2, 2)));
        Assert.Equal(1, handler.RequestCount);
    }

    private static bool IsWeekday(DateTime date)
        => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private sealed class CalendarHandler(string content) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}
