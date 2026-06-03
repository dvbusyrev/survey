using MainProject.Application.DTO.Theme;

namespace MainProject.Application.Contracts;

public interface IThemeSettingsService
{
    Task<ThemeSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ThemeSettings settings, CancellationToken cancellationToken = default);
}
