namespace MainProject.Web.ViewModels;

public static class SurveyArchiveSortFields
{
    public const string Name = "name";
    public const string DateBegin = "dateBegin";
    public const string DateEnd = "dateEnd";
    public const string Default = DateBegin;
}

public sealed class SurveyArchivePageViewModel : ServerSortablePageViewModelBase
{
    public IReadOnlyList<SurveyTableRowViewModel> SurveyRows { get; init; } = Array.Empty<SurveyTableRowViewModel>();
    public bool IsTemplateSection { get; init; }
    public SurveyEditPageViewModel? EditSurveyPage { get; init; }
    public ServerTableFilterStateViewModel FilterState { get; init; } = new();

    protected override string BasePath => IsTemplateSection ? "/survey-templates/archive" : "/surveys/archive";
    protected override string DefaultSortField => SurveyArchiveSortFields.Default;
    protected override string DefaultSortDirection => "desc";
    protected override string PaginationAriaLabel => IsTemplateSection
        ? "Навигация по страницам архива шаблонов"
        : "Навигация по страницам архива анкет";
    protected override string ScrollAnchorId => "surveys-table-top";

    protected override IEnumerable<KeyValuePair<string, string>> BuildAdditionalQueryParameters()
    {
        if (FilterState.SelectedOrganizationIds.Count > 0)
        {
            yield return new KeyValuePair<string, string>(
                "organizationIds",
                string.Join(",", FilterState.SelectedOrganizationIds));
        }

        if (FilterState.SelectedSurveyIds.Count > 0)
        {
            yield return new KeyValuePair<string, string>(
                "surveyIds",
                string.Join(",", FilterState.SelectedSurveyIds));
        }

        if (FilterState.Year.HasValue)
        {
            yield return new KeyValuePair<string, string>("year", FilterState.Year.Value.ToString());
        }
        else if (!string.IsNullOrWhiteSpace(FilterState.Month))
        {
            yield return new KeyValuePair<string, string>("month", FilterState.Month);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(FilterState.DateFrom))
            {
                yield return new KeyValuePair<string, string>("dateFrom", FilterState.DateFrom);
            }

            if (!string.IsNullOrWhiteSpace(FilterState.DateTo))
            {
                yield return new KeyValuePair<string, string>("dateTo", FilterState.DateTo);
            }
        }
    }

    protected override string NormalizeSortField(string? field)
    {
        return field?.Trim() switch
        {
            SurveyArchiveSortFields.Name => SurveyArchiveSortFields.Name,
            SurveyArchiveSortFields.DateBegin => SurveyArchiveSortFields.DateBegin,
            SurveyArchiveSortFields.DateEnd => SurveyArchiveSortFields.DateEnd,
            _ => SurveyArchiveSortFields.Default
        };
    }

    protected override string GetDefaultDirectionForField(string field)
    {
        return field switch
        {
            SurveyArchiveSortFields.Name => "asc",
            SurveyArchiveSortFields.DateBegin => "desc",
            SurveyArchiveSortFields.DateEnd => "desc",
            _ => "desc"
        };
    }
}
