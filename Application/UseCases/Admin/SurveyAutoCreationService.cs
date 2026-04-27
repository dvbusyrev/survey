using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;
using Npgsql;

namespace MainProject.Application.UseCases.Admin;

public sealed class SurveyAutoCreationService : ISurveyAutoCreationService
{
    private const int SingletonConfigId = 1;
    private const string DefaultPattern = "1-monday";
    private const int DefaultBusinessDayOffset = 8;
    private const string StorageUnavailableMessage = "Автосоздание анкет недоступно: в базе данных ещё не применена миграция настроек автосоздания.";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SurveyAutoCreationService> _logger;

    public SurveyAutoCreationService(IDbConnectionFactory connectionFactory, ILogger<SurveyAutoCreationService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<SurveyAutoCreationPageViewModel> GetPageModelAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (!await HasStorageAsync(connection, null, cancellationToken))
        {
            _logger.LogWarning("Страница автосоздания открыта до применения миграции таблиц автосоздания.");
            return BuildDefaultPageModel();
        }

        var config = await GetOrCreateConfigurationAsync(connection, null, cancellationToken, lockRow: false);
        var selectedSurveys = await GetSelectedSurveysAsync(connection, null, cancellationToken);

        return new SurveyAutoCreationPageViewModel
        {
            CreationPattern = config.CreationPattern,
            StartPattern = config.StartPattern,
            EndOffsetBusinessDays = config.EndOffsetBusinessDays,
            IsEnabled = config.IsEnabled,
            LastProcessedScheduleDate = config.LastProcessedScheduleDate,
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
        using var connection = _connectionFactory.CreateConnection();
        var surveys = await connection.QueryAsync<SurveySelectionItem>(
            new CommandDefinition(
                """
                SELECT
                    s.id_survey AS Id,
                    s.name_survey AS Name
                FROM public.survey s
                ORDER BY lower(s.name_survey), s.id_survey;
                """,
                cancellationToken: cancellationToken));

        return surveys.ToArray();
    }

    public Task<SurveyAutoCreationCommandResult> SaveAsync(SurveyAutoCreationSettingsRequest? request, CancellationToken cancellationToken = default)
        => SaveInternalAsync(request, enableOverride: null, runImmediately: false, cancellationToken);

    public Task<SurveyAutoCreationCommandResult> StartAsync(SurveyAutoCreationSettingsRequest? request, CancellationToken cancellationToken = default)
        => SaveInternalAsync(request, enableOverride: true, runImmediately: true, cancellationToken);

    public async Task<SurveyAutoCreationCommandResult> StopAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (!await HasStorageAsync(connection, null, cancellationToken))
        {
            return BuildStorageUnavailableCommandResult();
        }

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await GetOrCreateConfigurationAsync(connection, transaction, cancellationToken, lockRow: true);

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE public.survey_auto_creation_config
                SET is_enabled = FALSE
                WHERE id_config = @ConfigId;
                """,
                new { ConfigId = SingletonConfigId },
                transaction,
                cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        return new SurveyAutoCreationCommandResult
        {
            Success = true,
            Message = "Автосоздание анкет остановлено.",
            IsEnabled = false,
            SelectedSurveyCount = await GetSelectedSurveyCountAsync(connection, cancellationToken)
        };
    }

    public async Task<SurveyAutoCreationRunResult> RunPendingAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (!await HasStorageAsync(connection, null, cancellationToken))
        {
            _logger.LogWarning("Фоновое автосоздание анкет пропущено: таблицы автосоздания отсутствуют в базе данных.");
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

        var selectedSurveyIds = await connection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT id_survey
                FROM public.survey_auto_creation_config_survey
                WHERE id_config = @ConfigId
                ORDER BY id_survey;
                """,
                new { ConfigId = SingletonConfigId },
                transaction,
                cancellationToken: cancellationToken));

        var surveyIdArray = selectedSurveyIds.ToArray();
        if (surveyIdArray.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = true
            };
        }

        var today = DateTime.Today;
        if (!SurveyAutoCreationScheduleHelper.TryResolveMonthWeekdayDate(today.Year, today.Month, config.CreationPattern, out var creationDate)
            || !SurveyAutoCreationScheduleHelper.TryResolveMonthWeekdayDate(today.Year, today.Month, config.StartPattern, out var startDate))
        {
            await transaction.CommitAsync(cancellationToken);
            _logger.LogWarning("Конфигурация автосоздания содержит некорректные шаблоны дат.");
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = true
            };
        }

        if (today < creationDate.Date || config.LastProcessedScheduleDate?.Date == creationDate.Date)
        {
            await transaction.CommitAsync(cancellationToken);
            return new SurveyAutoCreationRunResult
            {
                IsEnabled = true,
                WasDue = today >= creationDate.Date,
                ScheduleDate = creationDate.Date
            };
        }

        var endDate = SurveyAutoCreationScheduleHelper.AddBusinessDays(startDate.Date, config.EndOffsetBusinessDays);
        var createdCount = 0;
        foreach (var surveyId in surveyIdArray)
        {
            await CopySurveyTemplateAsync(connection, transaction, surveyId, startDate.Date, endDate.Date, cancellationToken);
            createdCount += 1;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE public.survey_auto_creation_config
                SET last_processed_schedule_date = @LastProcessedScheduleDate
                WHERE id_config = @ConfigId;
                """,
                new
                {
                    ConfigId = SingletonConfigId,
                    LastProcessedScheduleDate = creationDate.Date
                },
                transaction,
                cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Автосоздание анкет выполнилось успешно. Создано копий: {Count}, дата запуска периода: {CreationDate}, дата начала: {StartDate}, дата конца: {EndDate}",
            createdCount,
            creationDate.Date,
            startDate.Date,
            endDate.Date);

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
        var normalizeResult = await TryNormalizeRequestAsync(request, cancellationToken);
        if (!normalizeResult.IsValid)
        {
            return new SurveyAutoCreationCommandResult
            {
                Message = normalizeResult.ValidationError
            };
        }

        var normalizedRequest = normalizeResult.NormalizedRequest;

        using var connection = _connectionFactory.CreateConnection();
        if (!await HasStorageAsync(connection, null, cancellationToken))
        {
            return BuildStorageUnavailableCommandResult();
        }

        using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var current = await GetOrCreateConfigurationAsync(connection, transaction, cancellationToken, lockRow: true);
            var isEnabled = enableOverride ?? current.IsEnabled;

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO public.survey_auto_creation_config
                    (
                        id_config,
                        creation_pattern,
                        start_pattern,
                        end_offset_business_days,
                        is_enabled,
                        last_processed_schedule_date
                    )
                    VALUES
                    (
                        @IdConfig,
                        @CreationPattern,
                        @StartPattern,
                        @EndOffsetBusinessDays,
                        @IsEnabled,
                        @LastProcessedScheduleDate
                    )
                    ON CONFLICT (id_config) DO UPDATE
                    SET
                        creation_pattern = EXCLUDED.creation_pattern,
                        start_pattern = EXCLUDED.start_pattern,
                        end_offset_business_days = EXCLUDED.end_offset_business_days,
                        is_enabled = EXCLUDED.is_enabled,
                        last_processed_schedule_date = public.survey_auto_creation_config.last_processed_schedule_date;
                    """,
                    new
                    {
                        IdConfig = SingletonConfigId,
                        normalizedRequest.CreationPattern,
                        normalizedRequest.StartPattern,
                        normalizedRequest.EndOffsetBusinessDays,
                        IsEnabled = isEnabled,
                        LastProcessedScheduleDate = current.LastProcessedScheduleDate
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    DELETE FROM public.survey_auto_creation_config_survey
                    WHERE id_config = @ConfigId;
                    """,
                    new { ConfigId = SingletonConfigId },
                    transaction,
                    cancellationToken: cancellationToken));

            foreach (var surveyId in normalizedRequest.SurveyIds)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO public.survey_auto_creation_config_survey (id_config, id_survey)
                        VALUES (@ConfigId, @SurveyId);
                        """,
                        new
                        {
                            ConfigId = SingletonConfigId,
                            SurveyId = surveyId
                        },
                        transaction,
                        cancellationToken: cancellationToken));
            }

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
        SurveyAutoCreationSettingsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Параметры автосоздания не переданы."
            };
        }

        var creationPattern = (request.CreationPattern ?? string.Empty).Trim().ToLowerInvariant();
        var startPattern = (request.StartPattern ?? string.Empty).Trim().ToLowerInvariant();
        var surveyIds = (request.SurveyIds ?? new List<int>())
            .Where(static id => id > 0)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();

        if (!SurveyAutoCreationScheduleHelper.IsValidMonthWeekdayPattern(creationPattern))
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Некорректное значение поля «Создание анкеты»."
            };
        }

        if (!SurveyAutoCreationScheduleHelper.IsValidMonthWeekdayPattern(startPattern))
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Некорректное значение поля «Дата начала»."
            };
        }

        if (request.EndOffsetBusinessDays is < 8 or > 20)
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Поле «Дата конца» должно быть в диапазоне от +8 до +20 рабочих дней."
            };
        }

        if (surveyIds.Length == 0)
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Выберите хотя бы одну анкету."
            };
        }

        using var connection = _connectionFactory.CreateConnection();
        var existingSurveyIds = (await connection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT s.id_survey
                FROM public.survey s
                WHERE s.id_survey = ANY(@SurveyIds);
                """,
                new { SurveyIds = surveyIds },
                cancellationToken: cancellationToken))).ToHashSet();

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
                EndOffsetBusinessDays = request.EndOffsetBusinessDays,
                SurveyIds = surveyIds.ToList()
            }
        };
    }

    private async Task<SurveyAutoCreationConfigRow> GetOrCreateConfigurationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken,
        bool lockRow)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO public.survey_auto_creation_config
                (
                    id_config,
                    creation_pattern,
                    start_pattern,
                    end_offset_business_days,
                    is_enabled
                )
                VALUES
                (
                    @ConfigId,
                    @CreationPattern,
                    @StartPattern,
                    @EndOffsetBusinessDays,
                    FALSE
                )
                ON CONFLICT (id_config) DO NOTHING;
                """,
                new
                {
                    ConfigId = SingletonConfigId,
                    CreationPattern = DefaultPattern,
                    StartPattern = DefaultPattern,
                    EndOffsetBusinessDays = DefaultBusinessDayOffset
                },
                transaction,
                cancellationToken: cancellationToken));

        var lockClause = lockRow ? "FOR UPDATE" : string.Empty;
        return await connection.QuerySingleAsync<SurveyAutoCreationConfigRow>(
            new CommandDefinition(
                $"""
                SELECT
                    id_config AS IdConfig,
                    creation_pattern AS CreationPattern,
                    start_pattern AS StartPattern,
                    end_offset_business_days AS EndOffsetBusinessDays,
                    is_enabled AS IsEnabled,
                    last_processed_schedule_date AS LastProcessedScheduleDate
                FROM public.survey_auto_creation_config
                WHERE id_config = @ConfigId
                {lockClause};
                """,
                new { ConfigId = SingletonConfigId },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static Task<IReadOnlyList<SurveySelectionItem>> GetSelectedSurveysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
        => QuerySelectedSurveysAsync(connection, transaction, cancellationToken);

    private static async Task<IReadOnlyList<SurveySelectionItem>> QuerySelectedSurveysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var surveys = await connection.QueryAsync<SurveySelectionItem>(
            new CommandDefinition(
                """
                SELECT
                    s.id_survey AS Id,
                    s.name_survey AS Name
                FROM public.survey_auto_creation_config_survey cs
                INNER JOIN public.survey s
                    ON s.id_survey = cs.id_survey
                WHERE cs.id_config = @ConfigId
                ORDER BY lower(s.name_survey), s.id_survey;
                """,
                new { ConfigId = SingletonConfigId },
                transaction,
                cancellationToken: cancellationToken));

        return surveys.ToArray();
    }

    private async Task<int> GetSelectedSurveyCountAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM public.survey_auto_creation_config_survey
                WHERE id_config = @ConfigId;
                """,
                new { ConfigId = SingletonConfigId },
                cancellationToken: cancellationToken));
    }

    private async Task<bool> GetIsEnabledAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var config = await GetOrCreateConfigurationAsync(connection, null, cancellationToken, lockRow: false);
        return config.IsEnabled;
    }

    private async Task<bool> HasStorageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT
                    to_regclass('public.survey_auto_creation_config') IS NOT NULL
                    AND to_regclass('public.survey_auto_creation_config_survey') IS NOT NULL;
                """,
                transaction: transaction,
                cancellationToken: cancellationToken));
    }

    private static SurveyAutoCreationPageViewModel BuildDefaultPageModel()
    {
        return new SurveyAutoCreationPageViewModel
        {
            CreationPattern = DefaultPattern,
            StartPattern = DefaultPattern,
            EndOffsetBusinessDays = DefaultBusinessDayOffset,
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

    private static async Task CopySurveyTemplateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var originalSurvey = await connection.QueryFirstOrDefaultAsync<SurveyCopySourceRow>(
            new CommandDefinition(
                """
                SELECT
                    s.id_survey AS IdSurvey,
                    s.name_survey AS NameSurvey,
                    s.description AS Description
                FROM public.survey s
                WHERE s.id_survey = @SurveyId;
                """,
                new { SurveyId = surveyId },
                transaction,
                cancellationToken: cancellationToken));

        if (originalSurvey == null)
        {
            throw new InvalidOperationException($"Анкета {surveyId} не найдена для автосоздания.");
        }

        var newSurveyId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO public.survey (name_survey, description)
                VALUES (@NameSurvey, @Description)
                RETURNING id_survey;
                """,
                new
                {
                    NameSurvey = $"{originalSurvey.NameSurvey} (Копия)",
                    originalSurvey.Description
                },
                transaction,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO public.survey_question (id_survey, question_order, question_text)
                SELECT @NewSurveyId, question_order, question_text
                FROM public.survey_question
                WHERE id_survey = @SourceSurveyId
                ON CONFLICT (id_survey, question_order) DO UPDATE
                SET question_text = EXCLUDED.question_text;
                """,
                new
                {
                    NewSurveyId = newSurveyId,
                    SourceSurveyId = surveyId
                },
                transaction,
                cancellationToken: cancellationToken));

        var organizationIds = (await connection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT id_organization
                FROM public.organization_survey
                WHERE id_survey = @SourceSurveyId
                ORDER BY id_organization;
                """,
                new { SourceSurveyId = surveyId },
                transaction,
                cancellationToken: cancellationToken))).ToArray();

        foreach (var organizationId in organizationIds.Distinct())
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO public.organization_survey (id_organization, id_survey, date_begin, date_end)
                    VALUES (@OrganizationId, @SurveyId, @DateBegin, @DateEnd)
                    ON CONFLICT (id_organization, id_survey) DO UPDATE
                    SET
                        date_begin = EXCLUDED.date_begin,
                        date_end = EXCLUDED.date_end;
                    """,
                    new
                    {
                        OrganizationId = organizationId,
                        SurveyId = newSurveyId,
                        DateBegin = startDate.Date,
                        DateEnd = endDate.Date
                    },
                    transaction,
                    cancellationToken: cancellationToken));
        }
    }

    private sealed class SurveyAutoCreationConfigRow
    {
        public int IdConfig { get; init; }
        public string CreationPattern { get; init; } = DefaultPattern;
        public string StartPattern { get; init; } = DefaultPattern;
        public int EndOffsetBusinessDays { get; init; } = DefaultBusinessDayOffset;
        public bool IsEnabled { get; init; }
        public DateTime? LastProcessedScheduleDate { get; init; }
    }

    private sealed class SurveyCopySourceRow
    {
        public int IdSurvey { get; init; }
        public string NameSurvey { get; init; } = string.Empty;
        public string? Description { get; init; }
    }

    private sealed class NormalizeSurveyAutoCreationRequestResult
    {
        public bool IsValid { get; init; }
        public SurveyAutoCreationSettingsRequest NormalizedRequest { get; init; } = new();
        public string ValidationError { get; init; } = string.Empty;
    }
}
