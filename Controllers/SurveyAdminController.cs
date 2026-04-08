using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Services.Surveys;
using MainProject.Infrastructure.Security;
using Npgsql;

[Authorize(Roles = AppRoles.Admin)]
public class SurveyAdminController : Controller
{
    private readonly SurveyAdminService _surveyAdminService;
    private readonly ILogger<SurveyAdminController> _logger;

    public SurveyAdminController(SurveyAdminService surveyAdminService, ILogger<SurveyAdminController> logger)
    {
        _surveyAdminService = surveyAdminService;
        _logger = logger;
    }

    [HttpGet("surveys")]
    [HttpGet("get_surveys")]
    [HttpGet("Survey/get_surveys")]
    [ActionName("get_surveys")]
    public IActionResult GetSurveys()
    {
        return View("~/Views/Survey/get_surveys.cshtml", _surveyAdminService.GetSurveys());
    }

    [HttpGet("surveys/create")]
    [HttpGet("add_survey")]
    [ActionName("add_survey")]
    public IActionResult AddSurvey()
    {
        return View("~/Views/Survey/add_survey.cshtml");
    }

    [HttpPost("surveys/create")]
    [HttpPost("add_survey_bd")]
    [ValidateAntiForgeryToken]
    [ActionName("add_survey_bd")]
    public async Task<IActionResult> AddSurveyBd([FromBody] SurveyAddRequest? request)
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
    [HttpPost("update_survey_bd/{id}")]
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
    [HttpPost("copy_survey_bd/{id}")]
    [HttpPost("Survey/copy_survey_bd/{id}")]
    [ValidateAntiForgeryToken]
    [ActionName("copy_survey_bd")]
    public async Task<IActionResult> CopySurveyBd(int id, [FromBody] SurveyCopyRequest? request)
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
    [HttpPost("Survey/delete_survey")]
    [HttpPost("surveys/delete/{id:int}")]
    [ActionName("delete_survey")]
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
    [HttpGet("update_survey/{id}")]
    [HttpPost("update_survey/{id}")]
    [ActionName("update_survey")]
    public IActionResult UpdateSurveyPage(int id)
    {
        try
        {
            var pageModel = _surveyAdminService.GetSurveyEditPage(id);
            if (pageModel == null)
            {
                return NotFound("Анкета не найдена");
            }

            return View("~/Views/Survey/update_survey.cshtml", pageModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении анкеты {SurveyId} для редактирования", id);
            return StatusCode(500, "Произошла ошибка при загрузке анкеты");
        }
    }

    [HttpGet("surveys/{id:int}/copy")]
    [HttpGet("copy_survey/{id}")]
    [HttpPost("copy_survey/{id}")]
    [ActionName("copy_survey")]
    public IActionResult CopySurvey(int id)
    {
        try
        {
            var survey = _surveyAdminService.GetSurveyForCopy(id);
            if (survey == null)
            {
                return NotFound("Анкета не найдена");
            }

            return View("~/Views/Survey/copy_survey.cshtml", survey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке анкеты для копирования (ID: {SurveyId})", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}
