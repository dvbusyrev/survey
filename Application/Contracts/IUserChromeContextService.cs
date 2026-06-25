using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface IUserChromeContextService
{
    Task<UserChromeContext> GetCurrentContextAsync(CancellationToken cancellationToken = default);
}
