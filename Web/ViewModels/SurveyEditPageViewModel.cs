using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class SurveyEditPageViewModel
{
    public Survey Survey { get; init; } = new();
    public IReadOnlyList<OrganizationSelectionItem> AllOrganization { get; init; } = Array.Empty<OrganizationSelectionItem>();
    public IReadOnlyList<int> SelectedOrganizationIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> SelectedOrganizationNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Criteria { get; init; } = Array.Empty<string>();
}
