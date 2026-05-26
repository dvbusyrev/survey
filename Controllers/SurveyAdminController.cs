using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Security;
using MainProject.Web.ViewModels;
using Npgsql;

[Authorize(Roles = AppRoles.Admin)]
public class SurveyAdminController : Controller
{
    private readonly ISurveyAdminService _surveyAdminService;
    private readonly ILogger<SurveyAdminController> _logger;

    public SurveyAdminController(ISurveyAdminService surveyAdminService, ILogger<SurveyAdminController> logger)
    {
        _surveyAdminService = surveyAdminService;
        _logger = logger;
    }

    private SurveyListPageViewModel BuildSurveyListPage(
        int currentPage = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        bool openAddSurveyModal = false,
        SurveyEditPageViewModel? editSurveyPage = null)
    {
        var pageModel = _surveyAdminService.GetSurveysPage(
            currentPage,
            sortBy,
            sortDirection,
            organizationIds);

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
            FilterState = pageModel.FilterState,
            OpenAddSurveyModal = openAddSurveyModal,
            EditSurveyPage = editSurveyPage
        };
    }

    [HttpGet("surveys")]
    public IActionResult GetSurveys(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null)
    {
        return View(
            "~/Web/Views/Survey/get_surveys.cshtml",
            BuildSurveyListPage(page, sortBy, sortDirection, organizationIds));
    }

    [HttpGet("surveys/data")]
    public IActionResult GetSurveyOptions()
    {
        var surveys = _surveyAdminService.GetSurveys()
            .Select(survey => new
            {
                id = survey.IdSurvey,
                name = survey.NameSurvey
            })
            .OrderBy(survey => survey.name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(survey => survey.id)
            .ToArray();

        return Json(surveys);
    }

    [HttpGet("surveys/create")]
    public IActionResult AddSurvey(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null)
    {
        return View(
            "~/Web/Views/Survey/get_surveys.cshtml",
            BuildSurveyListPage(page, sortBy, sortDirection, organizationIds, openAddSurveyModal: true));
    }

    [HttpPost("surveys/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSurvey([FromBody] SurveyAddRequest? request)
    {
        try
        {
            var result = await _surveyAdminService.CreateSurveyAsync(request);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                surveyId = result.SurveyId
            });
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Ошибка базы данных при создании анкеты");
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка базы данных: " + ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании анкеты");
            return StatusCode(500, new
            {
                success = false,
                message = "Внутренняя ошибка сервера: " + ex.Message
            });
        }
    }

    [HttpPost("surveys/{id:int}/update")]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateSurvey(int id, [FromBody] SurveyUpdateRequest? model)
    {
        try
        {
            var result = _surveyAdminService.UpdateSurvey(id, model);
            if (!result.Success)
            {
                if (result.NotFound)
                {
                    return NotFound(new { success = false, message = result.Message });
                }

                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                surveyId = result.SurveyId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении анкеты ID: {SurveyId}", id);
            return StatusCode(500, new
            {
                success = false,
                message = "Произошла ошибка при обновлении анкеты",
                error = ex.Message
            });
        }
    }

    [HttpPost("surveys/active/work-period")]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateActiveSurveysWorkPeriod([FromBody] SurveyWorkPeriodRequest? request)
    {
        try
        {
            var result = _surveyAdminService.UpdateActiveSurveysWorkPeriod(request);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении периода работы активных анкет");
            return StatusCode(500, new
            {
                success = false,
                message = "Произошла ошибка при сохранении периода работы",
                error = ex.Message
            });
        }
    }

    [HttpPost("surveys/{id:int}/copy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopySurveySubmission(int id, [FromBody] SurveyCopyRequest? request)
    {
        try
        {
            var result = await _surveyAdminService.CopySurveyAsync(id, request);
            if (!result.Success)
            {
                if (result.NotFound)
                {
                    return NotFound(new { success = false, message = result.Message });
                }

                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                surveyId = result.SurveyId
            });
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Ошибка базы данных при копировании анкеты {Id}", id);
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка базы данных: " + ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при копировании анкеты {Id}", id);
            return StatusCode(500, new
            {
                success = false,
                message = "Внутренняя ошибка сервера: " + ex.Message
            });
        }
    }

    [HttpPost("surveys/{id:int}/delete")]
    public IActionResult DeleteSurvey(int? id, [FromBody] DeleteSurveyRequest? request)
    {
        var surveyId = request?.SurveyId ?? id ?? 0;
        if (surveyId <= 0)
        {
            return BadRequest(new { success = false, message = "Неверный идентификатор анкеты" });
        }

        try
        {
            var surveys = _surveyAdminService.DeleteSurvey(surveyId);
            if (surveys == null)
            {
                return NotFound(new { success = false, message = "Анкета не найдена" });
            }

            return Ok(new
            {
                success = true,
                message = "Анкета успешно удалена",
                surveys
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении анкеты {SurveyId}", surveyId);
            return StatusCode(500, new
            {
                success = false,
                message = "Внутренняя ошибка сервера при удалении анкеты",
                error = ex.Message
            });
        }
    }

    [HttpGet("surveys/{id:int}/edit")]
    public IActionResult UpdateSurveyPage(int id)
    {
        try
        {
            var pageModel = _surveyAdminService.GetSurveyEditPage(id);
            if (pageModel == null)
            {
                return NotFound("Анкета не найдена");
            }

            return View("~/Web/Views/Survey/get_surveys.cshtml", BuildSurveyListPage(editSurveyPage: pageModel));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении анкеты {SurveyId} для редактирования", id);
            return StatusCode(500, "Произошла ошибка при загрузке анкеты");
        }
    }

    [HttpGet("surveys/{id:int}/copy")]
    public IActionResult CopySurvey(int id)
    {
        try
        {
            var survey = _surveyAdminService.GetSurveyForCopy(id);
            if (survey == null)
            {
                return NotFound("Анкета не найдена");
            }

            return View("~/Web/Views/Survey/copy_survey.cshtml", survey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке анкеты для копирования (ID: {SurveyId})", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    [HttpGet("surveys/{id:int}/copy-template")]
    public IActionResult GetSurveyCopyTemplate(int id)
    {
        try
        {
            var pageModel = _surveyAdminService.GetSurveyEditPage(id);
            if (pageModel == null)
            {
                return NotFound(new { success = false, message = "Анкета не найдена." });
            }

            var organizations = pageModel.SelectedOrganizationIds
                .Select((organizationId, index) => new OrganizationSelectionItem
                {
                    Id = organizationId,
                    Name = pageModel.SelectedOrganizationNames.ElementAtOrDefault(index) ?? string.Empty
                })
                .Where(organization => organization.Id > 0 && !string.IsNullOrWhiteSpace(organization.Name))
                .ToArray();

            var response = new SurveyCopyTemplateResponse
            {
                Title = $"{pageModel.Survey.NameSurvey} (Копия)",
                Description = pageModel.Survey.Description ?? string.Empty,
                StartDate = pageModel.Survey.DateBegin.ToString("yyyy-MM-dd"),
                EndDate = pageModel.Survey.DateEnd?.ToString("yyyy-MM-dd") ?? string.Empty,
                Organizations = organizations,
                Criteria = pageModel.Criteria
            };

            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при подготовке шаблона копирования анкеты {SurveyId}", id);
            return StatusCode(500, new { success = false, message = "Не удалось подготовить данные для копирования анкеты." });
        }
    }
}
