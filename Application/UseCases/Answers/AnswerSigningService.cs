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

    public bool SaveSignature(int surveyId, int organizationId, string signature)
    {
        return _answerDataService.UpdateSignature(surveyId, organizationId, signature);
    }
}
