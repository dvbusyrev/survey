namespace MainProject.Web.ViewModels;

public sealed class SurveyTableViewModel
{
    public IReadOnlyList<SurveyTableRowViewModel> Surveys { get; init; } = Array.Empty<SurveyTableRowViewModel>();
    public bool IsArchive { get; init; }
    public bool IsTemplateSection { get; init; }
    public string TableId { get; init; } = "surveys-table-top";
    public string NameSortUrl { get; init; } = string.Empty;
    public string NameSortDirection { get; init; } = string.Empty;
    public string NameAriaSort { get; init; } = "none";
    public string AutoCreationSortUrl { get; init; } = string.Empty;
    public string AutoCreationSortDirection { get; init; } = string.Empty;
    public string AutoCreationAriaSort { get; init; } = "none";
    public string DateBeginSortUrl { get; init; } = string.Empty;
    public string DateBeginSortDirection { get; init; } = string.Empty;
    public string DateBeginAriaSort { get; init; } = "none";
    public string DateEndSortUrl { get; init; } = string.Empty;
    public string DateEndSortDirection { get; init; } = string.Empty;
    public string DateEndAriaSort { get; init; } = "none";
}
