namespace main_project.Services.Answers;

public sealed class AnswerSigningService
{
    private readonly AnswerDataService _answerDataService;

    public AnswerSigningService(AnswerDataService answerDataService)
    {
        _answerDataService = answerDataService;
    }

    public string GetSigningData(int surveyId, int omsuId)
    {
        return $"Данные для подписи анкеты {surveyId} организации {omsuId}";
    }

    public bool SaveSignature(int surveyId, int omsuId, string signature)
    {
        return _answerDataService.UpdateSignature(surveyId, omsuId, signature);
    }
}
