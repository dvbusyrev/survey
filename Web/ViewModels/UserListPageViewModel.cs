using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class UserListPageViewModel
{
    public IReadOnlyList<User> Users { get; init; } = Array.Empty<User>();
    public IReadOnlyList<SelectionOption> Organizations { get; init; } = Array.Empty<SelectionOption>();
    public bool OpenAddUserModal { get; init; }
}
