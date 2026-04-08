using MainProject.Services;

namespace MainProject.Services.Answers;

public sealed class AnswerAccessService
{
    private readonly AnswerDataService _answerDataService;
    private readonly CurrentUserService _currentUserService;

    public AnswerAccessService(AnswerDataService answerDataService, CurrentUserService currentUserService)
    {
        _answerDataService = answerDataService;
        _currentUserService = currentUserService;
    }

    public bool IsAuthenticated => _currentUserService.IsAuthenticated;
    public bool IsAdmin => _currentUserService.IsAdmin;
    public int? UserId => _currentUserService.UserId;

    public int? GetCurrentUserOrganizationId()
    {
        if (!UserId.HasValue)
        {
            return null;
        }

        return _answerDataService.GetUserOrganizationId(UserId.Value);
    }

    public bool CanAccessOrganization(int requestedOrganizationId)
    {
        if (IsAdmin)
        {
            return true;
        }

        var currentOrganizationId = GetCurrentUserOrganizationId();
        return currentOrganizationId.HasValue && currentOrganizationId.Value == requestedOrganizationId;
    }

    public bool CanSubmitAnswer(int surveyId, int requestedOrganizationId)
    {
        if (!CanAccessOrganization(requestedOrganizationId))
        {
            return false;
        }

        return IsAdmin || _answerDataService.IsSurveyAssignedToOrganization(surveyId, requestedOrganizationId);
    }

    public bool CanAccessAnswerRecord(int surveyId, int requestedOrganizationId)
    {
        if (!CanAccessOrganization(requestedOrganizationId))
        {
            return false;
        }

        return IsAdmin || _answerDataService.AnswerRecordExists(surveyId, requestedOrganizationId);
    }
}
