using MainProject.Application.DTO;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;
using MainProject.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[Authorize(Roles = AppRoles.Admin)]
public sealed class SurveyTemplateAdminController : Controller
{
    private readonly SurveyService _surveyService;

    public SurveyTemplateAdminController(SurveyService surveyService)
    {
        _surveyService = surveyService;
    }

    private async Task<SurveyListPageViewModel> BuildTemplateListPageAsync(
        int currentPage = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        bool openAddTemplateModal = false,
        SurveyEditPageViewModel? editSurveyPage = null,
        bool isPlannedTemplateSection = false,
        CancellationToken cancellationToken = default)
    {
        var pageModel = isPlannedTemplateSection
            ? await _surveyService.GetPlannedSurveyTemplatesPageAsync(
                currentPage,
                sortBy,
                sortDirection,
                organizationIds,
                cancellationToken)
            : await _surveyService.GetSurveyTemplatesPageAsync(
                currentPage,
                sortBy,
                sortDirection,
                organizationIds,
                cancellationToken);

        return new SurveyListPageViewModel
        {
            SurveyRows = pageModel.SurveyRows,
            IsTemplateSection = true,
            IsPlannedTemplateSection = isPlannedTemplateSection,
            CurrentPage = pageModel.CurrentPage,
            TotalPages = pageModel.TotalPages,
            TotalCount = pageModel.TotalCount,
            PageSize = pageModel.PageSize,
            HasExplicitSort = pageModel.HasExplicitSort,
            SortBy = pageModel.SortBy,
            SortDirection = pageModel.SortDirection,
            FilterState = pageModel.FilterState,
            OpenAddSurveyModal = openAddTemplateModal,
            EditSurveyPage = editSurveyPage
        };
    }

    [HttpGet("survey-templates/planned")]
    public async Task<IActionResult> GetPlannedTemplates(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        CancellationToken cancellationToken = default)
    {
        return View(
            "~/Views/Survey/get_surveys.cshtml",
            await BuildTemplateListPageAsync(
                page,
                sortBy,
                sortDirection,
                organizationIds,
                isPlannedTemplateSection: true,
                cancellationToken: cancellationToken));
    }

    [HttpGet("survey-templates/planned/create")]
    public async Task<IActionResult> AddPlannedTemplate(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        CancellationToken cancellationToken = default)
    {
        return View(
            "~/Views/Survey/get_surveys.cshtml",
            await BuildTemplateListPageAsync(
                page,
                sortBy,
                sortDirection,
                organizationIds,
                openAddTemplateModal: true,
                isPlannedTemplateSection: true,
                cancellationToken: cancellationToken));
    }

    [HttpGet("survey-templates")]
    public async Task<IActionResult> GetTemplates(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        CancellationToken cancellationToken = default)
    {
        return View(
            "~/Views/Survey/get_surveys.cshtml",
            await BuildTemplateListPageAsync(
                page,
                sortBy,
                sortDirection,
                organizationIds,
                cancellationToken: cancellationToken));
    }

    [HttpGet("survey-templates/create")]
    public async Task<IActionResult> AddTemplate(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        CancellationToken cancellationToken = default)
    {
        return View(
            "~/Views/Survey/get_surveys.cshtml",
            await BuildTemplateListPageAsync(
                page,
                sortBy,
                sortDirection,
                organizationIds,
                openAddTemplateModal: true,
                cancellationToken: cancellationToken));
    }

    [HttpGet("survey-templates/options")]
    public async Task<IActionResult> GetTemplateOptions(CancellationToken cancellationToken)
    {
        try
        {
            return Json(await _surveyService.GetActiveSurveyTemplateOptionsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить список шаблонов.", "Ошибка при получении списка шаблонов анкет");
        }
    }

    [HttpPost("survey-templates/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(
        [FromBody] SurveyAddRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyService.CreateSurveyTemplateAsync(request, cancellationToken);
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
            return this.SafeError(ex, "Не удалось создать шаблон.", "Ошибка PostgreSQL при создании шаблона анкеты");
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось создать шаблон.", "Ошибка при создании шаблона анкеты");
        }
    }

    [HttpPost("survey-templates/planned/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePlannedTemplate(
        [FromBody] SurveyAddRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyService.CreatePlannedSurveyTemplateAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message, surveyId = result.SurveyId });
        }
        catch (PostgresException ex)
        {
            return this.SafeError(ex, "Не удалось создать плановый шаблон.", "Ошибка PostgreSQL при создании планового шаблона анкеты");
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось создать плановый шаблон.", "Ошибка при создании планового шаблона анкеты");
        }
    }

    [HttpGet("survey-templates/{id:int}/edit")]
    public async Task<IActionResult> EditTemplate(int id, CancellationToken cancellationToken)
    {
        try
        {
            var editPage = await _surveyService.GetSurveyTemplateEditPageAsync(id, cancellationToken);
            if (editPage == null)
            {
                return NotFound("Шаблон не найден.");
            }

            return View(
                "~/Views/Survey/get_surveys.cshtml",
                await BuildTemplateListPageAsync(
                    editSurveyPage: editPage,
                    cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить шаблон.", $"Ошибка при получении шаблона анкеты {id} для редактирования");
        }
    }

    [HttpPost("survey-templates/{id:int}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTemplate(
        int id,
        [FromBody] SurveyUpdateRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyService.UpdateSurveyTemplateAsync(id, request, cancellationToken);
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
            return this.SafeError(ex, "Не удалось обновить шаблон.", $"Ошибка при обновлении шаблона анкеты {id}");
        }
    }

    [HttpGet("survey-templates/planned/{id:int}/edit")]
    public async Task<IActionResult> EditPlannedTemplate(int id, CancellationToken cancellationToken)
    {
        try
        {
            var editPage = await _surveyService.GetPlannedSurveyTemplateEditPageAsync(id, cancellationToken);
            if (editPage == null)
            {
                return NotFound("Плановый шаблон не найден.");
            }

            return View(
                "~/Views/Survey/get_surveys.cshtml",
                await BuildTemplateListPageAsync(
                    editSurveyPage: editPage,
                    isPlannedTemplateSection: true,
                    cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить плановый шаблон.", $"Ошибка при получении планового шаблона {id} для редактирования");
        }
    }

    [HttpPost("survey-templates/planned/{id:int}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePlannedTemplate(
        int id,
        [FromBody] SurveyUpdateRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyService.UpdatePlannedSurveyTemplateAsync(id, request, cancellationToken);
            if (!result.Success)
            {
                return result.NotFound
                    ? NotFound(new { success = false, message = result.Message })
                    : BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message, surveyId = result.SurveyId });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось обновить плановый шаблон.", $"Ошибка при обновлении планового шаблона {id}");
        }
    }

    [HttpGet("survey-templates/{id:int}/details")]
    public async Task<IActionResult> GetTemplateDetails(int id, CancellationToken cancellationToken)
    {
        try
        {
            var pageModel = await _surveyService.GetSurveyTemplateEditPageAsync(id, cancellationToken);
            if (pageModel == null)
            {
                return NotFound(new { success = false, message = "Шаблон не найден." });
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
            return this.SafeError(ex, "Не удалось загрузить шаблон.", $"Ошибка при просмотре шаблона {id}");
        }
    }

    [HttpGet("survey-templates/{id:int}/copy-template")]
    public async Task<IActionResult> GetTemplateCopyData(int id, CancellationToken cancellationToken)
    {
        try
        {
            var pageModel = await _surveyService.GetSurveyTemplateEditPageAsync(id, cancellationToken);
            if (pageModel == null)
            {
                return NotFound(new { success = false, message = "Шаблон не найден." });
            }

            var organizations = pageModel.SelectedOrganizationIds
                .Select((organizationId, index) => new OrganizationSelectionItem
                {
                    Id = organizationId,
                    Name = pageModel.SelectedOrganizationNames.ElementAtOrDefault(index) ?? string.Empty
                })
                .Where(organization => organization.Id > 0 && !string.IsNullOrWhiteSpace(organization.Name))
                .ToArray();

            return Json(new SurveyCopyTemplateResponse
            {
                Title = pageModel.Survey.NameSurvey,
                Description = pageModel.Survey.Description ?? string.Empty,
                StartDate = pageModel.Survey.DateBegin.ToString("yyyy-MM-dd"),
                EndDate = pageModel.Survey.DateEnd?.ToString("yyyy-MM-dd") ?? string.Empty,
                Organizations = organizations,
                Criteria = pageModel.Criteria,
                IsAutoCreationEnabled = pageModel.IsAutoCreationEnabled
            });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось подготовить копирование шаблона.", $"Ошибка копирования шаблона {id}");
        }
    }

    [HttpPost("survey-templates/{id:int}/delete")]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyService.DeleteSurveyTemplateAsync(id, cancellationToken);
            if (!result.Success)
            {
                return result.Code == "survey_template_not_found"
                    ? NotFound(new { success = false, message = result.Message })
                    : BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось удалить шаблон.", $"Ошибка при удалении шаблона {id}");
        }
    }

    [HttpGet("survey-templates/archive")]
    public async Task<IActionResult> ArchivedTemplates(
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
        var pageModel = await _surveyService.GetAdminArchivedSurveyTemplatesPageAsync(
            page,
            sortBy,
            sortDirection,
            organizationIds,
            surveyIds,
            year,
            month,
            dateFrom,
            dateTo,
            cancellationToken);

        return View("~/Views/Survey/archived_surveys.cshtml", pageModel);
    }
}
