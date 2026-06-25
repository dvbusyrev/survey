namespace MainProject.Application.Contracts;

public interface IAnswerAccessService
{
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    int? UserId { get; }
    Task<int?> GetCurrentUserOrganizationIdAsync(CancellationToken cancellationToken = default);
    Task<bool> CanAccessOrganizationAsync(int requestedOrganizationId, CancellationToken cancellationToken = default);
    Task<bool> CanSubmitAnswerAsync(int surveyId, int requestedOrganizationId, CancellationToken cancellationToken = default);
    Task<bool> CanAccessAnswerRecordAsync(int surveyId, int requestedOrganizationId, CancellationToken cancellationToken = default);
}
