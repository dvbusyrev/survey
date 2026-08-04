using MainProject.Application.DTO;

namespace MainProject.Web.ViewModels;

public static class AnswerListSortFields
{
    public const string Default = "date";
    public const string Organization = "organization";
    public const string Survey = "survey";
    public const string Date = "date";
    public const string Signed = "signed";
}

public sealed class AnswerListPageViewModel : ServerSortablePageViewModelBase
{
    public IReadOnlyList<AnswerListItemViewModel> Answers { get; init; } = Array.Empty<AnswerListItemViewModel>();
    public ServerTableFilterStateViewModel FilterState { get; init; } = new();

    protected override string BasePath => "/surveys/answers";
    protected override string DefaultSortField => AnswerListSortFields.Default;
    protected override string DefaultSortDirection => "desc";
    protected override string PaginationAriaLabel => "Навигация по страницам списка ответов";
    protected override string ScrollAnchorId => "answers-table-top";

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
            AnswerListSortFields.Organization => AnswerListSortFields.Organization,
            AnswerListSortFields.Survey => AnswerListSortFields.Survey,
            AnswerListSortFields.Signed => AnswerListSortFields.Signed,
            _ => AnswerListSortFields.Date
        };
    }

    protected override string GetDefaultDirectionForField(string field)
    {
        return field switch
        {
            AnswerListSortFields.Date => "desc",
            AnswerListSortFields.Signed => "desc",
            _ => "asc"
        };
    }
}
