using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface ISurveyReportService
{
    Task<IReadOnlyList<int>> GetAvailableReportYearsAsync(CancellationToken cancellationToken = default);
    Task<GeneratedFileResult> CreateSurveyMonthlyReportAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<GeneratedFileResult> CreateAllMonthlyReportAsync(int month, int year, CancellationToken cancellationToken = default);
    Task<GeneratedFileResult> CreateQuarterlyReportAsync(int quarter, int year, CancellationToken cancellationToken = default);
}
