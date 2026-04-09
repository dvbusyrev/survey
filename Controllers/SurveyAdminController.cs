using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Security;
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

    [HttpGet("surveys")]
    public IActionResult GetSurveys()
    {
        return View("~/Web/Views/Survey/get_surveys.cshtml", _surveyAdminService.GetSurveys());
    }

    [HttpGet("surveys/create")]
    public IActionResult AddSurvey()
    {
        return View("~/Web/Views/Survey/add_survey.cshtml");
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

            return View("~/Web/Views/Survey/update_survey.cshtml", pageModel);
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
}
