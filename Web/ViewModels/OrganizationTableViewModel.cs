using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class OrganizationTableViewModel
{
    public IReadOnlyList<Organization> Organizations { get; init; } = Array.Empty<Organization>();
    public bool ShowActions { get; init; }
    public string TableId { get; init; } = "organizations-table-top";
    public string NameSortUrl { get; init; } = string.Empty;
    public string NameSortDirection { get; init; } = string.Empty;
    public string NameAriaSort { get; init; } = "none";
    public string DateBeginSortUrl { get; init; } = string.Empty;
    public string DateBeginSortDirection { get; init; } = string.Empty;
    public string DateBeginAriaSort { get; init; } = "none";
    public string DateEndSortUrl { get; init; } = string.Empty;
    public string DateEndSortDirection { get; init; } = string.Empty;
    public string DateEndAriaSort { get; init; } = "none";
}
