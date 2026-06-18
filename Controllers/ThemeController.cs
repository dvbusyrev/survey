using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Theme;
using MainProject.Application.UseCases.Admin;
using MainProject.Infrastructure.Security;

[Authorize(Roles = AppRoles.Admin)]
public class ThemeController : Controller
{
    private readonly IThemeSettingsService _themeSettingsService;

    public ThemeController(IThemeSettingsService themeSettingsService)
    {
        _themeSettingsService = themeSettingsService;
    }

    [HttpGet("settings/theme/data")]
    [HttpGet("theme/settings")]
    public async Task<IActionResult> GetThemeSettings(CancellationToken cancellationToken)
    {
        var settings = await _themeSettingsService.GetAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPost("settings/theme/data")]
    [HttpPost("theme/settings")]
    public async Task<IActionResult> SaveThemeSettings([FromBody] ThemeSettings? settings, CancellationToken cancellationToken)
    {
        try
        {
            await _themeSettingsService.SaveAsync(settings ?? new ThemeSettings(), cancellationToken);
            return Ok(new { success = true, message = "Настройки темы сохранены." });
        }
        catch (ThemeSettingsValidationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message, errors = ex.Errors });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = $"Не удалось сохранить настройки темы: {GetDetailedErrorMessage(ex)}" });
        }
    }

    [HttpGet("settings/theme")]
    [HttpGet("theme/configuration")]
    public async Task<IActionResult> UpdateSettings(CancellationToken cancellationToken)
    {
        var settings = await _themeSettingsService.GetAsync(cancellationToken);
        return View("update_settings", settings);
    }

    [HttpGet("theme-settings")]
    public IActionResult LegacyThemeSettings()
    {
        return Redirect("/settings/theme");
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
