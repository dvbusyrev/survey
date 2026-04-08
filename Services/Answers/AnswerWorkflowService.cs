using main_project.Models;

namespace main_project.Services.Answers;

public sealed class AnswerWorkflowService
{
    private readonly AnswerDataService _answerDataService;

    public AnswerWorkflowService(AnswerDataService answerDataService)
    {
        _answerDataService = answerDataService;
    }

    public CheckAnswersPageViewModel? InsertAnswer(HistoryAnswer historyAnswerData)
    {
        var existingAnswer = _answerDataService.GetHistoryAnswer(historyAnswerData.id_survey, historyAnswerData.organization_id);
        if (existingAnswer == null)
        {
            _answerDataService.InsertHistoryAnswer(historyAnswerData);
        }
        else
        {
            _answerDataService.UpdateHistoryAnswer(historyAnswerData);
        }

        _answerDataService.ClearSurveyExtension(historyAnswerData.organization_id, historyAnswerData.id_survey);

        return BuildCheckAnswersPage(historyAnswerData.id_survey, historyAnswerData.organization_id, historyAnswerData.Answers);
    }

    public CheckAnswersPageViewModel? UpdateAnswer(HistoryAnswer historyAnswerData)
    {
        var updated = _answerDataService.UpdateHistoryAnswer(historyAnswerData);
        if (!updated)
        {
            return null;
        }

        return BuildCheckAnswersPage(historyAnswerData.id_survey, historyAnswerData.organization_id, historyAnswerData.Answers);
    }

    public UpdateAnswerPageViewModel? GetUpdateAnswerPage(int surveyId, int organizationId)
    {
        var historyAnswer = _answerDataService.GetHistoryAnswer(surveyId, organizationId);
        if (historyAnswer == null || historyAnswer.Answers.Count == 0)
        {
            return null;
        }

        return new UpdateAnswerPageViewModel
        {
            SurveyId = surveyId,
            OrganizationId = organizationId,
            Answers = historyAnswer.Answers
        };
    }

    public SurveyAnswersResponse GetAnswersResponse(int surveyId, int organizationId, string? type, bool includeAllOrganizationAnswers)
    {
        var surveyInfo = _answerDataService.GetSurveyInfo(surveyId);
        if (surveyInfo == null)
        {
            return new SurveyAnswersResponse
            {
                Success = false,
                Error = "Анкета не найдена"
            };
        }

        var historyAnswers = _answerDataService.GetHistoryAnswers(
            surveyId,
            includeAllOrganizationAnswers ? null : organizationId);

        if (historyAnswers.Count == 0)
        {
            return new SurveyAnswersResponse
            {
                Success = false,
                Error = "Ответы не найдены"
            };
        }

        var mappedAnswers = historyAnswers
            .Select(answer => new SurveyAnswerResultViewModel
            {
                Id = answer.id_answer,
                OrganizationId = answer.organization_id,
                OrganizationName = answer.organization_name ?? "Неизвестно",
                Date = answer.completion_date?.ToString("dd.MM.yyyy HH:mm") ?? "Дата не указана",
                Answers = answer.Answers
                    .Select(item => new SurveyAnswerResultItemViewModel
                    {
                        QuestionText = item.DisplayQuestion,
                        Rating = item.DisplayRating,
                        Comment = item.Comment ?? string.Empty
                    })
                    .ToList(),
                IsSigned = !string.IsNullOrWhiteSpace(answer.csp),
                Signature = answer.csp
            })
            .ToList();

        return new SurveyAnswersResponse
        {
            Success = true,
            Survey = new SurveyAnswersSurveyViewModel
            {
                Id = surveyId,
                Name = surveyInfo.name_survey ?? string.Empty,
                Description = surveyInfo.description,
                IsArchive = string.Equals(type, "archive", StringComparison.OrdinalIgnoreCase),
                Csp = historyAnswers.FirstOrDefault(answer => !string.IsNullOrWhiteSpace(answer.csp))?.csp
            },
            Answers = mappedAnswers
        };
    }

    private CheckAnswersPageViewModel? BuildCheckAnswersPage(int surveyId, int organizationId, IReadOnlyList<AnswerPayloadItem> answers)
    {
        var survey = _answerDataService.GetSurveyInfo(surveyId);
        if (survey == null)
        {
            return null;
        }

        return new CheckAnswersPageViewModel
        {
            Survey = survey,
            Answers = answers,
            IdOrganization = organizationId
        };
    }
}
