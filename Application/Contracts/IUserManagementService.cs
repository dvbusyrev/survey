using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IUserManagementService
{
    Task<UserListPageViewModel> GetActiveUsersPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool openAddUserModal = false,
        CancellationToken cancellationToken = default);
    Task<UserListPageViewModel> GetArchivedUsersPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetArchivedUsersAsync(CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<OperationResult> CreateUserAsync(UserSaveRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateUserAsync(int id, UserUpdateRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteUserAsync(int id, CancellationToken cancellationToken = default);
}
