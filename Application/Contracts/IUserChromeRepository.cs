using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface IUserChromeRepository
{
    Task<UserChromeContext?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
}
