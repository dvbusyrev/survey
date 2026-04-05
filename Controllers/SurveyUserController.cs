using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Services.Surveys;
using main_project.Services;

[Authorize]
public class SurveyUserController : Controller
{
    private readonly SurveyUserService _surveyUserService;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<SurveyUserController> _logger;

    public SurveyUserController(
        SurveyUserService surveyUserService,
        CurrentUserService currentUserService,
        ILogger<SurveyUserController> logger)
    {
        _surveyUserService = surveyUserService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private IActionResult? EnsureUserRouteAccess(int requestedUserId)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return Challenge();
        }

        if (!_currentUserService.IsAdmin && _currentUserService.UserId != requestedUserId)
        {
            return Forbid();
        }

        return null;
    }

    private IActionResult? EnsureOmsuAccess(int requestedOmsuId)
    {
        if (_currentUserService.IsAdmin)
        {
            return null;
        }

        if (!_currentUserService.UserId.HasValue)
        {
            return Challenge();
        }

        var currentOmsuId = _surveyUserService.GetUserOmsuId(_currentUserService.UserId.Value);
        if (!currentOmsuId.HasValue || currentOmsuId.Value != requestedOmsuId)
        {
            return Forbid();
        }

        return null;
    }

    [HttpGet("my-surveys")]
    public IActionResult my_surveys(int? page, string? searchTerm, string? date)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Challenge();
        }

        return RenderSurveyListPage(_currentUserService.UserId.Value, page, searchTerm, date);
    }

    [HttpGet("survey_list_user/{id}")]
    [HttpGet("Survey/survey_list_user/{id}")]
    public IActionResult survey_list_user(int id, int? page, string? searchTerm, string? date)
    {
        return RenderSurveyListPage(id, page, searchTerm, date);
    }

    private IActionResult RenderSurveyListPage(int id, int? page, string? searchTerm, string? date)
    {
        var accessResult = EnsureUserRouteAccess(id);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var pageModel = _surveyUserService.GetActiveSurveysPage(id, page ?? 1, searchTerm);
            if (pageModel == null)
            {
                return NotFound(new { error = "Пользователь не найден" });
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    accessibleSurveys = pageModel.AccessibleSurveys,
                    currentPage = pageModel.CurrentPage,
                    totalPages = pageModel.TotalPages,
                    totalCount = pageModel.TotalCount,
                    searchTerm = pageModel.SearchTerm
                });
            }

            return View("~/Views/Survey/survey_list_user.cshtml", pageModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в survey_list_user для пользователя {UserId}", id);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return StatusCode(500, new { error = "Ошибка сервера" });
            }

            throw;
        }
    }

    [HttpGet("surveys/{id:int}/organizations/{omsuId:int}/questions")]
    [HttpGet("zapolnenie_anketi/{id:int}/{omsuId:int}")]
    [HttpGet("Survey/zapolnenie_anketi/{id:int}/{omsuId:int}")]
    public IActionResult zapolnenie_anketi(int id, int omsuId)
    {
        var accessResult = EnsureOmsuAccess(omsuId);
        if (accessResult != null)
        {
            return accessResult;
        }

        var questions = _surveyUserService.GetSurveyQuestions(id);
        return Json(new { questions });
    }
}
