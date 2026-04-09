namespace MainProject.Application.Contracts;

public interface IAnswerAccessService
{
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    int? UserId { get; }
    int? GetCurrentUserOrganizationId();
    bool CanAccessOrganization(int requestedOrganizationId);
    bool CanSubmitAnswer(int surveyId, int requestedOrganizationId);
    bool CanAccessAnswerRecord(int surveyId, int requestedOrganizationId);
}
