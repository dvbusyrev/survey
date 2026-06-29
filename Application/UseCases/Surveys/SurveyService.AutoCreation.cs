using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Configuration;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;
using Npgsql;

namespace MainProject.Application.UseCases.Surveys;

public partial class SurveyService
{
    private const int SingletonConfigId = 1;
    private const string DefaultPattern = "1-monday";
    private const int DefaultCreationDayId = 1;
    private const int DefaultBeginDayId = 1;
    private static readonly int? DefaultWorkingPeriod = null;
    private const string StorageUnavailableMessage = "Автосоздание анкет недоступно: в базе данных ещё не применена актуальная миграция настроек автосоздания.";

    public async Task<SurveyAutoCreationPageViewModel> GetPageModelAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await _surveyRepository.HasCurrentAutoCreationStorageAsync(connection, null, cancellationToken))
        {
            _logger.LogWarning("Страница автосоздания открыта до применения актуальной миграции таблиц автосоздания.");
            return BuildDefaultPageModel();
        }

        var config = await GetOrCreateConfigurationAsync(connection, null, cancellationToken, lockRow: false);
        var selectedSurveys = await _surveyRepository.GetSelectedAutoCreationSurveysAsync(
            connection, null, SingletonConfigId, cancellationToken);

        return new SurveyAutoCreationPageViewModel
        {
            CreationPattern = config.CreationPattern,
            StartPattern = config.StartPattern,
            EndOffsetBusinessDays = config.WorkingPeriod,
            IsEnabled = config.IsEnabled,
            SelectedSurveys = selectedSurveys
                .Select(static survey => new SurveyAutoCreationSelectedSurveyViewModel
                {
                    Id = survey.Id,
                    Name = survey.Name
                })
                .ToArray()
        };
    }

    public async Task<IReadOnlyList<SurveySelectionItem>> GetSurveyOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _surveyRepository.GetSurveySelectionOptionsAsync(connection, cancellationToken: cancellationToken);
    }

    public Task<SurveyAutoCreationCommandResult> SaveAsync(SurveyAutoCreationSettingsRequest? request, CancellationToken cancellationToken = default)
        => SaveInternalAsync(request, enableOverride: null, runImmediately: false, cancellationToken);

    public Task<SurveyAutoCreationCommandResult> StartAsync(SurveyAutoCreationSettingsRequest? request, CancellationToken cancellationToken = default)
        => SaveInternalAsync(request, enableOverride: true, runImmediately: true, cancellationToken);

    public async Task<SurveyAutoCreationCommandResult> StopAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await _surveyRepository.HasCurrentAutoCreationStorageAsync(connection, null, cancellationToken))
        {
            return BuildStorageUnavailableCommandResult();
        }

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await GetOrCreateConfigurationAsync(connection, transaction, cancellationToken, lockRow: true);

        await _surveyRepository.SetAutoCreationEnabledAsync(connection, transaction, SingletonConfigId, false, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new SurveyAutoCreationCommandResult
        {
            Success = true,
            Message = "Автосоздание анкет остановлено.",
            IsEnabled = false,
            SelectedSurveyCount = await _surveyRepository.GetSelectedAutoCreationSurveyCountAsync(connection, SingletonConfigId, cancellationToken)
        };
    }

    public virtual async Task<SurveyAutoCreationRunResult> RunPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await _surveyRepository.HasCurrentAutoCreationStorageAsync(connection, null, cancellationToken))
        {
            _logger.LogWarning("Фоновое автосоздание анкет пропущено: таблицы автосоздания отсутствуют или не обновлены до актуальной схемы.");
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = false
            };
        }

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var config = await GetOrCreateConfigurationAsync(connection, transaction, cancellationToken, lockRow: true);
        if (!config.IsEnabled)
        {
            await transaction.CommitAsync(cancellationToken);
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = false
            };
        }

        var surveyIdArray = (await _surveyRepository.GetSelectedAutoCreationSurveyIdsAsync(
            connection, transaction, SingletonConfigId, cancellationToken)).ToArray();
        if (surveyIdArray.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = true
            };
        }

        var today = _clock.Today.Date;
        if (!TryParseDayOfWeek(config.CreationDayName, out var creationDayOfWeek)
            || !TryParseDayOfWeek(config.BeginDayName, out var beginDayOfWeek)
            || !SurveyAutoCreationScheduleHelper.TryResolveMonthWeekdayDate(today.Year, today.Month, config.CreationWeekNumber, creationDayOfWeek, out var creationDate)
            || !SurveyAutoCreationScheduleHelper.TryResolveMonthWeekdayDate(today.Year, today.Month, config.BeginWeekNumber, beginDayOfWeek, out var startDate))
        {
            await transaction.CommitAsync(cancellationToken);
            _logger.LogWarning("Конфигурация автосоздания содержит некорректные дни расписания.");
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = true
            };
        }

        if (today < creationDate.Date)
        {
            await transaction.CommitAsync(cancellationToken);
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = true,
                WasDue = today >= creationDate.Date,
                ScheduleDate = creationDate.Date
            };
        }

        var endDate = config.WorkingPeriod.HasValue
            ? SurveyAutoCreationScheduleHelper.AddBusinessDays(startDate.Date, config.WorkingPeriod.Value)
            : (DateTime?)null;
        var createdCount = 0;
        foreach (var surveyId in surveyIdArray)
        {
            var created = await CopySurveyTemplateAsync(connection, transaction, surveyId, startDate.Date, endDate?.Date, cancellationToken);
            if (created)
            {
                createdCount += 1;
            }
        }

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Автосоздание анкет выполнилось успешно. Создано копий: {Count}, дата запуска периода: {CreationDate}, дата начала: {StartDate}, дата конца: {EndDate}",
            createdCount,
            creationDate.Date,
            startDate.Date,
            endDate?.Date);

        return new SurveyAutoCreationRunResult
        {
            IsEnabled = true,
            WasDue = true,
            Processed = true,
            CreatedSurveyCount = createdCount,
            ScheduleDate = creationDate.Date
        };
    }
}
