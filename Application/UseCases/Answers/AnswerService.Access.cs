using MainProject.Application.Contracts;

namespace MainProject.Application.UseCases.Answers;

public partial class AnswerService
{
    public virtual bool IsAuthenticated => _currentUserService.IsAuthenticated;
    public virtual bool IsAdmin => _currentUserService.IsAdmin;
    public virtual int? UserId => _currentUserService.UserId;

    public virtual async Task<int?> GetCurrentUserOrganizationIdAsync(CancellationToken cancellationToken = default)
    {
        if (!UserId.HasValue)
        {
            return null;
        }

        return await GetUserOrganizationIdAsync(UserId.Value, cancellationToken);
    }

    public virtual async Task<bool> CanAccessOrganizationAsync(
        int requestedOrganizationId,
        CancellationToken cancellationToken = default)
    {
        if (IsAdmin)
        {
            return true;
        }

        var currentOrganizationId = await GetCurrentUserOrganizationIdAsync(cancellationToken);
        return currentOrganizationId.HasValue && currentOrganizationId.Value == requestedOrganizationId;
    }

    public virtual async Task<bool> CanSubmitAnswerAsync(
        int surveyId,
        int requestedOrganizationId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessOrganizationAsync(requestedOrganizationId, cancellationToken))
        {
            return false;
        }

        return IsAdmin || await IsSurveyAssignedToOrganizationAsync(
            surveyId, requestedOrganizationId, cancellationToken);
    }

    public virtual async Task<bool> CanAccessAnswerRecordAsync(
        int surveyId,
        int requestedOrganizationId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessOrganizationAsync(requestedOrganizationId, cancellationToken))
        {
            return false;
        }

        return IsAdmin || await AnswerRecordExistsAsync(
            surveyId, requestedOrganizationId, cancellationToken);
    }
}
