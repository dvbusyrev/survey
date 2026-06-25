using MainProject.Application.DTO;
using MainProject.Application.DTO.Configuration;
using Npgsql;

namespace MainProject.Application.Contracts;

public interface IAutoCreationConfigRepository
{
    Task<bool> HasCurrentStorageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<AutoCreationConfigRecord> GetOrCreateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int configId,
        int defaultCreationDayId,
        int defaultBeginDayId,
        int? defaultWorkingPeriod,
        bool lockRow,
        CancellationToken cancellationToken = default);

    Task SetEnabledAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int configId,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int configId,
        int creationDayId,
        int beginDayId,
        int? workingPeriod,
        bool isEnabled,
        IReadOnlyCollection<int> surveyIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetSelectedSurveyIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int configId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveySelectionItem>> GetSelectedSurveysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int configId,
        CancellationToken cancellationToken = default);

    Task<int> GetSelectedSurveyCountAsync(
        NpgsqlConnection connection,
        int configId,
        CancellationToken cancellationToken = default);

    Task<int?> GetWeekDayIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int weekNumber,
        string dayName,
        CancellationToken cancellationToken = default);
}
