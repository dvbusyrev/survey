using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface IAnswerExportService
{
    Task<AnswerGeneratedFileResult?> CreatePdfReportAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<AnswerGeneratedFileResult?> CreateSignedArchiveAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<AnswerGeneratedFileResult?> CreateSurveyReportAsync(int surveyId, int organizationId, string? type, CancellationToken cancellationToken = default);
}
