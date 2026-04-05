using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Services.Answers;
using main_project.Models;

[Authorize]
public class AnswerWorkflowController : Controller
{
    private readonly AnswerAccessService _answerAccessService;
    private readonly AnswerWorkflowService _answerWorkflowService;
    private readonly ILogger<AnswerWorkflowController> _logger;

    public AnswerWorkflowController(
        AnswerAccessService answerAccessService,
        AnswerWorkflowService answerWorkflowService,
        ILogger<AnswerWorkflowController> logger)
    {
        _answerAccessService = answerAccessService;
        _answerWorkflowService = answerWorkflowService;
        _logger = logger;
    }

    [HttpPost("answers/create")]
    [HttpPost("api/insert_answer")]
    public IActionResult insert_answer([FromBody] HistoryAnswer historyAnswerData)
    {
        if (historyAnswerData == null)
        {
            return BadRequest("Данные ответа отсутствуют.");
        }

        var accessResult = EnsureOmsuAccess(historyAnswerData.id_omsu);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var model = _answerWorkflowService.InsertAnswer(historyAnswerData);
            if (model == null)
            {
                return NotFound("Анкета не найдена");
            }

            return View("~/Views/Answer/check_answers.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении ответа");
            return View("Error", new ErrorViewModel { Message = $"Ошибка при сохранении ответа: {ex.Message}" });
        }
    }

    [HttpGet("answers/{idSurvey}/{idOmsu}/{type?}")]
    [HttpGet("Answer/answers/{idSurvey}/{idOmsu}/{type?}")]
    public IActionResult answers(int idSurvey, int idOmsu = 0, string type = "regular")
    {
        var includeAllOmsuAnswers = string.Equals(type, "archive", StringComparison.OrdinalIgnoreCase)
            && _answerAccessService.IsAdmin;

        if (!includeAllOmsuAnswers)
        {
            var accessResult = EnsureOmsuAccess(idOmsu);
            if (accessResult != null)
            {
                return accessResult;
            }
        }

        try
        {
            var response = _answerWorkflowService.GetAnswersResponse(idSurvey, idOmsu, type, includeAllOmsuAnswers);
            if (!response.Success)
            {
                return NotFound(new
                {
                    success = false,
                    error = response.Error
                });
            }

            return Json(new
            {
                success = true,
                survey = new
                {
                    id = response.Survey?.Id ?? idSurvey,
                    name = response.Survey?.Name ?? string.Empty,
                    description = response.Survey?.Description,
                    is_archive = response.Survey?.IsArchive ?? false,
                    csp = response.Survey?.Csp
                },
                answers = response.Answers.Select(answer => new
                {
                    id = answer.Id,
                    omsu_id = answer.OmsuId,
                    omsu_name = answer.OmsuName,
                    date = answer.Date,
                    answers = answer.Answers.Select(item => new
                    {
                        question_text = item.QuestionText,
                        rating = item.Rating,
                        comment = item.Comment
                    }),
                    is_signed = answer.IsSigned,
                    signature = answer.Signature
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке запроса ответов");
            return StatusCode(500, new
            {
                success = false,
                error = "Внутренняя ошибка сервера",
                details = ex.Message
            });
        }
    }

    [HttpGet("answers/{idSurvey}/{idOmsu}/edit")]
    [HttpPost("answers/{idSurvey}/{idOmsu}/edit")]
    [HttpPost("update_answer/{idSurvey}/{idOmsu}")]
    [HttpPost("Answer/update_answer/{idSurvey}/{idOmsu}")]
    public IActionResult update_answer([FromRoute] int idSurvey, [FromRoute] int idOmsu)
    {
        var accessResult = EnsureOmsuAccess(idOmsu);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var model = _answerWorkflowService.GetUpdateAnswerPage(idSurvey, idOmsu);
            if (model == null)
            {
                return NotFound("Ответы не найдены");
            }

            return View("~/Views/Answer/update_answer.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке страницы редактирования ответа");
            return StatusCode(500, "Произошла ошибка на сервере");
        }
    }

    [HttpPost("answers/update")]
    [HttpPost("update_answer_bd")]
    [HttpPost("Answer/update_answer_bd")]
    public IActionResult update_answer_bd([FromBody] HistoryAnswer historyAnswerData)
    {
        if (historyAnswerData == null)
        {
            return BadRequest("Данные ответа отсутствуют.");
        }

        var accessResult = EnsureOmsuAccess(historyAnswerData.id_omsu);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var model = _answerWorkflowService.UpdateAnswer(historyAnswerData);
            if (model == null)
            {
                return NotFound("Запись для обновления не найдена.");
            }

            return View("~/Views/Answer/check_answers.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении ответа");
            return View("Error", new ErrorViewModel { Message = $"Ошибка при обновлении ответа: {ex.Message}" });
        }
    }

    private IActionResult? EnsureOmsuAccess(int requestedOmsuId)
    {
        if (!_answerAccessService.IsAuthenticated)
        {
            return Challenge();
        }

        if (!_answerAccessService.CanAccessOmsu(requestedOmsuId))
        {
            return Forbid();
        }

        return null;
    }
}
