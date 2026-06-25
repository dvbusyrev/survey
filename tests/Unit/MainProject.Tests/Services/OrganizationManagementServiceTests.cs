using System.Reflection;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Organization;
using MainProject.Application.UseCases.Admin;
using MainProject.Domain.Entities;

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

    [Fact]
    public async Task GetOrganizationSurveyAssignmentsPage_UsesInjectedClockForExpirationStatus()
    {
        var clock = new FixedClock(new DateTime(2030, 5, 10));
        var repository = new StubOrganizationRepository([
            new OrganizationSurveyAssignmentRecord
            {
                OrganizationId = 1,
                OrganizationName = "Организация",
                SurveyId = 10,
                SurveyName = "Анкета",
                AssignmentDateEnd = new DateTime(2030, 5, 9)
            },
            new OrganizationSurveyAssignmentRecord
            {
                OrganizationId = 1,
                OrganizationName = "Организация",
                SurveyId = 11,
                SurveyName = "Анкета сегодня",
                AssignmentDateEnd = new DateTime(2030, 5, 10)
            }
        ]);
        var service = new OrganizationManagementService(repository, clock);

        var page = await service.GetOrganizationSurveyAssignmentsPageAsync();
        var assignments = Assert.Single(page.Organizations).Surveys;

        Assert.True(Assert.Single(assignments, item => item.SurveyId == 10).IsExpired);
        Assert.False(Assert.Single(assignments, item => item.SurveyId == 11).IsExpired);
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

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime Today => now.Date;
        public DateTime Now => now;
    }

    private sealed class StubOrganizationRepository(
        IReadOnlyList<OrganizationSurveyAssignmentRecord> assignments) : IOrganizationRepository
    {
        public Task<IReadOnlyList<OrganizationSurveyAssignmentRecord>> GetLatestUnansweredAssignmentsAsync(
            IReadOnlyCollection<int>? organizationIds = null,
            CancellationToken cancellationToken = default) => Task.FromResult(assignments);

        public Task<int> CountAsync(bool includeArchived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Organization>> GetPageAsync(bool includeArchived, string sortBy, string sortDirection, int pageSize, int offset, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Organization>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OrganizationDataResponse>> GetActiveOptionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Organization?> GetByIdAsync(int organizationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CreateAsync(OrganizationWriteModel organization, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> UpdateAsync(int organizationId, OrganizationWriteModel organization, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OrganizationArchiveResult> ArchiveIfUnusedAsync(int organizationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateAssignmentEndDatesAsync(IReadOnlyCollection<(int OrganizationId, int SurveyId)> assignments, DateTime dateEnd, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
