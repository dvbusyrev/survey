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
    private const string DefaultReportingPeriod = "month";
    private const int DefaultReportingOffsetBusinessDays = 1;
    private const int DefaultActivePeriodBusinessDays = 8;
    private const string StorageUnavailableMessage = "Автосоздание анкет недоступно: в базе данных ещё не применена актуальная миграция настроек автосоздания.";

    public async Task<SurveyAutoCreationPageViewModel> GetPageModelAsync(CancellationToken cancellationToken = default)
    {
        await PromotePlannedSurveyTemplatesAsync(cancellationToken);
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await _surveyRepository.HasCurrentAutoCreationStorageAsync(connection, null, cancellationToken))
        {
            _logger.LogWarning("Страница автосоздания открыта до применения актуальной миграции таблиц автосоздания.");
            return BuildDefaultPageModel();
        }

        var config = await GetOrCreateConfigurationAsync(connection, null, cancellationToken, lockRow: false);
        await _surveyRepository.RemoveInactiveAutoCreationTemplatesAsync(
            connection, null, SingletonConfigId, cancellationToken);
        var selectedTemplates = await _surveyRepository.GetSelectedAutoCreationTemplatesAsync(
            connection, null, SingletonConfigId, cancellationToken);

        return new SurveyAutoCreationPageViewModel
        {
            ReportingPeriod = config.ReportingPeriod,
            ReportingOffsetBusinessDays = config.ReportingOffsetBusinessDays,
            ActivePeriodBusinessDays = config.WorkingPeriod,
            PreviewYear = _clock.Today.Year,
            PreviewMonth = _clock.Today.Month,
            IsEnabled = config.IsEnabled,
            SelectedTemplates = selectedTemplates
                .Select(static template => new SurveyAutoCreationSelectedTemplateViewModel
                {
                    Id = template.Id,
                    Name = template.Name
                })
                .ToArray()
        };
    }

    public async Task<IReadOnlyList<SelectionOption>> GetTemplateOptionsAsync(CancellationToken cancellationToken = default)
    {
        await PromotePlannedSurveyTemplatesAsync(cancellationToken);
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _surveyRepository.GetAutoCreationTemplateSelectionOptionsAsync(
            connection,
            cancellationToken);
    }

    public async Task<SurveyAutoCreationPreviewResult> GetSchedulePreviewAsync(
        SurveyAutoCreationPreviewRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return InvalidPreview("Параметры календаря не переданы.");
        }

        if (!SurveyAutoCreationScheduleHelper.TryNormalizeReportingPeriod(request.ReportingPeriod, out var reportingPeriod))
        {
            return InvalidPreview("Некорректное значение поля «Период отчётности».");
        }

        if (!IsValidBusinessDayPeriod(request.ReportingOffsetBusinessDays)
            || !IsValidBusinessDayPeriod(request.ActivePeriodBusinessDays))
        {
            return InvalidPreview("Количество рабочих дней должно быть положительным целым числом.");
        }

        if (request.TargetYear is < 2000 or > 2100 || request.TargetMonth is < 1 or > 12)
        {
            return InvalidPreview("Некорректный месяц календаря.");
        }

        if (_productionCalendar == null)
        {
            throw new InvalidOperationException("Сервис производственного календаря не настроен.");
        }

        var targetMonth = new DateTime(request.TargetYear, request.TargetMonth, 1);
        var periods = new List<SurveyAutoCreationPreviewPeriod>(2);
        for (var monthOffset = 0; monthOffset < 2; monthOffset++)
        {
            var month = targetMonth.AddMonths(monthOffset);
            var schedule = await SurveyAutoCreationScheduleHelper.CalculateAsync(
                month,
                reportingPeriod,
                request.ReportingOffsetBusinessDays,
                request.ActivePeriodBusinessDays,
                _productionCalendar.IsBusinessDayAsync,
                cancellationToken);
            periods.Add(new SurveyAutoCreationPreviewPeriod
            {
                Year = month.Year,
                Month = month.Month,
                StartDate = schedule.StartDate.ToString("yyyy-MM-dd"),
                EndDate = schedule.EndDate.ToString("yyyy-MM-dd")
            });
        }

        return new SurveyAutoCreationPreviewResult
        {
            Success = true,
            TargetYear = targetMonth.Year,
            TargetMonth = targetMonth.Month,
            StartDate = periods[0].StartDate,
            EndDate = periods[0].EndDate,
            Periods = periods
        };
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
            SelectedTemplateCount = await _surveyRepository.GetSelectedAutoCreationTemplateCountAsync(connection, SingletonConfigId, cancellationToken)
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

        await _surveyRepository.PromotePlannedSurveyTemplatesAsync(
            connection,
            transaction,
            _clock.Today.Date,
            cancellationToken);
        var config = await GetOrCreateConfigurationAsync(connection, transaction, cancellationToken, lockRow: true);
        await _surveyRepository.RemoveInactiveAutoCreationTemplatesAsync(
            connection, transaction, SingletonConfigId, cancellationToken);
        if (!config.IsEnabled)
        {
            await transaction.CommitAsync(cancellationToken);
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = false
            };
        }

        var templateIdArray = (await _surveyRepository.GetSelectedAutoCreationTemplateIdsAsync(
            connection, transaction, SingletonConfigId, cancellationToken)).ToArray();
        if (templateIdArray.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = true
            };
        }

        if (_productionCalendar == null)
        {
            throw new InvalidOperationException("Сервис производственного календаря не настроен.");
        }

        var today = _clock.Today.Date;
        var schedule = await SurveyAutoCreationScheduleHelper.CalculateAsync(
            today,
            config.ReportingPeriod,
            config.ReportingOffsetBusinessDays,
            config.WorkingPeriod,
            _productionCalendar.IsBusinessDayAsync,
            cancellationToken);

        if (today < schedule.StartDate)
        {
            await transaction.CommitAsync(cancellationToken);
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = true,
                WasDue = false,
                ScheduleDate = schedule.StartDate
            };
        }

        var createdCount = 0;
        foreach (var templateId in templateIdArray)
        {
            var created = await CopySurveyTemplateAsync(
                connection,
                transaction,
                templateId,
                schedule.StartDate,
                schedule.EndDate,
                cancellationToken);
            if (created)
            {
                createdCount += 1;
            }
        }

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Автосоздание анкет выполнилось успешно. Создано копий: {Count}, дата начала: {StartDate}, дата конца: {EndDate}",
            createdCount,
            schedule.StartDate,
            schedule.EndDate);

        return new SurveyAutoCreationRunResult
        {
            IsEnabled = true,
            WasDue = true,
            Processed = true,
            CreatedSurveyCount = createdCount,
            ScheduleDate = schedule.StartDate
        };
    }

    public virtual async Task<int> PromotePlannedSurveyTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var promotedCount = await _surveyRepository.PromotePlannedSurveyTemplatesAsync(
                connection,
                transaction,
                _clock.Today.Date,
                cancellationToken);
            if (promotedCount > 0
                && await _surveyRepository.HasCurrentAutoCreationStorageAsync(connection, transaction, cancellationToken))
            {
                await GetOrCreateConfigurationAsync(connection, transaction, cancellationToken, lockRow: false);
                await _surveyRepository.RemoveInactiveAutoCreationTemplatesAsync(
                    connection,
                    transaction,
                    SingletonConfigId,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return promotedCount;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
