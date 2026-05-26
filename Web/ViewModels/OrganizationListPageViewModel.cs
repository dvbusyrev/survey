using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public static class OrganizationListSortFields
{
    public const string Default = "name";
    public const string Name = "name";
    public const string DateBegin = "dateBegin";
    public const string DateEnd = "dateEnd";
}

public sealed class OrganizationListPageViewModel : ServerSortablePageViewModelBase
{
    public IReadOnlyList<Organization> Organizations { get; init; } = Array.Empty<Organization>();
    public bool OpenAddOrganizationModal { get; init; }

    protected override string BasePath => ViewModeIsArchive ? "/organizations/archive" : "/organizations";
    protected override string DefaultSortField => OrganizationListSortFields.Default;
    protected override string DefaultSortDirection => "asc";
    protected override string PaginationAriaLabel => ViewModeIsArchive
        ? "Навигация по страницам архива организаций"
        : "Навигация по страницам списка организаций";
    protected override string ScrollAnchorId => "organizations-table-top";

    public bool ViewModeIsArchive { get; init; }

    protected override string NormalizeSortField(string? field)
    {
        return field?.Trim() switch
        {
            OrganizationListSortFields.DateBegin => OrganizationListSortFields.DateBegin,
            OrganizationListSortFields.DateEnd => OrganizationListSortFields.DateEnd,
            _ => OrganizationListSortFields.Name
        };
    }

    protected override string GetDefaultDirectionForField(string field)
    {
        return field switch
        {
            OrganizationListSortFields.DateBegin => "desc",
            OrganizationListSortFields.DateEnd => "desc",
            _ => "asc"
        };
    }
}
