using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public static class UserListSortFields
{
    public const string Default = "name";
    public const string Name = "name";
    public const string Organization = "organization";
    public const string Role = "role";
    public const string DateBegin = "dateBegin";
    public const string DateEnd = "dateEnd";
}

public sealed class UserListPageViewModel : ServerSortablePageViewModelBase
{
    public IReadOnlyList<User> Users { get; init; } = Array.Empty<User>();
    public IReadOnlyList<SelectionOption> Organizations { get; init; } = Array.Empty<SelectionOption>();
    public bool OpenAddUserModal { get; init; }

    protected override string BasePath => ViewModeIsArchive ? "/users/archive" : "/users";
    protected override string DefaultSortField => UserListSortFields.Default;
    protected override string DefaultSortDirection => "asc";
    protected override string PaginationAriaLabel => ViewModeIsArchive
        ? "Навигация по страницам архива пользователей"
        : "Навигация по страницам списка пользователей";
    protected override string ScrollAnchorId => "users-table-top";

    public bool ViewModeIsArchive { get; init; }

    protected override string NormalizeSortField(string? field)
    {
        return field?.Trim() switch
        {
            UserListSortFields.Organization => UserListSortFields.Organization,
            UserListSortFields.Role => UserListSortFields.Role,
            UserListSortFields.DateBegin => UserListSortFields.DateBegin,
            UserListSortFields.DateEnd => UserListSortFields.DateEnd,
            _ => UserListSortFields.Name
        };
    }

    protected override string GetDefaultDirectionForField(string field)
    {
        return field switch
        {
            UserListSortFields.DateBegin => "desc",
            UserListSortFields.DateEnd => "desc",
            _ => "asc"
        };
    }
}
