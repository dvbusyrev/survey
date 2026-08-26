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
            return InvalidAutoCreationRequest("Параметры автосоздания не переданы.");
        }

        if (!SurveyAutoCreationScheduleHelper.TryNormalizeReportingPeriod(request.ReportingPeriod, out var reportingPeriod))
        {
            return InvalidAutoCreationRequest("Выберите период отчётности.");
        }

        if (!IsValidBusinessDayPeriod(request.ReportingOffsetBusinessDays))
        {
            return InvalidAutoCreationRequest(
                "Срок подготовки отчёта должен быть положительным целым числом.");
        }

        if (!IsValidBusinessDayPeriod(request.ActivePeriodBusinessDays))
        {
            return InvalidAutoCreationRequest(
                "Срок доступности анкет должен быть положительным целым числом.");
        }

        var templateIds = (request.TemplateIds ?? [])
            .Where(static id => id > 0)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();
        if (templateIds.Length == 0)
        {
            return InvalidAutoCreationRequest("Выберите хотя бы один шаблон.");
        }

        var availableTemplateIds = await _surveyRepository.GetAvailableAutoCreationTemplateIdsAsync(
            connection,
            transaction,
            templateIds,
            cancellationToken);
        if (availableTemplateIds.Count != templateIds.Length)
        {
            return InvalidAutoCreationRequest(
                "Один или несколько выбранных шаблонов не найдены, не активны или не назначены организациям.");
        }

        var hasConflictingNames = await _surveyRepository.HasConflictingAutoCreationTemplateNamesAsync(
            connection,
            transaction,
            templateIds,
            cancellationToken);
        if (hasConflictingNames)
        {
            return InvalidAutoCreationRequest("Шаблоны с одинаковым названием нельзя выбирать несколько раз.");
        }

        return new NormalizeSurveyAutoCreationRequestResult
        {
            IsValid = true,
            NormalizedRequest = new SurveyAutoCreationSettingsRequest
            {
                ReportingPeriod = reportingPeriod,
                ReportingOffsetBusinessDays = request.ReportingOffsetBusinessDays,
                ActivePeriodBusinessDays = request.ActivePeriodBusinessDays,
                TemplateIds = templateIds.ToList()
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
        int templateId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var template = await _surveyRepository.GetSurveyTemplateByIdAsync(
            connection,
            transaction,
            templateId,
            cancellationToken);
        if (template == null)
        {
            throw new InvalidOperationException($"Шаблон {templateId} не найден для автосоздания.");
        }

        var alreadyCreated = await _surveyRepository.HasSurveyForReportingMonthAsync(
            connection,
            transaction,
            template.NameSurvey,
            startDate,
            cancellationToken);
        if (alreadyCreated)
        {
            return false;
        }

        var newSurveyId = await _surveyRepository.CreateSurveyAsync(
            connection,
            transaction,
            template.NameSurvey,
            template.Description,
            startDate,
            endDate,
            cancellationToken);
        var questions = await _surveyRepository.GetSurveyTemplateQuestionsAsync(
            connection,
            transaction,
            templateId,
            cancellationToken);
        await _surveyRepository.ReplaceSurveyQuestionsAsync(
            connection,
            transaction,
            newSurveyId,
            questions,
            cancellationToken);

        var organizationIds = await _surveyRepository.GetOrganizationIdsForSurveyTemplateAsync(
            connection,
            transaction,
            templateId,
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
