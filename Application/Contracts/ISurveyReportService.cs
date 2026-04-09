using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface ISurveyReportService
{
    GeneratedFileResult CreateSurveyMonthlyReport(int surveyId, int organizationId);
    GeneratedFileResult CreateAllMonthlyReport();
    GeneratedFileResult CreateQuarterlyReport(int quarter, int year);
}
