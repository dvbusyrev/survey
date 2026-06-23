using Npgsql;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface ISurveyAssignmentRepository
{
    IReadOnlyList<Survey> GetActiveSurveySummaries(NpgsqlConnection connection);

    Survey? GetSurveyWithSchedule(NpgsqlConnection connection, int surveyId);

    IReadOnlyList<OrganizationSelectionItem> GetAvailableOrganizationsForSurvey(
        NpgsqlConnection connection,
        int surveyId);

    IReadOnlyList<OrganizationSelectionItem> GetSelectedOrganizationsForSurvey(
        NpgsqlConnection connection,
        int surveyId);

    int UpdateActiveSurveyPeriod(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTime dateBegin,
        DateTime dateEnd);

    IReadOnlyList<ArchivedSurvey> GetAdminArchivedSurveySummaries(NpgsqlConnection connection);

    Task<ArchivedSurvey?> GetArchivedSurveyForCopyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default);

    int CountActiveSurveys(NpgsqlConnection connection, IReadOnlyCollection<int> organizationIds);

    IReadOnlyList<SurveyAssignmentTableRow> GetActiveSurveyPage(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset);

    IReadOnlyList<SelectionOption> GetActiveOrganizationOptions(NpgsqlConnection connection);

    int CountArchivedSurveys(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> surveyIds,
        DateTime? dateStart,
        DateTime? dateEnd);

    IReadOnlyList<SurveyAssignmentTableRow> GetArchivedSurveyPage(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> surveyIds,
        DateTime? dateStart,
        DateTime? dateEnd,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset);

    IReadOnlyList<SelectionOption> GetArchivedOrganizationOptions(NpgsqlConnection connection);

    IReadOnlyList<SelectionOption> GetArchivedSurveyOptions(NpgsqlConnection connection);

    int? GetUserOrganizationId(NpgsqlConnection connection, int userId);

    bool IsActiveAssignment(NpgsqlConnection connection, int surveyId, int organizationId);

    UserSurveyAssignmentPageData GetActiveUserSurveyPage(
        NpgsqlConnection connection,
        int organizationId,
        string searchTerm,
        int pageSize,
        int offset);

    UserSurveyAssignmentPageData GetUserArchivePage(
        NpgsqlConnection connection,
        int organizationId,
        string searchTerm,
        DateTime? exactCompletionDate,
        DateTime? completionDateFrom,
        DateTime? completionDateTo,
        bool signedOnly,
        int pageSize,
        int offset);

    Task ReplaceSurveyAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IEnumerable<int> organizationIds,
        DateTime dateBegin,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default);

    Task UpsertSurveyAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IEnumerable<int> organizationIds,
        DateTime dateBegin,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetOrganizationIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<bool> HasSurveyWithScheduleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string surveyName,
        DateTime dateBegin,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default);

    int UpsertSurveyEndDate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId,
        DateTime dateEnd);

    int? GetAssignmentIdForUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId);
}
