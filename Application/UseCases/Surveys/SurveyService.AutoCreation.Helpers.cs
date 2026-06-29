using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Configuration;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;
using Npgsql;

namespace MainProject.Application.UseCases.Surveys;

public partial class SurveyService
{
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
            : await _surveyRepository.GetWeekDayIdAsync(connection, transaction, creationWeekNumber, creationDayName, cancellationToken);
        var beginDayId = string.IsNullOrWhiteSpace(beginDayName)
            ? null
            : await _surveyRepository.GetWeekDayIdAsync(connection, transaction, beginWeekNumber, beginDayName, cancellationToken);
        if (creationDayId == null || beginDayId == null)
        {
            return new NormalizeSurveyAutoCreationRequestResult
            {
                ValidationError = "Дни расписания не найдены в справочнике week_day."
            };
        }

        var existingSurveyIds = await _surveyRepository.GetExistingSurveyIdsAsync(
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
        => _surveyRepository.GetOrCreateAutoCreationConfigAsync(
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
        var originalSurvey = await _surveyRepository.GetSurveyByIdAsync(
            connection, transaction, surveyId, cancellationToken);

        if (originalSurvey == null)
        {
            throw new InvalidOperationException($"Анкета {surveyId} не найдена для автосоздания.");
        }

        var copyName = $"{originalSurvey.NameSurvey} (Копия)";
        var alreadyCreated = await _surveyRepository.HasSurveyWithScheduleAsync(
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

        var newSurveyId = await _surveyRepository.CreateSurveyAsync(
            connection, transaction, copyName, originalSurvey.Description, cancellationToken);

        await _surveyRepository.CopySurveyQuestionsAsync(
            connection, transaction, surveyId, newSurveyId, cancellationToken);

        var organizationIds = await _surveyRepository.GetOrganizationIdsAsync(
            connection,
            transaction,
            surveyId,
            cancellationToken);
        await _surveyRepository.UpsertSurveyAssignmentsAsync(
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
