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
    public void TryResolveMonthWeekdayDate_ReturnsExpectedFourthWeekday()
    {
        var success = SurveyAutoCreationScheduleHelper.TryResolveMonthWeekdayDate(2026, 5, "4-friday", out var result);

        Assert.True(success);
        Assert.Equal(new DateTime(2026, 5, 22), result);
    }

    [Fact]
    public void AddBusinessDays_SkipsWeekends()
    {
        var result = SurveyAutoCreationScheduleHelper.AddBusinessDays(new DateTime(2026, 4, 24), 8);

        Assert.Equal(new DateTime(2026, 5, 6), result);
    }
}
