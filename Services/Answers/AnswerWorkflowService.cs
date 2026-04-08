using MainProject.Models;

namespace MainProject.Services.Answers;

public sealed class AnswerWorkflowService
{
    private readonly AnswerDataService _answerDataService;

    public AnswerWorkflowService(AnswerDataService answerDataService)
    {
        _answerDataService = answerDataService;
    }

    public AnswerMutationResult InsertAnswer(HistoryAnswer historyAnswerData)
    {
        var validationResult = ValidateAnswerSubmission(historyAnswerData);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        var existingAnswer = _answerDataService.GetHistoryAnswer(historyAnswerData.IdSurvey, historyAnswerData.OrganizationId);
        if (existingAnswer == null)
        {
            _answerDataService.InsertHistoryAnswer(historyAnswerData);
        }
        else
        {
            _answerDataService.UpdateHistoryAnswer(historyAnswerData);
        }

        _answerDataService.ClearSurveyExtension(historyAnswerData.OrganizationId, historyAnswerData.IdSurvey);

        var model = BuildCheckAnswersPage(historyAnswerData.IdSurvey, historyAnswerData.OrganizationId, historyAnswerData.Answers);
        if (model == null)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Анкета не найдена."
            };
        }

        return new AnswerMutationResult
        {
            Success = true,
            Model = model
        };
    }

    public AnswerMutationResult UpdateAnswer(HistoryAnswer historyAnswerData)
    {
        var validationResult = ValidateAnswerSubmission(historyAnswerData);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        var updated = _answerDataService.UpdateHistoryAnswer(historyAnswerData);
        if (!updated)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Запись для обновления не найдена."
            };
        }

        var model = BuildCheckAnswersPage(historyAnswerData.IdSurvey, historyAnswerData.OrganizationId, historyAnswerData.Answers);
        if (model == null)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Анкета не найдена."
            };
        }

        return new AnswerMutationResult
        {
            Success = true,
            Model = model
        };
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
                Id = answer.IdAnswer,
                OrganizationId = answer.OrganizationId,
                OrganizationName = answer.OrganizationName ?? "Неизвестно",
                Date = answer.CompletionDate?.ToString("dd.MM.yyyy HH:mm") ?? "Дата не указана",
                Answers = answer.Answers
                    .Select(item => new SurveyAnswerResultItemViewModel
                    {
                        QuestionText = item.DisplayQuestion,
                        Rating = item.DisplayRating,
                        Comment = item.Comment ?? string.Empty
                    })
                    .ToList(),
                IsSigned = !string.IsNullOrWhiteSpace(answer.Csp),
                Signature = answer.Csp
            })
            .ToList();

        return new SurveyAnswersResponse
        {
            Success = true,
            Survey = new SurveyAnswersSurveyViewModel
            {
                Id = surveyId,
                Name = surveyInfo.NameSurvey ?? string.Empty,
                Description = surveyInfo.Description,
                IsArchive = string.Equals(type, "archive", StringComparison.OrdinalIgnoreCase),
                Csp = historyAnswers.FirstOrDefault(answer => !string.IsNullOrWhiteSpace(answer.Csp))?.Csp
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

    private AnswerMutationResult ValidateAnswerSubmission(HistoryAnswer historyAnswerData)
    {
        if (historyAnswerData.IdSurvey <= 0)
        {
            return CreateValidationFailure("Неверный идентификатор анкеты.");
        }

        if (historyAnswerData.OrganizationId <= 0)
        {
            return CreateValidationFailure("Неверный идентификатор организации.");
        }

        var survey = _answerDataService.GetSurveyInfo(historyAnswerData.IdSurvey);
        if (survey == null)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Анкета не найдена."
            };
        }

        var surveyQuestions = _answerDataService.GetSurveyQuestions(historyAnswerData.IdSurvey);
        if (surveyQuestions.Count == 0)
        {
            return CreateValidationFailure("Анкета не содержит вопросов.");
        }

        if (historyAnswerData.Answers.Count == 0)
        {
            return CreateValidationFailure("Необходимо ответить на все вопросы анкеты.");
        }

        var expectedQuestionOrders = surveyQuestions
            .Select(question => question.Id)
            .ToHashSet();

        var answeredQuestionOrders = new HashSet<int>();
        foreach (var answer in historyAnswerData.Answers)
        {
            var questionOrder = ParseQuestionOrder(answer.QuestionId);
            if (questionOrder <= 0 || !expectedQuestionOrders.Contains(questionOrder))
            {
                return CreateValidationFailure("Получен ответ на неизвестный вопрос анкеты.");
            }

            if (!answeredQuestionOrders.Add(questionOrder))
            {
                return CreateValidationFailure("Обнаружены повторяющиеся ответы на один и тот же вопрос.");
            }

            if (!answer.Rating.HasValue || answer.Rating < 1 || answer.Rating > 5)
            {
                return CreateValidationFailure("Каждый вопрос должен иметь оценку от 1 до 5.");
            }

            if (answer.Rating < 5 && string.IsNullOrWhiteSpace(answer.Comment))
            {
                return CreateValidationFailure("Для оценки ниже 5 требуется комментарий.");
            }
        }

        if (answeredQuestionOrders.Count != expectedQuestionOrders.Count)
        {
            return CreateValidationFailure("Необходимо ответить на все вопросы анкеты.");
        }

        return new AnswerMutationResult
        {
            Success = true
        };
    }

    private static int ParseQuestionOrder(string? rawQuestionId)
    {
        return int.TryParse(rawQuestionId, out var questionOrder) ? questionOrder : 0;
    }

    private static AnswerMutationResult CreateValidationFailure(string error)
    {
        return new AnswerMutationResult
        {
            Error = error
        };
    }
}
