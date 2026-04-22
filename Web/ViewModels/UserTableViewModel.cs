using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class UserTableViewModel
{
    public IReadOnlyList<User> Users { get; init; } = Array.Empty<User>();
    public bool ShowActions { get; init; }
}
