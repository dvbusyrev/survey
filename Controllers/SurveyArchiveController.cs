using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Services.Surveys;
using main_project.Infrastructure.Security;
using main_project.Services;

[Authorize]
public class SurveyArchiveController : Controller
{
    private readonly SurveyArchiveService _surveyArchiveService;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<SurveyArchiveController> _logger;

    public SurveyArchiveController(
        SurveyArchiveService surveyArchiveService,
        CurrentUserService currentUserService,
        ILogger<SurveyArchiveController> logger)
    {
        _surveyArchiveService = surveyArchiveService;
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

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("surveys/archive")]
    [HttpGet("archiv_surveys")]
    [HttpGet("Survey/archiv_surveys")]
    public IActionResult archiv_surveys()
    {
        return View(
            "~/Views/Survey/archiv_surveys.cshtml",
            _surveyArchiveService.GetAdminArchiveSurveys());
    }

    [HttpGet("archiv_surveys_for_user")]
    [HttpGet("Survey/archiv_surveys_for_user")]
    [HttpGet("my-surveys/archive")]
    public IActionResult archiv_surveys_for_user()
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Challenge();
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
            return NotFound(new { error = "Пользователь не найден" });
        }

        return View("~/Views/Survey/archiv_surveys_for_user.cshtml", pageModel);
    }

    [HttpGet("my-surveys/archive/{id:int}")]
    [HttpGet("get_list_archive/{id}")]
    [HttpGet("Survey/get_list_archive/{id}")]
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
                return NotFound(new { error = "Пользователь не найден" });
            }

            if (countOnly)
            {
                return Ok(new { totalCount = pageModel.TotalCount });
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new
                {
                    accessibleSurveys = pageModel.ArchivedSurveys,
                    currentPage = pageModel.CurrentPage,
                    totalPages = pageModel.TotalPages,
                    totalCount = pageModel.TotalCount,
                    searchTerm = pageModel.SearchTerm,
                    dateFrom = pageModel.DateFrom,
                    dateTo = pageModel.DateTo,
                    signedOnly = pageModel.SignedOnly
                });
            }

            return View("~/Views/Survey/archiv_surveys_for_user.cshtml", pageModel);
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

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost("surveys/archive/copy")]
    [HttpPost("copy_archive_survey")]
    [HttpPost("Survey/copy_archive_survey")]
    public async Task<IActionResult> copy_archive_survey([FromBody] ArchiveSurveyCopyRequest request)
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
