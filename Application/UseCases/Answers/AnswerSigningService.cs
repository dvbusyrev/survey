using System.Globalization;
using MainProject.Application.DTO;
using MainProject.Application.Contracts;

namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerSigningService : IAnswerSigningService
{
    private readonly AnswerDataService _answerDataService;
    private readonly IClock _clock;

    public AnswerSigningService(AnswerDataService answerDataService, IClock clock)
    {
        _answerDataService = answerDataService;
        _clock = clock;
    }

    public async Task<AnswerSigningPayload> GetSigningDataAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        var survey = await _answerDataService.GetSurveyInfoAsync(surveyId, cancellationToken)
            ?? throw new InvalidOperationException("Анкета для подписи не найдена.");
        var answerRecords = (await _answerDataService.GetAnswerRecordsAsync(
            surveyId, organizationId, cancellationToken)).ToList();
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

    public async Task<bool> SaveSignatureAsync(
        int surveyId,
        int organizationId,
        AnswerSignatureSaveRequest request,
        CancellationToken cancellationToken = default)
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

        var answerRecord = await _answerDataService.GetAnswerRecordAsync(surveyId, organizationId, cancellationToken);
        if (answerRecord == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(answerRecord.Csp))
        {
            throw new AnswerAlreadySignedException();
        }

        if (await _answerDataService.UpdateSignatureAsync(
                surveyId, organizationId, signature, signedContent, cancellationToken))
        {
            return true;
        }

        var updatedAnswerRecord = await _answerDataService.GetAnswerRecordAsync(surveyId, organizationId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(updatedAnswerRecord?.Csp))
        {
            throw new AnswerAlreadySignedException();
        }

        return false;
    }

    public async Task<AnswerSigningPayload> GetDraftSigningDataAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        var survey = await _answerDataService.GetSurveyInfoAsync(surveyId, cancellationToken)
            ?? throw new InvalidOperationException("Анкета для подписи не найдена.");
        var draftRecord = await _answerDataService.GetDraftRecordAsync(surveyId, organizationId, cancellationToken);
        if (draftRecord == null || draftRecord.Answers.Count == 0)
        {
            throw new InvalidOperationException("Черновик не содержит ответов для подписи.");
        }

        if (!string.IsNullOrWhiteSpace(draftRecord.Csp))
        {
            throw new AnswerAlreadySignedException();
        }

        draftRecord.CompletionDate ??= _clock.Now;
        var pdfBytes = AnswerPdfDocumentBuilder.BuildPdfContent(survey, new[] { draftRecord });
        return new AnswerSigningPayload
        {
            Content = Convert.ToBase64String(pdfBytes),
            ContentEncoding = "base64",
            Detached = true,
            FileName = $"{survey.NameSurvey ?? "Анкета"}_{surveyId.ToString(CultureInfo.InvariantCulture)}_draft.pdf"
        };
    }

    public async Task<bool> SaveDraftSignatureAsync(
        int surveyId,
        int organizationId,
        AnswerSignatureSaveRequest request,
        CancellationToken cancellationToken = default)
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

        var draftRecord = await _answerDataService.GetDraftRecordAsync(surveyId, organizationId, cancellationToken);
        if (draftRecord == null || draftRecord.Answers.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(draftRecord.Csp))
        {
            throw new AnswerAlreadySignedException();
        }

        if (await _answerDataService.UpdateDraftSignatureAsync(
                surveyId, organizationId, signature, signedContent, cancellationToken))
        {
            return true;
        }

        var updatedDraftRecord = await _answerDataService.GetDraftRecordAsync(surveyId, organizationId, cancellationToken);
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
