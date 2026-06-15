using System.Globalization;
using MainProject.Application.DTO;
using MainProject.Application.Contracts;

namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerSigningService : IAnswerSigningService
{
    private readonly AnswerDataService _answerDataService;

    public AnswerSigningService(AnswerDataService answerDataService)
    {
        _answerDataService = answerDataService;
    }

    public AnswerSigningPayload GetSigningData(int surveyId, int organizationId)
    {
        var survey = _answerDataService.GetSurveyInfo(surveyId)
            ?? throw new InvalidOperationException("Анкета для подписи не найдена.");
        var answerRecords = _answerDataService.GetAnswerRecords(surveyId, organizationId).ToList();
        if (answerRecords.Count == 0)
        {
            throw new InvalidOperationException("Ответы для подписи не найдены.");
        }

        if (answerRecords.Any(answer => !string.IsNullOrWhiteSpace(answer.Csp)))
        {
            throw new AnswerAlreadySignedException();
        }

        var pdfBytes = AnswerPdfDocumentBuilder.BuildPdfContent(survey, answerRecords);
        return new AnswerSigningPayload
        {
            Content = Convert.ToBase64String(pdfBytes),
            ContentEncoding = "base64",
            Detached = true,
            FileName = $"{survey.NameSurvey ?? "Анкета"}_{surveyId.ToString(CultureInfo.InvariantCulture)}.pdf"
        };
    }

    public bool SaveSignature(int surveyId, int organizationId, AnswerSignatureSaveRequest request)
    {
        var signature = NormalizeBase64Payload(request.Signature);
        if (string.IsNullOrWhiteSpace(signature))
        {
            throw new ArgumentException("Подпись не может быть пустой.", nameof(request));
        }

        byte[]? signedContent = null;
        if (request.Detached && string.Equals(request.ContentEncoding, "base64", StringComparison.OrdinalIgnoreCase))
        {
            signedContent = DecodeBase64Payload(request.SignedContent, "подписанный PDF");
        }

        var answerRecord = _answerDataService.GetAnswerRecord(surveyId, organizationId);
        if (answerRecord == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(answerRecord.Csp))
        {
            throw new AnswerAlreadySignedException();
        }

        if (_answerDataService.UpdateSignature(surveyId, organizationId, signature, signedContent))
        {
            return true;
        }

        var updatedAnswerRecord = _answerDataService.GetAnswerRecord(surveyId, organizationId);
        if (!string.IsNullOrWhiteSpace(updatedAnswerRecord?.Csp))
        {
            throw new AnswerAlreadySignedException();
        }

        return false;
    }

    public AnswerSigningPayload GetDraftSigningData(int surveyId, int organizationId)
    {
        var survey = _answerDataService.GetSurveyInfo(surveyId)
            ?? throw new InvalidOperationException("Анкета для подписи не найдена.");
        var draftRecord = _answerDataService.GetDraftRecord(surveyId, organizationId);
        if (draftRecord == null || draftRecord.Answers.Count == 0)
        {
            throw new InvalidOperationException("Черновик не содержит ответов для подписи.");
        }

        if (!string.IsNullOrWhiteSpace(draftRecord.Csp))
        {
            throw new AnswerAlreadySignedException();
        }

        draftRecord.CompletionDate ??= DateTime.Now;
        var pdfBytes = AnswerPdfDocumentBuilder.BuildPdfContent(survey, new[] { draftRecord });
        return new AnswerSigningPayload
        {
            Content = Convert.ToBase64String(pdfBytes),
            ContentEncoding = "base64",
            Detached = true,
            FileName = $"{survey.NameSurvey ?? "Анкета"}_{surveyId.ToString(CultureInfo.InvariantCulture)}_draft.pdf"
        };
    }

    public bool SaveDraftSignature(int surveyId, int organizationId, AnswerSignatureSaveRequest request)
    {
        var signature = NormalizeBase64Payload(request.Signature);
        if (string.IsNullOrWhiteSpace(signature))
        {
            throw new ArgumentException("Подпись не может быть пустой.", nameof(request));
        }

        byte[]? signedContent = null;
        if (request.Detached && string.Equals(request.ContentEncoding, "base64", StringComparison.OrdinalIgnoreCase))
        {
            signedContent = DecodeBase64Payload(request.SignedContent, "подписанный PDF");
        }

        var draftRecord = _answerDataService.GetDraftRecord(surveyId, organizationId);
        if (draftRecord == null || draftRecord.Answers.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(draftRecord.Csp))
        {
            throw new AnswerAlreadySignedException();
        }

        if (_answerDataService.UpdateDraftSignature(surveyId, organizationId, signature, signedContent))
        {
            return true;
        }

        var updatedDraftRecord = _answerDataService.GetDraftRecord(surveyId, organizationId);
        if (!string.IsNullOrWhiteSpace(updatedDraftRecord?.Csp))
        {
            throw new AnswerAlreadySignedException();
        }

        return false;
    }

    private static string NormalizeBase64Payload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        return string.Concat(payload.Where(character => !char.IsWhiteSpace(character)));
    }

    private static byte[] DecodeBase64Payload(string? payload, string fieldName)
    {
        var normalized = NormalizeBase64Payload(payload);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException($"Не передано содержимое для поля \"{fieldName}\".");
        }

        try
        {
            return Convert.FromBase64String(normalized);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException($"Поле \"{fieldName}\" содержит некорректный base64.", exception);
        }
    }
}
