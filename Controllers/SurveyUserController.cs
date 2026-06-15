using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Web.ViewModels;

[Authorize]
public class SurveyUserController : Controller
{
    private readonly ISurveyUserService _surveyUserService;
    private readonly ISurveyArchiveService _surveyArchiveService;
    private readonly IAnswerWorkflowService _answerWorkflowService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SurveyUserController> _logger;

    public SurveyUserController(
        ISurveyUserService surveyUserService,
        ISurveyArchiveService surveyArchiveService,
        IAnswerWorkflowService answerWorkflowService,
        ICurrentUserService currentUserService,
        ILogger<SurveyUserController> logger)
    {
        _surveyUserService = surveyUserService;
        _surveyArchiveService = surveyArchiveService;
        _answerWorkflowService = answerWorkflowService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private IActionResult? EnsureUserRouteAccess(int requestedUserId)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return Challenge();
        }

        if (_currentUserService.IsAdmin)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return StatusCode(403, new { error = "Раздел активных анкет доступен только пользователям." });
            }

            return Redirect("/surveys");
        }

        if (_currentUserService.UserId != requestedUserId)
        {
            return Forbid();
        }

        return null;
    }

    private IActionResult? EnsureOrganizationAccess(int requestedOrganizationId)
    {
        if (_currentUserService.IsAdmin)
        {
            return null;
        }

        if (!_currentUserService.UserId.HasValue)
        {
            return Challenge();
        }

        var currentOrganizationId = _surveyUserService.GetUserOrganizationId(_currentUserService.UserId.Value);
        if (!currentOrganizationId.HasValue || currentOrganizationId.Value != requestedOrganizationId)
        {
            return Forbid();
        }

        return null;
    }

    [HttpGet("my-surveys")]
    public IActionResult MySurveys(int? page, string? searchTerm, string? date)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Challenge();
        }

        return RenderSurveyListPage(_currentUserService.UserId.Value, page, searchTerm, date);
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
                return NotFound(new { error = "Клиент не найден" });
            }

            var archivedPageModel = _surveyArchiveService.GetUserArchivePage(
                id,
                1,
                searchTerm: null,
                date: null,
                dateFrom: null,
                dateTo: null,
                signedOnly: false);

            var activeContentModel = BuildActiveContentModel(
                pageModel,
                archivedPageModel?.TotalCount ?? 0);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("~/Web/Views/Survey/Partials/_UserSurveyPageContent.cshtml", activeContentModel);
            }

            ViewBag.ArchivedTabContentModel = BuildArchivedContentModel(
                archivedPageModel,
                pageModel.TotalCount);

            return View("~/Web/Views/Survey/survey_list_user.cshtml", pageModel);
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

    private static UserSurveyPageContentViewModel BuildActiveContentModel(
        UserSurveyListPageViewModel pageModel,
        int archivedCount)
    {
        return new UserSurveyPageContentViewModel
        {
            Surveys = pageModel.AccessibleSurveys,
            ActiveTab = "active",
            CurrentPage = pageModel.CurrentPage,
            TotalPages = pageModel.TotalPages,
            ActiveCount = pageModel.TotalCount,
            ArchivedCount = archivedCount,
            SearchTerm = pageModel.SearchTerm
        };
    }

    private static UserSurveyPageContentViewModel BuildArchivedContentModel(
        UserSurveyArchivePageViewModel? pageModel,
        int activeCount)
    {
        return new UserSurveyPageContentViewModel
        {
            Surveys = pageModel?.ArchivedSurveys ?? Array.Empty<MainProject.Domain.Entities.Survey>(),
            ActiveTab = "archived",
            CurrentPage = pageModel?.CurrentPage ?? 1,
            TotalPages = pageModel?.TotalPages ?? 1,
            ActiveCount = activeCount,
            ArchivedCount = pageModel?.TotalCount ?? 0,
            SearchTerm = pageModel?.SearchTerm ?? string.Empty,
            SignedOnly = pageModel?.SignedOnly ?? false
        };
    }

    [HttpGet("surveys/{id:int}/organizations/{organizationId:int}/questions")]
    public IActionResult GetSurveyQuestions(int id, int organizationId)
    {
        var accessResult = EnsureSurveyAccess(id, organizationId);
        if (accessResult != null)
        {
            return accessResult;
        }

        var questions = _surveyUserService.GetSurveyQuestions(id);
        return Json(new { questions });
    }

    [HttpGet("surveys/{id:int}/organizations/{organizationId:int}/fill-content")]
    public IActionResult GetSurveyFillContent(int id, int organizationId)
    {
        var accessResult = EnsureSurveyAccess(id, organizationId);
        if (accessResult != null)
        {
            return accessResult;
        }

        var survey = _surveyUserService.GetSurveyInfo(id);
        if (survey == null)
        {
            return NotFound("Анкета не найдена.");
        }

        var model = new UserSurveyFillContentViewModel
        {
            Survey = survey,
            OrganizationId = organizationId,
            Questions = _surveyUserService.GetSurveyQuestions(id),
            DraftAnswer = _answerWorkflowService.GetDraftAnswer(id, organizationId)
        };

        return PartialView("~/Web/Views/Survey/Partials/_UserSurveyFillContent.cshtml", model);
    }

    private IActionResult? EnsureSurveyAccess(int surveyId, int organizationId)
    {
        var organizationAccessResult = EnsureOrganizationAccess(organizationId);
        if (organizationAccessResult != null)
        {
            return organizationAccessResult;
        }

        if (_currentUserService.IsAdmin)
        {
            return null;
        }

        if (!_surveyUserService.IsSurveyAssignedToOrganization(surveyId, organizationId))
        {
            return Forbid();
        }

        return null;
    }
}
