using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class OrganizationTableViewModel
{
    public IReadOnlyList<Organization> Organizations { get; init; } = Array.Empty<Organization>();
    public bool ShowActions { get; init; }
}
