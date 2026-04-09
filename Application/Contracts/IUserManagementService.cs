using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IUserManagementService
{
    UserListPageViewModel GetActiveUsersPage(bool openAddUserModal = false);
    IReadOnlyList<User> GetArchivedUsers();
    User? GetUserById(int id);
    OperationResult CreateUser(UserSaveRequest request);
    OperationResult UpdateUser(int id, UserUpdateRequest request);
    OperationResult DeleteUser(int id);
}
