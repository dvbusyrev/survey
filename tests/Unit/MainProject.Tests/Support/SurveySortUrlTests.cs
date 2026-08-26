using MainProject.Web.ViewModels;

namespace MainProject.Tests.Support;

public sealed class SurveySortUrlTests
{
    [Theory]
    [InlineData(SurveyListSortFields.DateBegin)]
    [InlineData(SurveyListSortFields.DateEnd)]
    public void ActiveSurveyDateSort_CyclesFromNewestToOldestThenResets(string field)
    {
        var unsortedPage = new SurveyListPageViewModel();
        var descendingPage = new SurveyListPageViewModel
        {
            HasExplicitSort = true,
            SortBy = field,
            SortDirection = "desc"
        };
        var ascendingPage = new SurveyListPageViewModel
        {
            HasExplicitSort = true,
            SortBy = field,
            SortDirection = "asc"
        };

        Assert.Equal($"/surveys?sortBy={field}&sortDirection=desc", unsortedPage.BuildSortUrl(field));
        Assert.Equal($"/surveys?sortBy={field}&sortDirection=asc", descendingPage.BuildSortUrl(field));
        Assert.Equal("/surveys", ascendingPage.BuildSortUrl(field));
    }

    [Theory]
    [InlineData(SurveyArchiveSortFields.DateBegin)]
    [InlineData(SurveyArchiveSortFields.DateEnd)]
    public void ArchivedSurveyDateSort_CyclesFromNewestToOldestThenResets(string field)
    {
        var unsortedPage = new SurveyArchivePageViewModel();
        var descendingPage = new SurveyArchivePageViewModel
        {
            HasExplicitSort = true,
            SortBy = field,
            SortDirection = "desc"
        };
        var ascendingPage = new SurveyArchivePageViewModel
        {
            HasExplicitSort = true,
            SortBy = field,
            SortDirection = "asc"
        };

        Assert.Equal($"/surveys/archive?sortBy={field}&sortDirection=desc", unsortedPage.BuildSortUrl(field));
        Assert.Equal($"/surveys/archive?sortBy={field}&sortDirection=asc", descendingPage.BuildSortUrl(field));
        Assert.Equal("/surveys/archive", ascendingPage.BuildSortUrl(field));
    }
}
