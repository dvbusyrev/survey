using main_project.Services;

namespace main_project.Services.Answers;

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

    public int? GetCurrentUserOmsuId()
    {
        if (!UserId.HasValue)
        {
            return null;
        }

        return _answerDataService.GetUserOmsuId(UserId.Value);
    }

    public bool CanAccessOmsu(int requestedOmsuId)
    {
        if (IsAdmin)
        {
            return true;
        }

        var currentOmsuId = GetCurrentUserOmsuId();
        return currentOmsuId.HasValue && currentOmsuId.Value == requestedOmsuId;
    }
}
