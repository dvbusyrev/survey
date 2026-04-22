namespace MainProject.Web.ViewModels;

public sealed class ReportsPageViewModel
{
    public IReadOnlyList<int> AvailableYears { get; init; } = Array.Empty<int>();
}
