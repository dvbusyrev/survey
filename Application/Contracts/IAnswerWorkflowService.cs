using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IAnswerWorkflowService
{
    AnswerMutationResult InsertAnswer(AnswerRecord answerRecord);
    AnswerMutationResult UpdateAnswer(AnswerRecord answerRecord);
    UpdateAnswerPageViewModel? GetUpdateAnswerPage(int surveyId, int organizationId);
    SurveyAnswersResponse GetAnswersResponse(int surveyId, int organizationId, string? type, bool includeAllOrganizationAnswers);
}
