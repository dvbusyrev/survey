using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface IAnswerSigningService
{
    AnswerSigningPayload GetSigningData(int surveyId, int organizationId);
    bool SaveSignature(int surveyId, int organizationId, string signature);
}
