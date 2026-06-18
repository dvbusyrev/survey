using Microsoft.AspNetCore.Authorization;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Email;
using MainProject.Application.UseCases.Admin;

[Authorize(Roles = AppRoles.Admin)]
public class EmailController : Controller
{
    private readonly IEmailTemplateService _emailTemplateService;

    public EmailController(IEmailTemplateService emailTemplateService)
    {
        _emailTemplateService = emailTemplateService;
    }

    [HttpGet("email/settings")]
    [HttpGet("mail/settings")]
    public async Task<IActionResult> GetEmailSettings(CancellationToken cancellationToken)
    {
        var settings = await _emailTemplateService.GetAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPost("email/settings")]
    [HttpPost("mail/settings")]
    public async Task<IActionResult> SaveEmailSettings([FromBody] EmailTemplateSettings? settings, CancellationToken cancellationToken)
    {
        try
        {
            await _emailTemplateService.SaveAsync(settings ?? new EmailTemplateSettings(), cancellationToken);
            return Ok(new { success = true, message = "Настройки электронной почты сохранены." });
        }
        catch (EmailTemplateValidationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message, errors = ex.Errors });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = $"Не удалось сохранить настройки: {GetDetailedErrorMessage(ex)}" });
        }
    }

    [HttpPost("email/send")]
    [HttpPost("mail/send")]
    public async Task<IActionResult> SendMessage([FromBody] EmailTemplateSettings? settings, CancellationToken cancellationToken)
    {
        try
        {
            var recipientCount = await _emailTemplateService.SendAsync(settings ?? new EmailTemplateSettings(), cancellationToken);
            return Ok(new
            {
                success = true,
                message = recipientCount == 1
                    ? "Письмо отправлено."
                    : $"Письмо отправлено ({recipientCount} получателя)."
            });
        }
        catch (EmailTemplateValidationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message, errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, error = $"Не удалось отправить письмо: {GetDetailedErrorMessage(ex)}" });
        }
    }

    [HttpGet("email")]
    [HttpGet("mail")]
    [HttpGet("mail/new")]
    public async Task<IActionResult> NewMessage(CancellationToken cancellationToken)
    {
        var settings = await _emailTemplateService.GetAsync(cancellationToken);
        return View("new_message", settings);
    }

    [HttpGet("settings/email")]
    [HttpGet("mail/configuration")]
    public async Task<IActionResult> UpdateSettings(CancellationToken cancellationToken)
    {
        var settings = await _emailTemplateService.GetAsync(cancellationToken);
        return View("update_settings", settings);
    }

    [HttpGet("mail-settings")]
    public IActionResult LegacyMailSettings()
    {
        return Redirect("/settings/email");
    }

    private static string GetDetailedErrorMessage(Exception exception)
    {
        var messages = new List<string>();
        Exception? current = exception;

        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message)
                && !messages.Contains(current.Message, StringComparer.Ordinal))
            {
                messages.Add(current.Message);
            }

            current = current.InnerException;
        }

        return messages.Count > 0
            ? string.Join(" | ", messages)
            : "Неизвестная ошибка.";
    }
}
