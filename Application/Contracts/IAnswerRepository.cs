using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface IAnswerRepository
{
    bool AnswerRecordExists(int surveyId, int organizationId);
    Survey? GetSurveyInfo(int surveyId);
    IReadOnlyList<SurveyQuestionItem> GetSurveyQuestions(int surveyId);
    AnswerRecord? GetAnswerRecord(int surveyId, int organizationId);
    AnswerRecord? GetDraftRecord(int surveyId, int organizationId);
    IReadOnlyList<AnswerRecord> GetAnswerRecords(int surveyId, int? organizationId = null);
    AnswerStorageResult SubmitAnswer(AnswerRecord answerRecord);
    AnswerStorageResult UpdateAnswer(AnswerRecord answerRecord);
    bool SaveDraft(AnswerRecord answerRecord);
    bool TrySaveAnswerSignature(int surveyId, int organizationId, string signature, byte[]? signedContent);
    bool TrySaveDraftSignature(int surveyId, int organizationId, string signature, byte[]? signedContent);
    void DeleteDraft(int surveyId, int organizationId);
}
