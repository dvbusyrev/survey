namespace MainProject.Application.Contracts;

public interface IAnswerSigningService
{
    string GetSigningData(int surveyId, int organizationId);
    bool SaveSignature(int surveyId, int organizationId, string signature);
}
