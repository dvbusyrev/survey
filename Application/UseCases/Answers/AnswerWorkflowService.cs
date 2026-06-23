using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using MainProject.Domain.Entities;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerWorkflowService : IAnswerWorkflowService
{
    private readonly AnswerDataService _answerDataService;

    public AnswerWorkflowService(AnswerDataService answerDataService)
    {
        _answerDataService = answerDataService;
    }

    public AnswerMutationResult InsertAnswer(AnswerRecord answerRecord)
    {
        var validationResult = ValidateAnswerSubmission(answerRecord);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        AttachMatchingDraftSignature(answerRecord);
        _answerDataService.InsertAnswerRecord(answerRecord);

        var model = BuildCheckAnswersPage(answerRecord.IdSurvey, answerRecord.OrganizationId, answerRecord.Answers);
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

    public AnswerMutationResult SaveDraftAnswer(AnswerRecord answerRecord)
    {
        var validationResult = ValidateDraftAnswer(answerRecord);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        var saved = _answerDataService.SaveDraftRecord(answerRecord);
        if (!saved)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Назначение анкеты для организации не найдено."
            };
        }

        return new AnswerMutationResult
        {
            Success = true
        };
    }

    public AnswerRecord? GetDraftAnswer(int surveyId, int organizationId)
    {
        return _answerDataService.GetDraftRecord(surveyId, organizationId);
    }

    public AnswerMutationResult UpdateAnswer(AnswerRecord answerRecord)
    {
        var validationResult = ValidateAnswerSubmission(answerRecord);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        var updated = _answerDataService.UpdateAnswerRecord(answerRecord);
        if (!updated)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Запись для обновления не найдена."
            };
        }

        var model = BuildCheckAnswersPage(answerRecord.IdSurvey, answerRecord.OrganizationId, answerRecord.Answers);
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
        var answerRecord = _answerDataService.GetAnswerRecord(surveyId, organizationId);
        if (answerRecord == null || answerRecord.Answers.Count == 0)
        {
            return null;
        }

        return new UpdateAnswerPageViewModel
        {
            SurveyId = surveyId,
            OrganizationId = organizationId,
            Answers = answerRecord.Answers
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

        var answerRecords = _answerDataService.GetAnswerRecords(
            surveyId,
            includeAllOrganizationAnswers ? null : organizationId);

        if (answerRecords.Count == 0)
        {
            return new SurveyAnswersResponse
            {
                Success = false,
                Error = "Ответы не найдены"
            };
        }

        var mappedAnswers = answerRecords
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
                Signature = answer.Csp,
                SignatureInfo = BuildSignatureInfo(answer)
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
                Csp = answerRecords.FirstOrDefault(answer => !string.IsNullOrWhiteSpace(answer.Csp))?.Csp
            },
            Answers = mappedAnswers
        };
    }

    private static AnswerSignatureInfoViewModel BuildSignatureInfo(AnswerRecord answer)
    {
        if (string.IsNullOrWhiteSpace(answer.Csp))
        {
            return new AnswerSignatureInfoViewModel
            {
                IsSigned = false,
                IsValid = false,
                Status = "Нет подписи"
            };
        }

        var signatureBytes = TryDecodeBase64(answer.Csp);
        if (signatureBytes.Length == 0)
        {
            return new AnswerSignatureInfoViewModel
            {
                IsSigned = true,
                IsValid = null,
                Status = "Проверка недоступна",
                ValidationMessage = "Подпись сохранена, но её не удалось прочитать."
            };
        }

        SignedCms signedCms;
        try
        {
            signedCms = answer.SignedContent is { Length: > 0 }
                ? new SignedCms(new ContentInfo(answer.SignedContent), detached: true)
                : new SignedCms();
            signedCms.Decode(signatureBytes);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            return new AnswerSignatureInfoViewModel
            {
                IsSigned = true,
                IsValid = null,
                Status = "Проверка недоступна",
                ValidationMessage = $"Подпись сохранена, но её не удалось разобрать: {NormalizeExceptionMessage(exception)}"
            };
        }

        var signerCertificate = ResolveSignerCertificate(signedCms);
        var verification = VerifySignature(signedCms);

        return new AnswerSignatureInfoViewModel
        {
            IsSigned = true,
            IsValid = verification.IsValid,
            Status = verification.Status,
            SignedBy = GetSignerDisplayName(signerCertificate),
            Subject = signerCertificate?.Subject ?? string.Empty,
            Issuer = signerCertificate?.Issuer ?? string.Empty,
            SerialNumber = signerCertificate?.SerialNumber ?? string.Empty,
            Thumbprint = signerCertificate?.Thumbprint ?? string.Empty,
            ValidFrom = FormatCertificateDate(signerCertificate?.NotBefore),
            ValidTo = FormatCertificateDate(signerCertificate?.NotAfter),
            ValidationMessage = verification.Message
        };
    }

    private static byte[] TryDecodeBase64(string signature)
    {
        var normalized = string.Concat(signature.Where(character => !char.IsWhiteSpace(character)));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<byte>();
        }

        try
        {
            return Convert.FromBase64String(normalized);
        }
        catch (FormatException)
        {
            return Array.Empty<byte>();
        }
    }

    private static X509Certificate2? ResolveSignerCertificate(SignedCms signedCms)
    {
        if (signedCms.SignerInfos.Count > 0)
        {
            var certificate = signedCms.SignerInfos[0].Certificate;
            if (certificate != null)
            {
                return certificate;
            }
        }

        return signedCms.Certificates.Count > 0
            ? signedCms.Certificates[0]
            : null;
    }

    private static (bool? IsValid, string Status, string Message) VerifySignature(SignedCms signedCms)
    {
        if (signedCms.SignerInfos.Count == 0)
        {
            return (null, "Проверка недоступна", "В подписи не найден подписант.");
        }

        try
        {
            signedCms.CheckSignature(verifySignatureOnly: true);
            return (true, "Подпись корректна", "Подпись соответствует сохранённому содержимому.");
        }
        catch (Exception exception) when (IsUnsupportedSignatureVerification(exception))
        {
            return (null, "Проверка недоступна", NormalizeExceptionMessage(exception));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return (null, "Проверка недоступна", NormalizeExceptionMessage(exception));
        }
        catch (CryptographicException exception)
        {
            return (false, "Подпись некорректна", NormalizeExceptionMessage(exception));
        }
    }

    private static bool IsUnsupportedSignatureVerification(Exception exception)
    {
        if (exception is PlatformNotSupportedException or NotSupportedException)
        {
            return true;
        }

        if (exception is not CryptographicException)
        {
            return false;
        }

        var message = exception.Message ?? string.Empty;
        return message.Contains("algorithm", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not supported", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
            || message.Contains("не поддерж", StringComparison.OrdinalIgnoreCase)
            || message.Contains("алгоритм", StringComparison.OrdinalIgnoreCase)
            || message.Contains("содержим", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSignerDisplayName(X509Certificate2? certificate)
    {
        if (certificate == null)
        {
            return "Не удалось определить";
        }

        var simpleName = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        if (!string.IsNullOrWhiteSpace(simpleName))
        {
            return simpleName.Trim();
        }

        var commonName = ExtractDistinguishedNameValue(certificate.Subject, "CN");
        if (!string.IsNullOrWhiteSpace(commonName))
        {
            return commonName;
        }

        var surname = ExtractDistinguishedNameValue(certificate.Subject, "SURNAME");
        var givenName = ExtractDistinguishedNameValue(certificate.Subject, "GIVENNAME");
        var fullName = $"{surname} {givenName}".Trim();
        return string.IsNullOrWhiteSpace(fullName)
            ? "Не удалось определить"
            : fullName;
    }

    private static string ExtractDistinguishedNameValue(string distinguishedName, string key)
    {
        foreach (var part in distinguishedName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var prefix = $"{key}=";
            if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return part[prefix.Length..]
                    .Replace("\\,", ",", StringComparison.Ordinal)
                    .Trim();
            }
        }

        return string.Empty;
    }

    private static string FormatCertificateDate(DateTime? value)
    {
        return value?.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU")) ?? string.Empty;
    }

    private static string NormalizeExceptionMessage(Exception exception)
    {
        return string.IsNullOrWhiteSpace(exception.Message)
            ? "причина не указана"
            : exception.Message.Trim();
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

    private void AttachMatchingDraftSignature(AnswerRecord answerRecord)
    {
        var draft = _answerDataService.GetDraftRecord(answerRecord.IdSurvey, answerRecord.OrganizationId);
        if (draft == null || string.IsNullOrWhiteSpace(draft.Csp))
        {
            return;
        }

        if (!AreAnswersEquivalent(draft.Answers, answerRecord.Answers))
        {
            return;
        }

        answerRecord.Csp = draft.Csp;
        answerRecord.SignedContent = draft.SignedContent;
    }

    private static bool AreAnswersEquivalent(IReadOnlyList<AnswerPayloadItem> left, IReadOnlyList<AnswerPayloadItem> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var leftByQuestion = left.ToDictionary(answer => ParseQuestionOrder(answer.QuestionId));
        foreach (var rightAnswer in right)
        {
            var questionOrder = ParseQuestionOrder(rightAnswer.QuestionId);
            if (questionOrder <= 0 || !leftByQuestion.TryGetValue(questionOrder, out var leftAnswer))
            {
                return false;
            }

            if (leftAnswer.Rating != rightAnswer.Rating)
            {
                return false;
            }

            if (!string.Equals(NormalizeComment(leftAnswer.Comment), NormalizeComment(rightAnswer.Comment), StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeComment(string? comment)
    {
        return string.IsNullOrWhiteSpace(comment) ? string.Empty : comment.Trim();
    }

    private AnswerMutationResult ValidateAnswerSubmission(AnswerRecord answerRecord)
    {
        if (answerRecord.IdSurvey <= 0)
        {
            return CreateValidationFailure("Неверный идентификатор анкеты.");
        }

        if (answerRecord.OrganizationId <= 0)
        {
            return CreateValidationFailure("Неверный идентификатор организации.");
        }

        var survey = _answerDataService.GetSurveyInfo(answerRecord.IdSurvey);
        if (survey == null)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Анкета не найдена."
            };
        }

        var surveyQuestions = _answerDataService.GetSurveyQuestions(answerRecord.IdSurvey);
        if (surveyQuestions.Count == 0)
        {
            return CreateValidationFailure("Анкета не содержит вопросов.");
        }

        if (answerRecord.Answers.Count == 0)
        {
            return CreateValidationFailure("Необходимо ответить на все вопросы анкеты.");
        }

        var expectedQuestionOrders = surveyQuestions
            .Select(question => question.Id)
            .ToHashSet();

        var answeredQuestionOrders = new HashSet<int>();
        foreach (var answer in answerRecord.Answers)
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

    private AnswerMutationResult ValidateDraftAnswer(AnswerRecord answerRecord)
    {
        if (answerRecord.IdSurvey <= 0)
        {
            return CreateValidationFailure("Неверный идентификатор анкеты.");
        }

        if (answerRecord.OrganizationId <= 0)
        {
            return CreateValidationFailure("Неверный идентификатор организации.");
        }

        var survey = _answerDataService.GetSurveyInfo(answerRecord.IdSurvey);
        if (survey == null)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Анкета не найдена."
            };
        }

        var expectedQuestionOrders = _answerDataService.GetSurveyQuestions(answerRecord.IdSurvey)
            .Select(question => question.Id)
            .ToHashSet();

        var answeredQuestionOrders = new HashSet<int>();
        foreach (var answer in answerRecord.Answers)
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

            if (answer.Rating.HasValue && (answer.Rating < 1 || answer.Rating > 5))
            {
                return CreateValidationFailure("Оценка должна быть от 1 до 5.");
            }
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
