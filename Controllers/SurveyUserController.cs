using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Web.Infrastructure;
using MainProject.Web.ViewModels;

[Authorize]
public class SurveyUserController : Controller
{
    private readonly ISurveyUserService _surveyUserService;
    private readonly ISurveyAdminService _surveyAdminService;
    private readonly IAnswerWorkflowService _answerWorkflowService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SurveyUserController> _logger;

    public SurveyUserController(
        ISurveyUserService surveyUserService,
        ISurveyAdminService surveyAdminService,
        IAnswerWorkflowService answerWorkflowService,
        ICurrentUserService currentUserService,
        ILogger<SurveyUserController> logger)
    {
        _surveyUserService = surveyUserService;
        _surveyAdminService = surveyAdminService;
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

            return Redirect("/survey");
        }

        if (_currentUserService.UserId != requestedUserId)
        {
            return Forbid();
        }

        return null;
    }

    private async Task<IActionResult?> EnsureOrganizationAccessAsync(
        int requestedOrganizationId,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.IsAdmin)
        {
            return null;
        }

        if (!_currentUserService.UserId.HasValue)
        {
            return Challenge();
        }

        var currentOrganizationId = await _surveyUserService.GetUserOrganizationIdAsync(
            _currentUserService.UserId.Value,
            cancellationToken);
        if (!currentOrganizationId.HasValue || currentOrganizationId.Value != requestedOrganizationId)
        {
            return Forbid();
        }

        return null;
    }

    [HttpGet("survey")]
    public async Task<IActionResult> Survey(
        int? page,
        string? searchTerm,
        string? date,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        CancellationToken cancellationToken = default)
    {
        if (_currentUserService.IsAdmin)
        {
            return View(
                "~/Web/Views/Survey/get_surveys.cshtml",
                await BuildAdminSurveyListPageAsync(page ?? 1, sortBy, sortDirection, organizationIds, cancellationToken));
        }

        if (!_currentUserService.UserId.HasValue)
        {
            return Challenge();
        }

        return await RenderSurveyListPageAsync(_currentUserService.UserId.Value, page, searchTerm, date, cancellationToken);
    }

    [HttpGet("my-surveys")]
    public async Task<IActionResult> MySurveys(
        int? page,
        string? searchTerm,
        string? date,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Challenge();
        }

        return await RenderSurveyListPageAsync(_currentUserService.UserId.Value, page, searchTerm, date, cancellationToken);
    }

    private async Task<SurveyListPageViewModel> BuildAdminSurveyListPageAsync(
        int currentPage = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        CancellationToken cancellationToken = default)
    {
        var pageModel = await _surveyAdminService.GetSurveysPageAsync(
            currentPage,
            sortBy,
            sortDirection,
            organizationIds,
            cancellationToken);

        return new SurveyListPageViewModel
        {
            SurveyRows = pageModel.SurveyRows,
            CurrentPage = pageModel.CurrentPage,
            TotalPages = pageModel.TotalPages,
            TotalCount = pageModel.TotalCount,
            PageSize = pageModel.PageSize,
            HasExplicitSort = pageModel.HasExplicitSort,
            SortBy = pageModel.SortBy,
            SortDirection = pageModel.SortDirection,
            FilterState = pageModel.FilterState
        };
    }

    private async Task<IActionResult> RenderSurveyListPageAsync(
        int id,
        int? page,
        string? searchTerm,
        string? date,
        CancellationToken cancellationToken)
    {
        var accessResult = EnsureUserRouteAccess(id);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var pageModel = await _surveyUserService.GetActiveSurveysPageAsync(
                id, page ?? 1, searchTerm, cancellationToken);
            if (pageModel == null)
            {
                return NotFound(new { error = "Клиент не найден" });
            }

            var activeContentModel = BuildActiveContentModel(pageModel, archivedCount: 0);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("~/Web/Views/Survey/Partials/_UserSurveyPageContent.cshtml", activeContentModel);
            }

            return View("~/Web/Views/Survey/survey_list_user.cshtml", pageModel);
        }
        catch (Exception ex)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return this.SafeError(ex, "Не удалось загрузить доступные анкеты.", $"Ошибка при загрузке доступных анкет пользователя {id}");
            }

            return this.SafeErrorView(ex, "Не удалось загрузить доступные анкеты.", $"Ошибка при загрузке доступных анкет пользователя {id}");
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

    [HttpGet("survey/{id:int}/organizations/{organizationId:int}/questions")]
    [HttpGet("surveys/{id:int}/organizations/{organizationId:int}/questions")]
    public async Task<IActionResult> GetSurveyQuestions(
        int id,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await EnsureSurveyAccessAsync(id, organizationId, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        var questions = await _surveyUserService.GetSurveyQuestionsAsync(id, cancellationToken);
        return Json(new { questions });
    }

    [HttpGet("survey/{id:int}/organizations/{organizationId:int}/fill-content")]
    [HttpGet("surveys/{id:int}/organizations/{organizationId:int}/fill-content")]
    public async Task<IActionResult> GetSurveyFillContent(
        int id,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await EnsureSurveyAccessAsync(id, organizationId, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        var survey = await _surveyUserService.GetSurveyInfoAsync(id, cancellationToken);
        if (survey == null)
        {
            return NotFound("Анкета не найдена.");
        }

        var model = new UserSurveyFillContentViewModel
        {
            Survey = survey,
            OrganizationId = organizationId,
            Questions = await _surveyUserService.GetSurveyQuestionsAsync(id, cancellationToken),
            DraftAnswer = await _answerWorkflowService.GetDraftAnswerAsync(id, organizationId, cancellationToken)
        };

        return PartialView("~/Web/Views/Survey/Partials/_UserSurveyFillContent.cshtml", model);
    }

    private async Task<IActionResult?> EnsureSurveyAccessAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken)
    {
        var organizationAccessResult = await EnsureOrganizationAccessAsync(organizationId, cancellationToken);
        if (organizationAccessResult != null)
        {
            return organizationAccessResult;
        }

        if (_currentUserService.IsAdmin)
        {
            return null;
        }

        if (!await _surveyUserService.IsSurveyAssignedToOrganizationAsync(surveyId, organizationId, cancellationToken))
        {
            return Forbid();
        }

        return null;
    }
}
