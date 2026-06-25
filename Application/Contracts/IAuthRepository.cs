using MainProject.Application.DTO.Read;

namespace MainProject.Application.Contracts;

public interface IAuthRepository
{
    Task<AuthUserRecord?> GetByLoginAsync(string login, CancellationToken cancellationToken = default);

    Task UpdatePasswordHashAsync(
        int userId,
        string passwordHash,
        CancellationToken cancellationToken = default);
}
