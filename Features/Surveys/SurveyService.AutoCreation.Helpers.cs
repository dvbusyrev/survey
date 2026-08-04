using MainProject.Application.DTO;
using MainProject.Application.DTO.Configuration;
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
            return InvalidAutoCreationRequest("Параметры автосоздания не переданы");
        }

        if (!SurveyAutoCreationScheduleHelper.TryNormalizeReportingPeriod(request.ReportingPeriod, out var reportingPeriod))
        {
            return InvalidAutoCreationRequest("Некорректное значение поля «Период отчётности».");
        }

        if (!IsValidBusinessDayPeriod(request.ReportingOffsetBusinessDays))
        {
            return InvalidAutoCreationRequest(
                "Поле «Период на отчётность» должно быть положительным целым числом.");
        }

        if (!IsValidBusinessDayPeriod(request.ActivePeriodBusinessDays))
        {
            return InvalidAutoCreationRequest(
                "Поле «Период действия» должно быть положительным целым числом.");
        }

        var surveyIds = (request.SurveyIds ?? [])
            .Where(static id => id > 0)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();
        if (surveyIds.Length == 0)
        {
            return InvalidAutoCreationRequest("Выберите хотя бы одну анкету.");
        }

        var existingSurveyIds = await _surveyRepository.GetExistingSurveyIdsAsync(
            connection,
            transaction,
            surveyIds,
            cancellationToken);
        if (existingSurveyIds.Count != surveyIds.Length)
        {
            return InvalidAutoCreationRequest("Одна или несколько выбранных анкет не найдены.");
        }

        var distinctNameCount = await _surveyRepository.GetDistinctSurveyNameCountAsync(
            connection,
            transaction,
            surveyIds,
            cancellationToken);
        if (distinctNameCount != surveyIds.Length)
        {
            return InvalidAutoCreationRequest("Анкеты с одинаковым названием нельзя выбирать несколько раз.");
        }

        return new NormalizeSurveyAutoCreationRequestResult
        {
            IsValid = true,
            NormalizedRequest = new SurveyAutoCreationSettingsRequest
            {
                ReportingPeriod = reportingPeriod,
                ReportingOffsetBusinessDays = request.ReportingOffsetBusinessDays,
                ActivePeriodBusinessDays = request.ActivePeriodBusinessDays,
                SurveyIds = surveyIds.ToList()
            }
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
            DefaultReportingPeriod,
            DefaultReportingOffsetBusinessDays,
            DefaultActivePeriodBusinessDays,
            lockRow,
            cancellationToken);

    private async Task<bool> GetIsEnabledAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var config = await GetOrCreateConfigurationAsync(connection, null, cancellationToken, lockRow: false);
        return config.IsEnabled;
    }

    private static bool IsValidBusinessDayPeriod(int value)
        => value >= SurveyAutoCreationScheduleHelper.MinBusinessDayPeriod;

    private static NormalizeSurveyAutoCreationRequestResult InvalidAutoCreationRequest(string message)
        => new() { ValidationError = message };

    private SurveyAutoCreationPageViewModel BuildDefaultPageModel()
        => new()
        {
            ReportingPeriod = DefaultReportingPeriod,
            ReportingOffsetBusinessDays = DefaultReportingOffsetBusinessDays,
            ActivePeriodBusinessDays = DefaultActivePeriodBusinessDays,
            PreviewYear = _clock.Today.Year,
            PreviewMonth = _clock.Today.Month,
            IsEnabled = false
        };

    private static SurveyAutoCreationPreviewResult InvalidPreview(string message)
        => new() { Message = message };

    private static SurveyAutoCreationCommandResult BuildStorageUnavailableCommandResult()
        => new() { Message = StorageUnavailableMessage };

    private async Task<bool> CopySurveyTemplateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var originalSurvey = await _surveyRepository.GetSurveyByIdAsync(
            connection,
            transaction,
            surveyId,
            cancellationToken);
        if (originalSurvey == null)
        {
            throw new InvalidOperationException($"Анкета {surveyId} не найдена для автосоздания.");
        }

        var alreadyCreated = await _surveyRepository.HasSurveyWithScheduleAsync(
            connection,
            transaction,
            originalSurvey.NameSurvey,
            startDate,
            endDate,
            cancellationToken);
        if (alreadyCreated)
        {
            return false;
        }

        var newSurveyId = await _surveyRepository.CreateSurveyAsync(
            connection,
            transaction,
            originalSurvey.NameSurvey,
            originalSurvey.Description,
            cancellationToken);
        await _surveyRepository.CopySurveyQuestionsAsync(
            connection,
            transaction,
            surveyId,
            newSurveyId,
            cancellationToken);

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
        public string ValidationError { get; init; } = string.Empty;
    }
}
