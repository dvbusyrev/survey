using MainProject.Web.ViewModels;

namespace MainProject.Tests.Services;

public sealed class AppNavigationRouteResolverTests
{
    [Theory]
    [InlineData("/survey", "get_surveys")]
    [InlineData("/survey/create", "add_survey")]
    [InlineData("/survey/answer", "list_answers_users")]
    [InlineData("/survey/archive", "archived_surveys")]
    [InlineData("/survey/17/edit", "get_surveys")]
    [InlineData("/survey-templates", "survey_templates")]
    [InlineData("/survey-templates/create", "add_survey_template")]
    [InlineData("/survey-templates/planned", "planned_survey_templates")]
    [InlineData("/survey-templates/planned/create", "add_planned_survey_template")]
    [InlineData("/survey-templates/planned/17/edit", "planned_survey_templates")]
    [InlineData("/survey-templates/archive", "archived_survey_templates")]
    [InlineData("/survey-templates/17/edit", "survey_templates")]
    [InlineData("/users/archive", "archived_users")]
    [InlineData("/organizations/survey", "organization_surveys")]
    [InlineData("/settings/survey-creation", "survey_auto_creation")]
    [InlineData("/survey-auto-creation", "survey_auto_creation")]
    [InlineData("/settings/theme", "theme_settings")]
    [InlineData("/settings/email", "email_settings")]
    [InlineData("/help", "help")]
    public void Resolve_ReturnsStableNavigationTab(string path, string expectedTab)
    {
        Assert.Equal(expectedTab, AppNavigationRouteResolver.Resolve(path));
    }

    [Fact]
    public void Resolve_IgnoresQueryAndTrailingSlash()
    {
        Assert.Equal("get_users", AppNavigationRouteResolver.Resolve("/users/?page=2"));
    }
}
