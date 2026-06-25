using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface ISurveyReportRepository
{
    Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default);
    Task<string?> GetSurveyNameAsync(int surveyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(int surveyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnswerRecord>> GetSurveyAnswersAsync(
        int surveyId,
        int? organizationId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Survey>> GetSurveysAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnswerRecord>> GetAnswersAsync(CancellationToken cancellationToken = default);
}
