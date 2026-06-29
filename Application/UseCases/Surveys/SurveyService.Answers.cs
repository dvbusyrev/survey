using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Surveys;

public partial class SurveyService
{
    public async Task<SurveyAnswerPageViewModel?> GetSurveyAnswerPageAsync(
        int surveyId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var survey = await _answerRepository.GetSurveyAsync(surveyId, cancellationToken);
        if (survey == null)
        {
            return null;
        }

        var answers = await _answerRepository.GetSurveyAnswersAsync(surveyId, cancellationToken);
        return new SurveyAnswerPageViewModel
        {
            Survey = survey,
            Answers = answers.Select(answer => new SurveyAnswerEntryViewModel
            {
                IdAnswer = answer.IdAnswer,
                IdOrganization = answer.OrganizationId,
                IdSurvey = answer.IdSurvey,
                NameOrganization = answer.OrganizationName ?? string.Empty,
                Csp = answer.Csp,
                CompletionDate = answer.CompletionDate,
                Details = answer.Answers.Select(item => new SurveyAnswerDetailViewModel
                {
                    QuestionText = item.DisplayQuestion,
                    Rating = item.Rating?.ToString(),
                    Comment = item.Comment
                }).ToList()
            }).ToList(),
            Role = role
        };
    }

    public async Task<object> GetSurveyAnswersResponseAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var survey = await _answerRepository.GetSurveyAsync(surveyId, cancellationToken);
        if (survey == null)
        {
            return new
            {
                success = false,
                error = "Анкета не найдена"
            };
        }

        var answers = await _answerRepository.GetSurveyAnswersAsync(surveyId, cancellationToken);
        return new
        {
            success = true,
            survey,
            answers
        };
    }
}
