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

        return _answerDataService.UpdateSignature(surveyId, organizationId, signature, signedContent);
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
