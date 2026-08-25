using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Configuration;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;
using Npgsql;

namespace MainProject.Application.UseCases.Surveys;

public partial class SurveyService
{
    private async Task<SurveyAutoCreationCommandResult> SaveInternalAsync(
        SurveyAutoCreationSettingsRequest? request,
        bool? enableOverride,
        bool runImmediately,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await _surveyRepository.HasCurrentAutoCreationStorageAsync(connection, null, cancellationToken))
        {
            return BuildStorageUnavailableCommandResult();
        }

        SurveyAutoCreationSettingsRequest normalizedRequest;
        var isEnabledAfterSave = false;

        using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var current = await GetOrCreateConfigurationAsync(connection, transaction, cancellationToken, lockRow: true);
            var normalizeResult = await TryNormalizeRequestAsync(
                connection,
                transaction,
                request,
                cancellationToken);
            if (!normalizeResult.IsValid)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyAutoCreationCommandResult
                {
                    Message = normalizeResult.ValidationError
                };
            }

            normalizedRequest = normalizeResult.NormalizedRequest;
            var isEnabled = enableOverride ?? current.IsEnabled;
            isEnabledAfterSave = isEnabled;

            await _surveyRepository.SaveAutoCreationConfigAsync(
                connection,
                transaction,
                SingletonConfigId,
                normalizedRequest.ReportingPeriod,
                normalizedRequest.ReportingOffsetBusinessDays,
                normalizedRequest.ActivePeriodBusinessDays,
                isEnabled,
                normalizedRequest.TemplateIds,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        var commandResult = new SurveyAutoCreationCommandResult
        {
            Success = true,
            Message = enableOverride == true
                ? "Новые настройки автосоздания применены, автосоздание анкет запущено."
                : "Новые настройки автосоздания применены.",
            IsEnabled = false,
            SelectedTemplateCount = normalizedRequest.TemplateIds.Count
        };

        if (!runImmediately && !isEnabledAfterSave)
        {
            return new SurveyAutoCreationCommandResult
            {
                Success = true,
                Message = commandResult.Message,
                IsEnabled = false,
                SelectedTemplateCount = normalizedRequest.TemplateIds.Count
            };
        }

        var runResult = await RunPendingAsync(cancellationToken);
        if (runResult.Processed && runResult.CreatedSurveyCount > 0)
        {
            var message = enableOverride == true
                ? $"Новые настройки автосоздания применены, автосоздание анкет запущено. Создано анкет: {runResult.CreatedSurveyCount}."
                : $"Новые настройки автосоздания применены. Создано анкет: {runResult.CreatedSurveyCount}.";
            return new SurveyAutoCreationCommandResult
            {
                Success = true,
                Message = message,
                IsEnabled = isEnabledAfterSave,
                SelectedTemplateCount = normalizedRequest.TemplateIds.Count
            };
        }

        return new SurveyAutoCreationCommandResult
        {
            Success = true,
            Message = commandResult.Message,
            IsEnabled = isEnabledAfterSave,
            SelectedTemplateCount = normalizedRequest.TemplateIds.Count
        };
    }
}
