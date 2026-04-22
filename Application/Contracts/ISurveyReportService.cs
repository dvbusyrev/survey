using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface ISurveyReportService
{
    IReadOnlyList<int> GetAvailableReportYears();
    GeneratedFileResult CreateSurveyMonthlyReport(int surveyId, int organizationId);
    GeneratedFileResult CreateAllMonthlyReport(int month, int year);
    GeneratedFileResult CreateQuarterlyReport(int quarter, int year);
}
