using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface IAnswerExportService
{
    AnswerGeneratedFileResult? CreatePdfReport(int surveyId, int organizationId);
    AnswerGeneratedFileResult? CreateSignedArchive(int surveyId, int organizationId);
    AnswerGeneratedFileResult? CreateSurveyReport(int surveyId, int organizationId, string? type);
}
