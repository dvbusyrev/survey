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

            await _surveyRepository.SaveAutoCreationConfigAsync(
                connection,
                transaction,
                SingletonConfigId,
                normalizeResult.CreationDayId,
                normalizeResult.BeginDayId,
                normalizedRequest.EndOffsetBusinessDays,
                isEnabled,
                normalizedRequest.SurveyIds,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        var commandResult = new SurveyAutoCreationCommandResult
        {
            Success = true,
            Message = enableOverride == true
                ? "Автосоздание анкет запущено."
                : "Настройки автосоздания анкет сохранены.",
            IsEnabled = false,
            SelectedSurveyCount = normalizedRequest.SurveyIds.Count
        };

        if (!runImmediately)
        {
            return new SurveyAutoCreationCommandResult
            {
                Success = true,
                Message = commandResult.Message,
                IsEnabled = enableOverride ?? (await GetIsEnabledAsync(cancellationToken)),
                SelectedSurveyCount = normalizedRequest.SurveyIds.Count
            };
        }

        var runResult = await RunPendingAsync(cancellationToken);
        if (runResult.Processed && runResult.CreatedSurveyCount > 0)
        {
            return new SurveyAutoCreationCommandResult
            {
                Success = true,
                Message = $"Автосоздание анкет запущено. Создано копий: {runResult.CreatedSurveyCount}.",
                IsEnabled = true,
                SelectedSurveyCount = normalizedRequest.SurveyIds.Count
            };
        }

        return new SurveyAutoCreationCommandResult
        {
            Success = true,
            Message = "Автосоздание анкет запущено.",
            IsEnabled = true,
            SelectedSurveyCount = normalizedRequest.SurveyIds.Count
        };
    }
}
