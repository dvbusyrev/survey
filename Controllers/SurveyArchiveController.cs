using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Security;
using MainProject.Web.ViewModels;

[Authorize]
public class SurveyArchiveController : Controller
{
    private readonly ISurveyArchiveService _surveyArchiveService;
    private readonly ISurveyAdminService _surveyAdminService;
    private readonly ISurveyUserService _surveyUserService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SurveyArchiveController> _logger;

    public SurveyArchiveController(
        ISurveyArchiveService surveyArchiveService,
        ISurveyAdminService surveyAdminService,
        ISurveyUserService surveyUserService,
        ICurrentUserService currentUserService,
        ILogger<SurveyArchiveController> logger)
    {
        _surveyArchiveService = surveyArchiveService;
        _surveyAdminService = surveyAdminService;
        _surveyUserService = surveyUserService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private IActionResult RenderAdminArchivePage(
        int currentPage = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        string? surveyIds = null,
        string? year = null,
        string? month = null,
        string? dateFrom = null,
        string? dateTo = null,
        SurveyEditPageViewModel? editSurveyPage = null)
    {
        var pageModel = _surveyArchiveService.GetAdminArchivedSurveysPage(
            currentPage,
            sortBy,
            sortDirection,
            organizationIds,
            surveyIds,
            year,
            month,
            dateFrom,
            dateTo);

        return View(
            "~/Web/Views/Survey/archived_surveys.cshtml",
            new MainProject.Web.ViewModels.SurveyArchivePageViewModel
            {
                SurveyRows = pageModel.SurveyRows,
                CurrentPage = pageModel.CurrentPage,
                TotalPages = pageModel.TotalPages,
                TotalCount = pageModel.TotalCount,
                PageSize = pageModel.PageSize,
                HasExplicitSort = pageModel.HasExplicitSort,
                SortBy = pageModel.SortBy,
                SortDirection = pageModel.SortDirection,
                FilterState = pageModel.FilterState,
                EditSurveyPage = editSurveyPage
            });
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
                return StatusCode(403, new { error = "Раздел архива анкет доступен только пользователям." });
            }

            return Redirect("/surveys");
        }

        if (_currentUserService.UserId != requestedUserId)
        {
            return Forbid();
        }

        return null;
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("surveys/archive")]
    public IActionResult ArchivedSurveys(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        string? surveyIds = null,
        string? year = null,
        string? month = null,
        string? dateFrom = null,
        string? dateTo = null)
    {
        return RenderAdminArchivePage(
            page,
            sortBy,
            sortDirection,
            organizationIds,
            surveyIds,
            year,
            month,
            dateFrom,
            dateTo);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("surveys/archive/{id:int}/edit")]
    public IActionResult ArchivedSurveyEdit(
        int id,
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        string? surveyIds = null,
        string? year = null,
        string? month = null,
        string? dateFrom = null,
        string? dateTo = null)
    {
        try
        {
            var pageModel = _surveyAdminService.GetSurveyEditPage(id);
            if (pageModel == null)
            {
                return NotFound("Анкета не найдена");
            }
            return RenderAdminArchivePage(
                page,
                sortBy,
                sortDirection,
                organizationIds,
                surveyIds,
                year,
                month,
                dateFrom,
                dateTo,
                pageModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при открытии архивной анкеты {SurveyId} для редактирования", id);
            return StatusCode(500, "Произошла ошибка при загрузке анкеты");
        }
    }

    [HttpGet("my-surveys/archive")]
    public IActionResult ArchivedSurveysForUser()
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Challenge();
        }

        var accessResult = EnsureUserRouteAccess(_currentUserService.UserId.Value);
        if (accessResult != null)
        {
            return accessResult;
        }

        var pageModel = _surveyArchiveService.GetUserArchivePage(
            _currentUserService.UserId.Value,
            1,
            searchTerm: null,
            date: null,
            dateFrom: null,
            dateTo: null,
            signedOnly: false);

        if (pageModel == null)
        {
            return NotFound(new { error = "Клиент не найден" });
        }

        var activePageModel = _surveyUserService.GetActiveSurveysPage(
            _currentUserService.UserId.Value,
            1,
            searchTerm: null);

        ViewBag.ActiveTabContentModel = BuildActiveContentModel(
            activePageModel,
            pageModel.TotalCount);

        return View("~/Web/Views/Survey/archived_surveys_for_user.cshtml", pageModel);
    }

    [HttpGet("my-surveys/archive/{id:int}")]
    public IActionResult GetListArchive(
        int id,
        int? page,
        string searchTerm = "",
        string date = "",
        string dateFrom = "",
        string dateTo = "",
        bool signedOnly = false,
        bool countOnly = false)
    {
        var accessResult = EnsureUserRouteAccess(id);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var pageModel = _surveyArchiveService.GetUserArchivePage(
                id,
                page ?? 1,
                searchTerm,
                date,
                dateFrom,
                dateTo,
                signedOnly);

            if (pageModel == null)
            {
                return NotFound(new { error = "Клиент не найден" });
            }

            if (countOnly)
            {
                return Ok(new { totalCount = pageModel.TotalCount });
            }

            var activePageModel = _surveyUserService.GetActiveSurveysPage(
                id,
                1,
                searchTerm: null);

            var archiveContentModel = BuildArchivedContentModel(
                pageModel,
                activePageModel?.TotalCount ?? 0);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("~/Web/Views/Survey/Partials/_UserSurveyPageContent.cshtml", archiveContentModel);
            }

            ViewBag.ActiveTabContentModel = BuildActiveContentModel(
                activePageModel,
                pageModel.TotalCount);

            return View("~/Web/Views/Survey/archived_surveys_for_user.cshtml", pageModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении архивных анкет пользователя {UserId}", id);
            return StatusCode(500, new
            {
                error = "Внутренняя ошибка сервера",
                details = ex.Message
            });
        }
    }

    private static UserSurveyPageContentViewModel BuildActiveContentModel(
        UserSurveyListPageViewModel? pageModel,
        int archivedCount)
    {
        return new UserSurveyPageContentViewModel
        {
            Surveys = pageModel?.AccessibleSurveys ?? Array.Empty<MainProject.Domain.Entities.Survey>(),
            ActiveTab = "active",
            CurrentPage = pageModel?.CurrentPage ?? 1,
            TotalPages = pageModel?.TotalPages ?? 1,
            ActiveCount = pageModel?.TotalCount ?? 0,
            ArchivedCount = archivedCount,
            SearchTerm = pageModel?.SearchTerm ?? string.Empty
        };
    }

    private static UserSurveyPageContentViewModel BuildArchivedContentModel(
        UserSurveyArchivePageViewModel pageModel,
        int activeCount)
    {
        return new UserSurveyPageContentViewModel
        {
            Surveys = pageModel.ArchivedSurveys,
            ActiveTab = "archived",
            CurrentPage = pageModel.CurrentPage,
            TotalPages = pageModel.TotalPages,
            ActiveCount = activeCount,
            ArchivedCount = pageModel.TotalCount,
            SearchTerm = pageModel.SearchTerm,
            SignedOnly = pageModel.SignedOnly
        };
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost("surveys/archive/copy")]
    public async Task<IActionResult> CopyArchivedSurvey([FromBody] ArchiveSurveyCopyRequest request)
    {
        if (request == null || request.SurveyId <= 0)
        {
            return BadRequest("Идентификатор архивной анкеты обязателен.");
        }

        try
        {
            var id = await _surveyArchiveService.CopyArchiveSurveyAsync(request);
            return Ok(new
            {
                message = "Анкета успешно добавлена",
                id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при копировании архивной анкеты {SurveyId}", request.SurveyId);
            return StatusCode(500, $"Ошибка при добавлении анкеты: {ex.Message}");
        }
    }
}
