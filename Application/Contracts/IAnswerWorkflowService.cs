using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IAnswerWorkflowService
{
    Task<AnswerMutationResult> InsertAnswerAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default);
    Task<AnswerMutationResult> UpdateAnswerAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default);
    Task<AnswerMutationResult> SaveDraftAnswerAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default);
    Task<AnswerRecord?> GetDraftAnswerAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<UpdateAnswerPageViewModel?> GetUpdateAnswerPageAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<SurveyAnswersResponse> GetAnswersResponseAsync(
        int surveyId,
        int organizationId,
        string? type,
        bool includeAllOrganizationAnswers,
        CancellationToken cancellationToken = default);
}
