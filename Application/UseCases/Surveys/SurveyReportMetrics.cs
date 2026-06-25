namespace MainProject.Application.UseCases.Surveys;

public sealed record SurveyReportMetrics(
    IReadOnlyList<double> CriterionAverages,
    IReadOnlyList<double?> OrganizationAverages,
    double OverallAverage);

public static class SurveyReportMetricsCalculator
{
    public static SurveyReportMetrics Calculate(
        IReadOnlyList<IReadOnlyList<int>> ratingsByOrganization,
        int criterionCount)
    {
        var criterionAverages = new double[Math.Max(criterionCount, 0)];

        for (var criterionIndex = 0; criterionIndex < criterionAverages.Length; criterionIndex++)
        {
            var criterionRatings = ratingsByOrganization
                .Where(ratings => ratings.Count > criterionIndex)
                .Select(ratings => ratings[criterionIndex])
                .ToArray();

            criterionAverages[criterionIndex] = criterionRatings.Length == 0
                ? 0
                : criterionRatings.Average();
        }

        var organizationAverages = ratingsByOrganization
            .Select(ratings => ratings.Count == 0 ? (double?)null : ratings.Average())
            .ToArray();

        var overallAverage = criterionAverages.Length == 0
            ? 0
            : criterionAverages.Average();

        return new SurveyReportMetrics(criterionAverages, organizationAverages, overallAverage);
    }
}
