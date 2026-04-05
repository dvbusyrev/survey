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
        _answerDataService.InsertHistoryAnswer(historyAnswerData);
        _answerDataService.DeleteAccessExtension(historyAnswerData.id_omsu, historyAnswerData.id_survey);

        return BuildCheckAnswersPage(historyAnswerData.id_survey, historyAnswerData.id_omsu, historyAnswerData.answers);
    }

    public CheckAnswersPageViewModel? UpdateAnswer(HistoryAnswer historyAnswerData)
    {
        var updated = _answerDataService.UpdateHistoryAnswer(historyAnswerData);
        if (!updated)
        {
            return null;
        }

        return BuildCheckAnswersPage(historyAnswerData.id_survey, historyAnswerData.id_omsu, historyAnswerData.answers);
    }

    public UpdateAnswerPageViewModel? GetUpdateAnswerPage(int surveyId, int omsuId)
    {
        var historyAnswer = _answerDataService.GetHistoryAnswer(surveyId, omsuId);
        if (historyAnswer == null || string.IsNullOrWhiteSpace(historyAnswer.answers))
        {
            return null;
        }

        return new UpdateAnswerPageViewModel
        {
            SurveyId = surveyId,
            OmsuId = omsuId,
            Answers = AnswerPayloadParser.Parse(historyAnswer.answers)
        };
    }

    public SurveyAnswersResponse GetAnswersResponse(int surveyId, int omsuId, string? type, bool includeAllOmsuAnswers)
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
            includeAllOmsuAnswers ? null : omsuId);

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
                OmsuId = answer.id_omsu,
                OmsuName = answer.name_omsu ?? "Неизвестно",
                Date = answer.completion_date?.ToString("dd.MM.yyyy HH:mm") ?? "Дата не указана",
                Answers = AnswerPayloadParser.Parse(answer.answers)
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

    private CheckAnswersPageViewModel? BuildCheckAnswersPage(int surveyId, int omsuId, string? answersJson)
    {
        var survey = _answerDataService.GetSurveyInfo(surveyId);
        if (survey == null)
        {
            return null;
        }

        return new CheckAnswersPageViewModel
        {
            Survey = survey,
            Answers = AnswerPayloadParser.Parse(answersJson),
            IdOmsu = omsuId
        };
    }
}
