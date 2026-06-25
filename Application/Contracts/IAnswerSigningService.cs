using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface IAnswerSigningService
{
    Task<AnswerSigningPayload> GetSigningDataAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<bool> SaveSignatureAsync(int surveyId, int organizationId, AnswerSignatureSaveRequest request, CancellationToken cancellationToken = default);
    Task<AnswerSigningPayload> GetDraftSigningDataAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<bool> SaveDraftSignatureAsync(int surveyId, int organizationId, AnswerSignatureSaveRequest request, CancellationToken cancellationToken = default);
}
