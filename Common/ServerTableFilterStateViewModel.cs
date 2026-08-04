using MainProject.Application.DTO;

namespace MainProject.Web.ViewModels;

public sealed class ServerTableFilterStateViewModel
{
    public string BasePath { get; init; } = string.Empty;
    public bool EnableDateFilter { get; init; }
    public bool EnableOrganizationFilter { get; init; }
    public bool EnableSurveyFilter { get; init; }
    public IReadOnlyList<SelectionOption> OrganizationOptions { get; init; } = Array.Empty<SelectionOption>();
    public IReadOnlyList<int> SelectedOrganizationIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<SelectionOption> SurveyOptions { get; init; } = Array.Empty<SelectionOption>();
    public IReadOnlyList<int> SelectedSurveyIds { get; init; } = Array.Empty<int>();
    public int? Year { get; init; }
    public string Month { get; init; } = string.Empty;
    public string DateFrom { get; init; } = string.Empty;
    public string DateTo { get; init; } = string.Empty;
}
