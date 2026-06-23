using MainProject.Application.DTO.Configuration;

namespace MainProject.Application.Contracts;

public interface IThemeConfigRepository
{
    Task<ThemeConfigRecord?> GetAsync(int configId, CancellationToken cancellationToken = default);
    Task SaveAsync(int configId, ThemeConfigRecord record, CancellationToken cancellationToken = default);
}
