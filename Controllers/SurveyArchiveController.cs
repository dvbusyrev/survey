using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;
using MainProject.Web.ViewModels;

[Authorize]
public class SurveyArchiveController : Controller
{
    private readonly SurveyService _surveyService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SurveyArchiveController> _logger;

    public SurveyArchiveController(
        SurveyService surveyService,
        ICurrentUserService currentUserService,
        ILogger<SurveyArchiveController> logger)
    {
        _surveyService = surveyService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private async Task<IActionResult> RenderAdminArchivePageAsync(
        int currentPage = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        string? surveyIds = null,
        string? year = null,
        string? month = null,
        string? dateFrom = null,
        string? dateTo = null,
        SurveyEditPageViewModel? editSurveyPage = null,
        CancellationToken cancellationToken = default)
    {
        var pageModel = await _surveyService.GetAdminArchivedSurveysPageAsync(
            currentPage,
            sortBy,
            sortDirection,
            organizationIds,
            surveyIds,
            year,
            month,
            dateFrom,
            dateTo,
            cancellationToken);

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

            return Redirect("/survey");
        }

        if (_currentUserService.UserId != requestedUserId)
        {
            return Forbid();
        }

        return null;
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("survey/archive")]
    [HttpGet("surveys/archive")]
    public async Task<IActionResult> ArchivedSurveys(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        string? surveyIds = null,
        string? year = null,
        string? month = null,
        string? dateFrom = null,
        string? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        return await RenderAdminArchivePageAsync(
            page,
            sortBy,
            sortDirection,
            organizationIds,
            surveyIds,
            year,
            month,
            dateFrom,
            dateTo,
            cancellationToken: cancellationToken);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("survey/archive/{id:int}/edit")]
    [HttpGet("surveys/archive/{id:int}/edit")]
    public async Task<IActionResult> ArchivedSurveyEdit(
        int id,
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        string? surveyIds = null,
        string? year = null,
        string? month = null,
        string? dateFrom = null,
        string? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pageModel = await _surveyService.GetSurveyEditPageAsync(id, cancellationToken);
            if (pageModel == null)
            {
                return NotFound("Анкета не найдена");
            }
            return await RenderAdminArchivePageAsync(
                page,
                sortBy,
                sortDirection,
                organizationIds,
                surveyIds,
                year,
                month,
                dateFrom,
                dateTo,
                pageModel,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить анкету.", $"Ошибка при открытии архивной анкеты {id} для редактирования");
        }
    }

    [HttpGet("archive")]
    [HttpGet("my-surveys/archive")]
    public async Task<IActionResult> ArchivedSurveysForUser(CancellationToken cancellationToken = default)
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

        var pageModel = await _surveyService.GetUserArchivePageAsync(
            _currentUserService.UserId.Value,
            1,
            searchTerm: null,
            date: null,
            dateFrom: null,
            dateTo: null,
            signedOnly: false,
            cancellationToken: cancellationToken);

        if (pageModel == null)
        {
            return NotFound(new { error = "Клиент не найден" });
        }

        var activePageModel = await _surveyService.GetActiveSurveysPageAsync(
            _currentUserService.UserId.Value,
            1,
            searchTerm: null,
            cancellationToken);

        ViewBag.ActiveTabContentModel = BuildActiveContentModel(
            activePageModel,
            pageModel.TotalCount);

        return View("~/Web/Views/Survey/archived_surveys_for_user.cshtml", pageModel);
    }

    [HttpGet("archive/{id:int}")]
    [HttpGet("my-surveys/archive/{id:int}")]
    public async Task<IActionResult> GetListArchive(
        int id,
        int? page,
        string searchTerm = "",
        string date = "",
        string dateFrom = "",
        string dateTo = "",
        bool signedOnly = false,
        bool countOnly = false,
        CancellationToken cancellationToken = default)
    {
        var accessResult = EnsureUserRouteAccess(id);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var pageModel = await _surveyService.GetUserArchivePageAsync(
                id,
                page ?? 1,
                searchTerm,
                date,
                dateFrom,
                dateTo,
                signedOnly,
                cancellationToken);

            if (pageModel == null)
            {
                return NotFound(new { error = "Клиент не найден" });
            }

            if (countOnly)
            {
                return Ok(new { totalCount = pageModel.TotalCount });
            }

            var activePageModel = await _surveyService.GetActiveSurveysPageAsync(
                id,
                1,
                searchTerm: null,
                cancellationToken);

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
            return this.SafeError(ex, "Не удалось загрузить архив анкет.", $"Ошибка при получении архивных анкет пользователя {id}");
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
    [HttpPost("survey/archive/copy")]
    [HttpPost("surveys/archive/copy")]
    public async Task<IActionResult> CopyArchivedSurvey([FromBody] ArchiveSurveyCopyRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.SurveyId <= 0)
        {
            return BadRequest("Идентификатор архивной анкеты обязателен.");
        }

        try
        {
            var id = await _surveyService.CopyArchiveSurveyAsync(request, cancellationToken);
            return Ok(new
            {
                message = "Анкета успешно добавлена",
                id
            });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось скопировать анкету.", $"Ошибка при копировании архивной анкеты {request.SurveyId}");
        }
    }
}
