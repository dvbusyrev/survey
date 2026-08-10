using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using MainProject.Domain.Entities;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Answers;

public partial class AnswerService
{
    public virtual async Task<AnswerMutationResult> InsertAnswerAsync(
        AnswerRecord answerRecord,
        CancellationToken cancellationToken = default)
    {
        NormalizeAnswerComments(answerRecord);

        var validationResult = await ValidateAnswerSubmissionAsync(answerRecord, cancellationToken);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        await AttachMatchingDraftSignatureAsync(answerRecord, cancellationToken);
        await InsertAnswerRecordAsync(answerRecord, GetRequiredCurrentUserId(), cancellationToken);

        var model = await BuildCheckAnswersPageAsync(
            answerRecord.IdSurvey, answerRecord.OrganizationId, answerRecord.Answers, cancellationToken);
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

    public virtual async Task<AnswerMutationResult> SaveDraftAnswerAsync(
        AnswerRecord answerRecord,
        CancellationToken cancellationToken = default)
    {
        NormalizeAnswerComments(answerRecord);

        var validationResult = await ValidateDraftAnswerAsync(answerRecord, cancellationToken);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        var saved = await SaveDraftRecordAsync(answerRecord, GetRequiredCurrentUserId(), cancellationToken);
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

    public virtual Task<AnswerRecord?> GetDraftAnswerAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        GetDraftRecordAsync(surveyId, organizationId, cancellationToken);

    public virtual async Task<AnswerMutationResult> UpdateAnswerAsync(
        AnswerRecord answerRecord,
        CancellationToken cancellationToken = default)
    {
        NormalizeAnswerComments(answerRecord);

        var validationResult = await ValidateAnswerSubmissionAsync(answerRecord, cancellationToken);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        var updated = await UpdateAnswerRecordAsync(answerRecord, GetRequiredCurrentUserId(), cancellationToken);
        if (!updated)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Запись для обновления не найдена."
            };
        }

        var model = await BuildCheckAnswersPageAsync(
            answerRecord.IdSurvey, answerRecord.OrganizationId, answerRecord.Answers, cancellationToken);
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

    public virtual async Task<UpdateAnswerPageViewModel?> GetUpdateAnswerPageAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        var answerRecord = await GetAnswerRecordAsync(surveyId, organizationId, cancellationToken);
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

    public virtual async Task<SurveyAnswersResponse> GetAnswersResponseAsync(
        int surveyId,
        int organizationId,
        string? type,
        bool includeAllOrganizationAnswers,
        CancellationToken cancellationToken = default)
    {
        var surveyInfo = await GetSurveyInfoAsync(surveyId, cancellationToken);
        if (surveyInfo == null)
        {
            return new SurveyAnswersResponse
            {
                Success = false,
                Error = "Анкета не найдена."
            };
        }

        var answerRecords = await GetAnswerRecordsAsync(
            surveyId,
            includeAllOrganizationAnswers ? null : organizationId,
            cancellationToken);

        if (answerRecords.Count == 0)
        {
            return new SurveyAnswersResponse
            {
                Success = false,
                Error = "Ответы не найдены."
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
                ValidationMessage = "Подпись сохранена, но проверить её не удалось."
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
            return (null, "Проверка недоступна", "Алгоритм проверки подписи не поддерживается.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return (null, "Проверка недоступна", "Проверить подпись не удалось.");
        }
        catch (CryptographicException)
        {
            return (false, "Подпись некорректна", "Подпись не соответствует сохранённому содержимому.");
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

    private async Task<CheckAnswersPageViewModel?> BuildCheckAnswersPageAsync(
        int surveyId,
        int organizationId,
        IReadOnlyList<AnswerPayloadItem> answers,
        CancellationToken cancellationToken)
    {
        var survey = await GetSurveyInfoAsync(surveyId, cancellationToken);
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

    private async Task AttachMatchingDraftSignatureAsync(AnswerRecord answerRecord, CancellationToken cancellationToken)
    {
        var draft = await GetDraftRecordAsync(
            answerRecord.IdSurvey,
            answerRecord.OrganizationId,
            cancellationToken);
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

    private static void NormalizeAnswerComments(AnswerRecord answerRecord)
    {
        foreach (var answer in answerRecord.Answers)
        {
            if (answer.Rating == 5)
            {
                answer.Comment = null;
            }
        }
    }

    private async Task<AnswerMutationResult> ValidateAnswerSubmissionAsync(
        AnswerRecord answerRecord,
        CancellationToken cancellationToken)
    {
        if (answerRecord.IdSurvey <= 0)
        {
            return CreateValidationFailure("Некорректный идентификатор анкеты.");
        }

        if (answerRecord.OrganizationId <= 0)
        {
            return CreateValidationFailure("Некорректный идентификатор организации.");
        }

        var survey = await GetSurveyInfoAsync(answerRecord.IdSurvey, cancellationToken);
        if (survey == null)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Анкета не найдена."
            };
        }

        var surveyQuestions = await GetSurveyQuestionsAsync(answerRecord.IdSurvey, cancellationToken);
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

    private async Task<AnswerMutationResult> ValidateDraftAnswerAsync(
        AnswerRecord answerRecord,
        CancellationToken cancellationToken)
    {
        if (answerRecord.IdSurvey <= 0)
        {
            return CreateValidationFailure("Некорректный идентификатор анкеты.");
        }

        if (answerRecord.OrganizationId <= 0)
        {
            return CreateValidationFailure("Некорректный идентификатор организации.");
        }

        var survey = await GetSurveyInfoAsync(answerRecord.IdSurvey, cancellationToken);
        if (survey == null)
        {
            return new AnswerMutationResult
            {
                NotFound = true,
                Error = "Анкета не найдена."
            };
        }

        var expectedQuestionOrders = (await GetSurveyQuestionsAsync(answerRecord.IdSurvey, cancellationToken))
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
