using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface IAuthService
{
    Task<LoginResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
}
