using MainProject.Application.DTO;
using MainProject.Application.DTO.Read;
using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface IAnswerReadRepository
{
    Task<Survey?> GetSurveyAsync(int surveyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnswerRecord>> GetSurveyAnswersAsync(
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<AnswerListReadData> GetListAsync(
        AnswerListReadRequest request,
        CancellationToken cancellationToken = default);

    Task<SurveySignatureReadData> GetSignatureStatusAsync(
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<AnswerStatisticsReadData> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
