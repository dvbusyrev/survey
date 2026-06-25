using MainProject.Application.DTO;
using MainProject.Application.DTO.User;
using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface IUserRepository
{
    Task<int> CountAsync(bool includeArchived, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetPageAsync(bool includeArchived, string sortBy, string sortDirection, int pageSize, int offset, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SelectionOption>> GetActiveOrganizationOptionsAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(UserWriteModel user, CancellationToken cancellationToken = default);
    Task<int> UpdateAsync(int userId, UserWriteModel user, CancellationToken cancellationToken = default);
    Task<UserDeletionResult> DeleteIfAllowedAsync(int userId, CancellationToken cancellationToken = default);
}
