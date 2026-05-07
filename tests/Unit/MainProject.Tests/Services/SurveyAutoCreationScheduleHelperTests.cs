using MainProject.Application.UseCases.Surveys;

namespace MainProject.Tests.Services;

public sealed class SurveyAutoCreationScheduleHelperTests
{
    [Fact]
    public void TryResolveMonthWeekdayDate_ReturnsExpectedFirstWeekday()
    {
        var success = SurveyAutoCreationScheduleHelper.TryResolveMonthWeekdayDate(2026, 4, "1-monday", out var result);

        Assert.True(success);
        Assert.Equal(new DateTime(2026, 4, 6), result);
    }

    [Fact]
    public void TryResolveMonthWeekdayDate_RejectsFourthWeekday()
    {
        var success = SurveyAutoCreationScheduleHelper.TryResolveMonthWeekdayDate(2026, 5, "4-friday", out var result);

        Assert.False(success);
        Assert.Equal(default, result);
    }

    [Fact]
    public void GetBusinessDayOffsetOptions_ReturnsValuesUpToFourteen()
    {
        var options = SurveyAutoCreationScheduleHelper.GetBusinessDayOffsetOptions();

        Assert.Equal(14, options.Count);
        Assert.Equal(1, options[0].Value);
        Assert.Equal(14, options[^1].Value);
    }

    [Fact]
    public void AddBusinessDays_SkipsWeekends()
    {
        var result = SurveyAutoCreationScheduleHelper.AddBusinessDays(new DateTime(2026, 4, 24), 8);

        Assert.Equal(new DateTime(2026, 5, 6), result);
    }
}
