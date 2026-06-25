using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface IAnswerRepository
{
    Task<bool> AnswerRecordExistsAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<Survey?> GetSurveyInfoAsync(int surveyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(int surveyId, CancellationToken cancellationToken = default);
    Task<AnswerRecord?> GetAnswerRecordAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<AnswerRecord?> GetDraftRecordAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnswerRecord>> GetAnswerRecordsAsync(int surveyId, int? organizationId = null, CancellationToken cancellationToken = default);
    Task<AnswerStorageResult> SubmitAnswerAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default);
    Task<AnswerStorageResult> UpdateAnswerAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default);
    Task<bool> SaveDraftAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default);
    Task<bool> TrySaveAnswerSignatureAsync(int surveyId, int organizationId, string signature, byte[]? signedContent, CancellationToken cancellationToken = default);
    Task<bool> TrySaveDraftSignatureAsync(int surveyId, int organizationId, string signature, byte[]? signedContent, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
}
