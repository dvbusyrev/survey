using MainProject.Application.DTO;
using MainProject.Application.DTO.User;
using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface IUserRepository
{
    int Count(bool includeArchived);
    IReadOnlyList<User> GetPage(bool includeArchived, string sortBy, string sortDirection, int pageSize, int offset);
    IReadOnlyList<User> GetAll(bool includeArchived);
    User? GetById(int userId);
    IReadOnlyList<SelectionOption> GetActiveOrganizationOptions();
    int Create(UserWriteModel user);
    int Update(int userId, UserWriteModel user);
    UserDeletionResult DeleteIfAllowed(int userId);
}
