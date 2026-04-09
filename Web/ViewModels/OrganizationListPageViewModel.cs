using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class OrganizationListPageViewModel
{
    public IReadOnlyList<Organization> Organizations { get; init; } = Array.Empty<Organization>();
    public bool OpenAddOrganizationModal { get; init; }
}
