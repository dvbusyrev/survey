using System.Reflection;
using MainProject.Application.UseCases.Admin;

namespace MainProject.Tests.Services;

public sealed class OrganizationManagementServiceTests
{
    [Fact]
    public void BuildArchiveRestrictedMessage_IncludesSurveyAndUserLists()
    {
        var result = InvokeBuildArchiveRestrictedMessage(
            ["Анкета А", "Анкета Б"],
            ["Иван Иванов", "petrov"]);

        Assert.Equal(
            "Нельзя удалить организацию: для неё уже заводились анкеты и выбирались пользователи." + Environment.NewLine +
            "Анкеты: Анкета А, Анкета Б." + Environment.NewLine +
            "Пользователи: Иван Иванов, petrov.",
            result);
    }

    [Fact]
    public void BuildArchiveRestrictedMessage_IncludesOnlyRelevantSection_WhenOnlyUsersExist()
    {
        var result = InvokeBuildArchiveRestrictedMessage(
            [],
            ["Иван Иванов"]);

        Assert.Equal(
            "Нельзя удалить организацию: для неё уже выбирались пользователи." + Environment.NewLine +
            "Пользователи: Иван Иванов.",
            result);
    }

    private static string InvokeBuildArchiveRestrictedMessage(
        IReadOnlyList<string> surveyNames,
        IReadOnlyList<string> userNames)
    {
        var method = typeof(OrganizationManagementService).GetMethod(
            "BuildArchiveRestrictedMessage",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var result = method!.Invoke(null, [surveyNames, userNames]);

        return Assert.IsType<string>(result);
    }
}
