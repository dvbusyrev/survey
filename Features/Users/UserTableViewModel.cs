using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class UserTableViewModel
{
    public IReadOnlyList<User> Users { get; init; } = Array.Empty<User>();
    public bool ShowActions { get; init; }
    public string TableId { get; init; } = "users-table-top";
    public string NameSortUrl { get; init; } = string.Empty;
    public string NameSortDirection { get; init; } = string.Empty;
    public string NameAriaSort { get; init; } = "none";
    public string OrganizationSortUrl { get; init; } = string.Empty;
    public string OrganizationSortDirection { get; init; } = string.Empty;
    public string OrganizationAriaSort { get; init; } = "none";
    public string RoleSortUrl { get; init; } = string.Empty;
    public string RoleSortDirection { get; init; } = string.Empty;
    public string RoleAriaSort { get; init; } = "none";
    public string DateBeginSortUrl { get; init; } = string.Empty;
    public string DateBeginSortDirection { get; init; } = string.Empty;
    public string DateBeginAriaSort { get; init; } = "none";
    public string DateEndSortUrl { get; init; } = string.Empty;
    public string DateEndSortDirection { get; init; } = string.Empty;
    public string DateEndAriaSort { get; init; } = "none";
}
