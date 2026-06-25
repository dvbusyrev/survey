using MainProject.Application.UseCases.Surveys;

namespace MainProject.Tests.Services;

public sealed class SurveyReportMetricsTests
{
    [Fact]
    public void Calculate_SeparatesOrganizationAndCriterionAverages()
    {
        IReadOnlyList<IReadOnlyList<int>> ratings =
        [
            new[] { 5, 3 },
            new[] { 1, 5 }
        ];

        var result = SurveyReportMetricsCalculator.Calculate(ratings, criterionCount: 2);

        Assert.Equal([3, 4], result.CriterionAverages);
        Assert.Equal([4, 3], result.OrganizationAverages);
        Assert.Equal(3.5, result.OverallAverage);
    }

    [Fact]
    public void Calculate_ReturnsNoOrganizationAverageForEmptyAnswers()
    {
        IReadOnlyList<IReadOnlyList<int>> ratings =
        [
            Array.Empty<int>()
        ];

        var result = SurveyReportMetricsCalculator.Calculate(ratings, criterionCount: 1);

        Assert.Equal([0], result.CriterionAverages);
        Assert.Equal([null], result.OrganizationAverages);
        Assert.Equal(0, result.OverallAverage);
    }
}
