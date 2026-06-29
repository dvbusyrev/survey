using Microsoft.AspNetCore.Authorization;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.DTO.Email;
using MainProject.Application.UseCases.Admin;
using MainProject.Web.Infrastructure;

[Authorize(Roles = AppRoles.Admin)]
public class EmailController : Controller
{
    private readonly EmailTemplateService _emailTemplateService;

    public EmailController(EmailTemplateService emailTemplateService)
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
            return BadRequest(new { success = false, error = "Проверьте корректность настроек электронной почты.", errors = ex.Errors });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось сохранить настройки электронной почты.", "Ошибка при сохранении настроек электронной почты");
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
            return BadRequest(new { success = false, error = "Проверьте параметры письма.", errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return this.SafeError(
                ex,
                "Не удалось отправить письмо.",
                "Ошибка SMTP при отправке письма",
                StatusCodes.Status502BadGateway);
        }
        catch (Exception ex)
        {
            return this.SafeError(
                ex,
                "Не удалось отправить письмо.",
                "Непредвиденная ошибка при отправке письма",
                StatusCodes.Status502BadGateway);
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
}
