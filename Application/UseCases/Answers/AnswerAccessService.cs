using MainProject.Application.Contracts;

namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerAccessService : IAnswerAccessService
{
    private readonly AnswerDataService _answerDataService;
    private readonly ICurrentUserService _currentUserService;

    public AnswerAccessService(AnswerDataService answerDataService, ICurrentUserService currentUserService)
    {
        _answerDataService = answerDataService;
        _currentUserService = currentUserService;
    }

    public bool IsAuthenticated => _currentUserService.IsAuthenticated;
    public bool IsAdmin => _currentUserService.IsAdmin;
    public int? UserId => _currentUserService.UserId;

    public async Task<int?> GetCurrentUserOrganizationIdAsync(CancellationToken cancellationToken = default)
    {
        if (!UserId.HasValue)
        {
            return null;
        }

        return await _answerDataService.GetUserOrganizationIdAsync(UserId.Value, cancellationToken);
    }

    public async Task<bool> CanAccessOrganizationAsync(
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

    public async Task<bool> CanSubmitAnswerAsync(
        int surveyId,
        int requestedOrganizationId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessOrganizationAsync(requestedOrganizationId, cancellationToken))
        {
            return false;
        }

        return IsAdmin || await _answerDataService.IsSurveyAssignedToOrganizationAsync(
            surveyId, requestedOrganizationId, cancellationToken);
    }

    public async Task<bool> CanAccessAnswerRecordAsync(
        int surveyId,
        int requestedOrganizationId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessOrganizationAsync(requestedOrganizationId, cancellationToken))
        {
            return false;
        }

        return IsAdmin || await _answerDataService.AnswerRecordExistsAsync(
            surveyId, requestedOrganizationId, cancellationToken);
    }
}
