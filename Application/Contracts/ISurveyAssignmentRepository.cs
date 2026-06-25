using Npgsql;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface ISurveyAssignmentRepository
{
    Task<IReadOnlyList<Survey>> GetActiveSurveySummariesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default);

    Task<Survey?> GetSurveyWithScheduleAsync(
        NpgsqlConnection connection,
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganizationSelectionItem>> GetAvailableOrganizationsForSurveyAsync(
        NpgsqlConnection connection,
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganizationSelectionItem>> GetSelectedOrganizationsForSurveyAsync(
        NpgsqlConnection connection,
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<int> UpdateActiveSurveyPeriodAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTime dateBegin,
        DateTime dateEnd,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArchivedSurvey>> GetAdminArchivedSurveySummariesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default);

    Task<ArchivedSurvey?> GetArchivedSurveyForCopyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveSurveysAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyAssignmentTableRow>> GetActiveSurveyPageAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SelectionOption>> GetActiveOrganizationOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default);

    Task<int> CountArchivedSurveysAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> surveyIds,
        DateTime? dateStart,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyAssignmentTableRow>> GetArchivedSurveyPageAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> surveyIds,
        DateTime? dateStart,
        DateTime? dateEnd,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SelectionOption>> GetArchivedOrganizationOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SelectionOption>> GetArchivedSurveyOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default);

    Task<int?> GetUserOrganizationIdAsync(
        NpgsqlConnection connection,
        int userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsActiveAssignmentAsync(
        NpgsqlConnection connection,
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default);

    Task<UserSurveyAssignmentPageData> GetActiveUserSurveyPageAsync(
        NpgsqlConnection connection,
        int organizationId,
        string searchTerm,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default);

    Task<UserSurveyAssignmentPageData> GetUserArchivePageAsync(
        NpgsqlConnection connection,
        int organizationId,
        string searchTerm,
        DateTime? exactCompletionDate,
        DateTime? completionDateFrom,
        DateTime? completionDateTo,
        bool signedOnly,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default);

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

    Task<int> UpsertSurveyEndDateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId,
        DateTime dateEnd,
        CancellationToken cancellationToken = default);

    Task<int?> GetAssignmentIdForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default);
}
