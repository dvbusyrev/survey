using Microsoft.AspNetCore.Authorization;
using MainProject.Infrastructure.Security;
using MainProject.Models;
using MainProject.Services.Email;
using Microsoft.AspNetCore.Mvc;

namespace MainProject.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class EmailController : Controller
    {
        private readonly EmailSettingsStore _settingsStore;
        private readonly SmtpEmailSender _emailSender;

        public EmailController(EmailSettingsStore settingsStore, SmtpEmailSender emailSender)
        {
            _settingsStore = settingsStore;
            _emailSender = emailSender;
        }

        [HttpGet("mail/settings")]
        public async Task<IActionResult> GetEmailSettings(CancellationToken cancellationToken)
        {
            var settings = await _settingsStore.GetAsync(cancellationToken);
            return Ok(settings);
        }

        [HttpPost("mail/settings")]
        public async Task<IActionResult> SaveEmailSettings([FromBody] EmailTemplateSettings settings, CancellationToken cancellationToken)
        {
            if (settings == null)
            {
                return BadRequest(new { success = false, error = "Параметры письма не переданы." });
            }

            try
            {
                await _settingsStore.SaveAsync(settings, cancellationToken);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("mail/send")]
        public async Task<IActionResult> SendMessage(CancellationToken cancellationToken)
        {
            try
            {
                var settings = await _settingsStore.GetAsync(cancellationToken);
                await _emailSender.SendAsync(settings, cancellationToken);
                return Ok(new { message = $"Письмо успешно отправлено на {settings.To}!" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (FormatException ex)
            {
                return BadRequest(new { message = $"Некорректный адрес электронной почты: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Ошибка: {ex.Message}" });
            }
        }

        [HttpGet("mail-settings")]
        public IActionResult UpdateSettings()
        {
            return View("update_settings");
        }
    }
}
