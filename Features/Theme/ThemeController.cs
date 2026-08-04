using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.DTO.Theme;
using MainProject.Application.UseCases.Admin;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;

[Authorize(Roles = AppRoles.Admin)]
public class ThemeController : Controller
{
    private readonly ThemeSettingsService _themeSettingsService;

    public ThemeController(ThemeSettingsService themeSettingsService)
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
            return BadRequest(new { success = false, error = "Проверьте корректность настроек темы.", errors = ex.Errors });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось сохранить настройки темы.", "Ошибка при сохранении настроек темы");
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
}
