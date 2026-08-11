namespace MainProject.Web.ViewModels;

public static class SurveyListSortFields
{
    public const string Name = "name";
    public const string Default = Name;
    public const string DateBegin = "dateBegin";
    public const string DateEnd = "dateEnd";
}

public sealed class SurveyListPageViewModel : ServerSortablePageViewModelBase
{
    public IReadOnlyList<SurveyTableRowViewModel> SurveyRows { get; init; } = Array.Empty<SurveyTableRowViewModel>();
    public bool OpenAddSurveyModal { get; init; }
    public SurveyEditPageViewModel? EditSurveyPage { get; init; }
    public ServerTableFilterStateViewModel FilterState { get; init; } = new();

    protected override string BasePath => "/surveys";
    protected override string DefaultSortField => SurveyListSortFields.Default;
    protected override string DefaultSortDirection => "asc";
    protected override string PaginationAriaLabel => "Навигация по страницам списка анкет";
    protected override string ScrollAnchorId => "surveys-table-top";

    protected override IEnumerable<KeyValuePair<string, string>> BuildAdditionalQueryParameters()
    {
        if (FilterState.SelectedOrganizationIds.Count > 0)
        {
            yield return new KeyValuePair<string, string>(
                "organizationIds",
                string.Join(",", FilterState.SelectedOrganizationIds));
        }
    }

    protected override string NormalizeSortField(string? field)
    {
        return field?.Trim() switch
        {
            SurveyListSortFields.Name => SurveyListSortFields.Name,
            SurveyListSortFields.DateBegin => SurveyListSortFields.DateBegin,
            SurveyListSortFields.DateEnd => SurveyListSortFields.DateEnd,
            _ => SurveyListSortFields.Default
        };
    }

    protected override string GetDefaultDirectionForField(string field)
    {
        return field switch
        {
            SurveyListSortFields.Name => "asc",
            SurveyListSortFields.DateBegin => "desc",
            SurveyListSortFields.DateEnd => "desc",
            _ => "desc"
        };
    }
}
