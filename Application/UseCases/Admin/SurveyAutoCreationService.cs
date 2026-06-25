using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Configuration;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;
using Npgsql;

namespace MainProject.Application.UseCases.Admin;

public sealed class SurveyAutoCreationService : ISurveyAutoCreationService
{
    private const int SingletonConfigId = 1;
    private const string DefaultPattern = "1-monday";
    private const int DefaultCreationDayId = 1;
    private const int DefaultBeginDayId = 1;
    private static readonly int? DefaultWorkingPeriod = null;
    private const string StorageUnavailableMessage = "Автосоздание анкет недоступно: в базе данных ещё не применена актуальная миграция настроек автосоздания.";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SurveyAutoCreationService> _logger;
    private readonly IClock _clock;
    private readonly ISurveyAssignmentRepository _assignmentRepository;
    private readonly ISurveyDefinitionRepository _definitionRepository;
    private readonly IAutoCreationConfigRepository _configRepository;

    public SurveyAutoCreationService(
        IDbConnectionFactory connectionFactory,
        ILogger<SurveyAutoCreationService> logger,
        IClock clock,
        ISurveyAssignmentRepository assignmentRepository,
        ISurveyDefinitionRepository definitionRepository,
        IAutoCreationConfigRepository configRepository)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _clock = clock;
        _assignmentRepository = assignmentRepository;
        _definitionRepository = definitionRepository;
        _configRepository = configRepository;
    }

    public async Task<SurveyAutoCreationPageViewModel> GetPageModelAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await _configRepository.HasCurrentStorageAsync(connection, null, cancellationToken))
        {
            _logger.LogWarning("Страница автосоздания открыта до применения актуальной миграции таблиц автосоздания.");
            return BuildDefaultPageModel();
        }

        var config = await GetOrCreateConfigurationAsync(connection, null, cancellationToken, lockRow: false);
        var selectedSurveys = await _configRepository.GetSelectedSurveysAsync(
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
        return await _definitionRepository.GetSelectionOptionsAsync(connection, cancellationToken: cancellationToken);
    }

    public Task<SurveyAutoCreationCommandResult> SaveAsync(SurveyAutoCreationSettingsRequest? request, CancellationToken cancellationToken = default)
        => SaveInternalAsync(request, enableOverride: null, runImmediately: false, cancellationToken);

    public Task<SurveyAutoCreationCommandResult> StartAsync(SurveyAutoCreationSettingsRequest? request, CancellationToken cancellationToken = default)
        => SaveInternalAsync(request, enableOverride: true, runImmediately: true, cancellationToken);

    public async Task<SurveyAutoCreationCommandResult> StopAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await _configRepository.HasCurrentStorageAsync(connection, null, cancellationToken))
        {
            return BuildStorageUnavailableCommandResult();
        }

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await GetOrCreateConfigurationAsync(connection, transaction, cancellationToken, lockRow: true);

        await _configRepository.SetEnabledAsync(connection, transaction, SingletonConfigId, false, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new SurveyAutoCreationCommandResult
        {
            Success = true,
            Message = "Автосоздание анкет остановлено.",
            IsEnabled = false,
            SelectedSurveyCount = await _configRepository.GetSelectedSurveyCountAsync(connection, SingletonConfigId, cancellationToken)
        };
    }

    public async Task<SurveyAutoCreationRunResult> RunPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await _configRepository.HasCurrentStorageAsync(connection, null, cancellationToken))
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

        var surveyIdArray = (await _configRepository.GetSelectedSurveyIdsAsync(
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

    private async Task<SurveyAutoCreationCommandResult> SaveInternalAsync(
        SurveyAutoCreationSettingsRequest? request,
        bool? enableOverride,
        bool runImmediately,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await _configRepository.HasCurrentStorageAsync(connection, null, cancellationToken))
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

            await _configRepository.SaveAsync(
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

    private async Task<NormalizeSurveyAutoCreationRequestResult> TryNormalizeRequestAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        SurveyAutoCreationSettingsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Параметры автосоздания не переданы"
            };
        }

        var creationPattern = (request.CreationPattern ?? string.Empty).Trim().ToLowerInvariant();
        var startPattern = (request.StartPattern ?? string.Empty).Trim().ToLowerInvariant();
        var surveyIds = (request.SurveyIds ?? new List<int>())
            .Where(static id => id > 0)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();

        if (!SurveyAutoCreationScheduleHelper.TryParseMonthWeekdayPattern(creationPattern, out var creationWeekNumber, out var creationDayOfWeek))
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Некорректное значение поля «Создание анкеты»."
            };
        }

        if (!SurveyAutoCreationScheduleHelper.TryParseMonthWeekdayPattern(startPattern, out var beginWeekNumber, out var beginDayOfWeek))
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Некорректное значение поля «Дата начала»."
            };
        }

        var effectiveWorkingPeriod = request.EndOffsetBusinessDays;
        if (effectiveWorkingPeriod.HasValue
            && (effectiveWorkingPeriod.Value <= 0
                || effectiveWorkingPeriod.Value > SurveyAutoCreationScheduleHelper.MaxBusinessDayOffset))
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = $"Поле «Период действия» должно быть от 1 до {SurveyAutoCreationScheduleHelper.MaxBusinessDayOffset} рабочих дней."
            };
        }

        if (surveyIds.Length == 0)
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Выберите хотя бы одну анкету."
            };
        }

        var creationDayName = SurveyAutoCreationScheduleHelper.GetPatternWeekdayName(creationDayOfWeek);
        var beginDayName = SurveyAutoCreationScheduleHelper.GetPatternWeekdayName(beginDayOfWeek);
        var creationDayId = string.IsNullOrWhiteSpace(creationDayName)
            ? null
            : await _configRepository.GetWeekDayIdAsync(connection, transaction, creationWeekNumber, creationDayName, cancellationToken);
        var beginDayId = string.IsNullOrWhiteSpace(beginDayName)
            ? null
            : await _configRepository.GetWeekDayIdAsync(connection, transaction, beginWeekNumber, beginDayName, cancellationToken);
        if (creationDayId == null || beginDayId == null)
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Дни расписания не найдены в справочнике week_day."
            };
        }

        var existingSurveyIds = await _definitionRepository.GetExistingIdsAsync(
            connection, transaction, surveyIds, cancellationToken);

        if (existingSurveyIds.Count != surveyIds.Length)
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Одна или несколько выбранных анкет не найдены."
            };
        }

        return new NormalizeSurveyAutoCreationRequestResult
        {
            IsValid = true,
            NormalizedRequest = new SurveyAutoCreationSettingsRequest
            {
                CreationPattern = creationPattern,
                StartPattern = startPattern,
                EndOffsetBusinessDays = effectiveWorkingPeriod,
                SurveyIds = surveyIds.ToList()
            },
            CreationDayId = creationDayId.Value,
            BeginDayId = beginDayId.Value
        };
    }

    private Task<AutoCreationConfigRecord> GetOrCreateConfigurationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken,
        bool lockRow)
        => _configRepository.GetOrCreateAsync(
            connection,
            transaction,
            SingletonConfigId,
            DefaultCreationDayId,
            DefaultBeginDayId,
            DefaultWorkingPeriod,
            lockRow,
            cancellationToken);

    private static bool TryParseDayOfWeek(string? dayName, out DayOfWeek dayOfWeek)
        => Enum.TryParse(dayName, ignoreCase: true, out dayOfWeek)
           && dayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;

    private async Task<bool> GetIsEnabledAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var config = await GetOrCreateConfigurationAsync(connection, null, cancellationToken, lockRow: false);
        return config.IsEnabled;
    }


    private static SurveyAutoCreationPageViewModel BuildDefaultPageModel()
    {
        return new SurveyAutoCreationPageViewModel
        {
            CreationPattern = DefaultPattern,
            StartPattern = DefaultPattern,
            EndOffsetBusinessDays = DefaultWorkingPeriod,
            IsEnabled = false
        };
    }

    private static SurveyAutoCreationCommandResult BuildStorageUnavailableCommandResult()
    {
        return new SurveyAutoCreationCommandResult
        {
            Message = StorageUnavailableMessage
        };
    }

    private async Task<bool> CopySurveyTemplateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        DateTime startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var originalSurvey = await _definitionRepository.GetByIdAsync(
            connection, transaction, surveyId, cancellationToken);

        if (originalSurvey == null)
        {
            throw new InvalidOperationException($"Анкета {surveyId} не найдена для автосоздания.");
        }

        var copyName = $"{originalSurvey.NameSurvey} (Копия)";
        var alreadyCreated = await _assignmentRepository.HasSurveyWithScheduleAsync(
            connection,
            transaction,
            copyName,
            startDate,
            endDate,
            cancellationToken);

        if (alreadyCreated)
        {
            return false;
        }

        var newSurveyId = await _definitionRepository.CreateAsync(
            connection, transaction, copyName, originalSurvey.Description, cancellationToken);

        await _definitionRepository.CopyQuestionsAsync(
            connection, transaction, surveyId, newSurveyId, cancellationToken);

        var organizationIds = await _assignmentRepository.GetOrganizationIdsAsync(
            connection,
            transaction,
            surveyId,
            cancellationToken);
        await _assignmentRepository.UpsertSurveyAssignmentsAsync(
            connection,
            transaction,
            newSurveyId,
            organizationIds,
            startDate,
            endDate,
            cancellationToken);

        return true;
    }

    private sealed class NormalizeSurveyAutoCreationRequestResult
    {
        public bool IsValid { get; init; }
        public SurveyAutoCreationSettingsRequest NormalizedRequest { get; init; } = new();
        public int CreationDayId { get; init; }
        public int BeginDayId { get; init; }
        public string ValidationError { get; init; } = string.Empty;
    }
}
