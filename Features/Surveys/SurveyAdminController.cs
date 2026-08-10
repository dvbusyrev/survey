using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;
using MainProject.Web.ViewModels;
using Npgsql;

[Authorize(Roles = AppRoles.Admin)]
public class SurveyAdminController : Controller
{
    private readonly SurveyService _surveyAdminService;
    private readonly ILogger<SurveyAdminController> _logger;

    public SurveyAdminController(SurveyService surveyAdminService, ILogger<SurveyAdminController> logger)
    {
        _surveyAdminService = surveyAdminService;
        _logger = logger;
    }

    private async Task<SurveyListPageViewModel> BuildSurveyListPageAsync(
        int currentPage = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        bool openAddSurveyModal = false,
        SurveyEditPageViewModel? editSurveyPage = null,
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
            FilterState = pageModel.FilterState,
            OpenAddSurveyModal = openAddSurveyModal,
            EditSurveyPage = editSurveyPage
        };
    }

    [HttpGet("surveys")]
    public async Task<IActionResult> GetSurveys(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        CancellationToken cancellationToken = default)
    {
        return View(
            "~/Views/Survey/get_surveys.cshtml",
            await BuildSurveyListPageAsync(page, sortBy, sortDirection, organizationIds, cancellationToken: cancellationToken));
    }

    [HttpGet("survey/data")]
    [HttpGet("surveys/data")]
    public async Task<IActionResult> GetSurveyOptions(CancellationToken cancellationToken)
    {
        var surveys = (await _surveyAdminService.GetSurveysAsync(cancellationToken))
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

    [HttpGet("survey/create")]
    [HttpGet("surveys/create")]
    public async Task<IActionResult> AddSurvey(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        CancellationToken cancellationToken = default)
    {
        return View(
            "~/Views/Survey/get_surveys.cshtml",
            await BuildSurveyListPageAsync(page, sortBy, sortDirection, organizationIds, openAddSurveyModal: true, cancellationToken: cancellationToken));
    }

    [HttpPost("survey/create")]
    [HttpPost("surveys/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSurvey([FromBody] SurveyAddRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyAdminService.CreateSurveyAsync(request, cancellationToken);
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
            return this.SafeError(ex, "Не удалось создать анкету.", "Ошибка PostgreSQL при создании анкеты");
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось создать анкету.", "Ошибка при создании анкеты");
        }
    }

    [HttpPost("survey/{id:int}/update")]
    [HttpPost("surveys/{id:int}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSurvey(int id, [FromBody] SurveyUpdateRequest? model, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyAdminService.UpdateSurveyAsync(id, model, cancellationToken);
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
            return this.SafeError(ex, "Не удалось обновить анкету.", $"Ошибка при обновлении анкеты {id}");
        }
    }

    [HttpPost("survey/active/work-period")]
    [HttpPost("surveys/active/work-period")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateActiveSurveysWorkPeriod([FromBody] SurveyWorkPeriodRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyAdminService.UpdateActiveSurveysWorkPeriodAsync(request, cancellationToken);
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
            return this.SafeError(ex, "Не удалось сохранить период работы.", "Ошибка при сохранении периода работы активных анкет");
        }
    }

    [HttpPost("survey/{id:int}/copy")]
    [HttpPost("surveys/{id:int}/copy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopySurveySubmission(int id, [FromBody] SurveyCopyRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyAdminService.CopySurveyAsync(id, request, cancellationToken);
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
            return this.SafeError(ex, "Не удалось скопировать анкету.", $"Ошибка PostgreSQL при копировании анкеты {id}");
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось скопировать анкету.", $"Ошибка при копировании анкеты {id}");
        }
    }

    [HttpPost("survey/{id:int}/delete")]
    [HttpPost("surveys/{id:int}/delete")]
    public async Task<IActionResult> DeleteSurvey(int? id, [FromBody] DeleteSurveyRequest? request, CancellationToken cancellationToken)
    {
        var surveyId = request?.SurveyId ?? id ?? 0;
        if (surveyId <= 0)
        {
            return BadRequest(new { success = false, message = "Некорректный идентификатор анкеты." });
        }

        try
        {
            var result = await _surveyAdminService.DeleteSurveyAsync(surveyId, cancellationToken);
            if (!result.Success)
            {
                var error = new
                {
                    success = false,
                    message = result.Message
                };

                if (string.Equals(result.Code, "survey_not_found", StringComparison.Ordinal))
                {
                    return NotFound(error);
                }

                if (string.Equals(result.Code, "survey_in_use", StringComparison.Ordinal))
                {
                    return Conflict(error);
                }

                return BadRequest(error);
            }

            return Ok(new
            {
                success = true,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось удалить анкету.", $"Ошибка при удалении анкеты {surveyId}");
        }
    }

    [HttpGet("survey/{id:int}/edit")]
    [HttpGet("surveys/{id:int}/edit")]
    public async Task<IActionResult> UpdateSurveyPage(int id, CancellationToken cancellationToken)
    {
        try
        {
            var pageModel = await _surveyAdminService.GetSurveyEditPageAsync(id, cancellationToken);
            if (pageModel == null)
            {
                return NotFound("Анкета не найдена.");
            }

            return View("~/Views/Survey/get_surveys.cshtml", await BuildSurveyListPageAsync(editSurveyPage: pageModel, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить анкету.", $"Ошибка при получении анкеты {id} для редактирования");
        }
    }

    [HttpGet("survey/{id:int}/details")]
    [HttpGet("surveys/{id:int}/details")]
    public async Task<IActionResult> GetSurveyDetails(int id, CancellationToken cancellationToken)
    {
        try
        {
            var pageModel = await _surveyAdminService.GetSurveyEditPageAsync(id, cancellationToken);
            if (pageModel == null)
            {
                return NotFound(new { success = false, message = "Анкета не найдена." });
            }

            return Json(new
            {
                id = pageModel.Survey.IdSurvey,
                name = pageModel.Survey.NameSurvey,
                description = pageModel.Survey.Description ?? string.Empty,
                dateBegin = pageModel.Survey.DateBegin.ToString("dd.MM.yyyy"),
                dateEnd = pageModel.Survey.DateEnd?.ToString("dd.MM.yyyy") ?? "Не указана",
                organizations = pageModel.SelectedOrganizationNames,
                criteria = pageModel.Criteria
            });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить анкету.", $"Ошибка при получении анкеты {id} для просмотра");
        }
    }

    [HttpGet("survey/{id:int}/copy")]
    [HttpGet("surveys/{id:int}/copy")]
    public async Task<IActionResult> CopySurvey(int id, CancellationToken cancellationToken)
    {
        try
        {
            var survey = await _surveyAdminService.GetSurveyForCopyAsync(id, cancellationToken);
            if (survey == null)
            {
                return NotFound("Анкета не найдена.");
            }

            return View("~/Views/Survey/copy_survey.cshtml", survey);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить анкету для копирования.", $"Ошибка при загрузке анкеты {id} для копирования");
        }
    }

    [HttpGet("survey/{id:int}/copy-template")]
    [HttpGet("surveys/{id:int}/copy-template")]
    public async Task<IActionResult> GetSurveyCopyTemplate(int id, CancellationToken cancellationToken)
    {
        try
        {
            var pageModel = await _surveyAdminService.GetSurveyEditPageAsync(id, cancellationToken);
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
                Title = pageModel.Survey.NameSurvey,
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
            return this.SafeError(ex, "Не удалось подготовить данные для копирования анкеты.", $"Ошибка при подготовке шаблона копирования анкеты {id}");
        }
    }
}
