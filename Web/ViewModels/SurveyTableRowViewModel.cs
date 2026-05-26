namespace MainProject.Web.ViewModels;

public sealed class SurveyTableRowViewModel
{
    public int IdSurvey { get; init; }
    public string NameSurvey { get; init; } = string.Empty;
    public DateTime DateBegin { get; init; }
    public DateTime? DateEnd { get; init; }
    public IReadOnlyList<int> OrganizationIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> OrganizationNames { get; init; } = Array.Empty<string>();
}
