namespace MainProject.Services.Answers;

public sealed class AnswerSigningService
{
    private readonly AnswerDataService _answerDataService;

    public AnswerSigningService(AnswerDataService answerDataService)
    {
        _answerDataService = answerDataService;
    }

    public string GetSigningData(int surveyId, int organizationId)
    {
        return $"Данные для подписи анкеты {surveyId} организации {organizationId}";
    }

    public bool SaveSignature(int surveyId, int organizationId, string signature)
    {
        return _answerDataService.UpdateSignature(surveyId, organizationId, signature);
    }
}
