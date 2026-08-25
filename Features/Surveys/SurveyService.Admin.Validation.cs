using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MainProject.Application.UseCases.Surveys;

public partial class SurveyService
{
    private bool TryValidateCreateRequest(
        SurveyAddRequest? request,
        bool allowOpenEndDate,
        out string title,
        out string description,
        out DateTime startDate,
        out DateTime? endDate,
        out IReadOnlyList<int> organizationIds,
        out IReadOnlyList<SurveyQuestionRow> questionRows,
        out string validationError)
    {
        title = string.Empty;
        description = string.Empty;
        startDate = default;
        endDate = null;
        organizationIds = Array.Empty<int>();
        questionRows = Array.Empty<SurveyQuestionRow>();
        validationError = string.Empty;

        if (request == null)
        {
            validationError = allowOpenEndDate
                ? "Данные шаблона не предоставлены."
                : "Данные анкеты не предоставлены.";
            return false;
        }

        title = request.Title?.Trim() ?? string.Empty;
        description = request.Description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            validationError = allowOpenEndDate
                ? "Введите название шаблона."
                : "Введите название анкеты.";
            return false;
        }

        if (allowOpenEndDate)
        {
            if (!TryParseOpenEndedDateRange(
                    request.StartDate,
                    request.EndDate,
                    out startDate,
                    out endDate,
                    out validationError))
            {
                return false;
            }
        }
        else if (!TryParseDateRange(
                     request.StartDate,
                     request.EndDate,
                     out startDate,
                     out var requiredEndDate,
                     out validationError))
        {
            return false;
        }
        else
        {
            endDate = requiredEndDate;
        }

        if (!TryValidateStartDateNotFuture(startDate, out validationError))
        {
            return false;
        }

        if (endDate.HasValue && !TryValidateEndDateNotPast(endDate.Value, out validationError))
        {
            return false;
        }

        if (!TryNormalizeOrganizationIds(request.Organizations, out organizationIds, out validationError))
        {
            return false;
        }

        return TryBuildQuestionRows(request.Criteria, out questionRows, out validationError);
    }

    private bool TryValidateUpdateRequest(
        SurveyUpdateRequest? request,
        bool allowOpenEndDate,
        out string title,
        out string description,
        out DateTime startDate,
        out DateTime? endDate,
        out IReadOnlyList<int> organizationIds,
        out IReadOnlyList<SurveyQuestionRow> questionRows,
        out string validationError)
    {
        title = string.Empty;
        description = string.Empty;
        startDate = default;
        endDate = null;
        organizationIds = Array.Empty<int>();
        questionRows = Array.Empty<SurveyQuestionRow>();
        validationError = string.Empty;

        if (request == null)
        {
            validationError = allowOpenEndDate
                ? "Данные шаблона не предоставлены."
                : "Данные анкеты не предоставлены.";
            return false;
        }

        title = request.Title?.Trim() ?? string.Empty;
        description = request.Description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            validationError = allowOpenEndDate
                ? "Введите название шаблона."
                : "Введите название анкеты.";
            return false;
        }

        if (request.StartDate == default)
        {
            validationError = "Укажите дату начала.";
            return false;
        }

        startDate = request.StartDate;
        endDate = request.EndDate;

        if (!endDate.HasValue && !allowOpenEndDate)
        {
            validationError = "Укажите дату конца.";
            return false;
        }

        if (endDate.HasValue
            && !TryValidateDateRange(startDate, endDate.Value, out validationError))
        {
            return false;
        }

        if (!TryValidateStartDateNotFuture(startDate, out validationError))
        {
            return false;
        }

        if (endDate.HasValue && !TryValidateEndDateNotPast(endDate.Value, out validationError))
        {
            return false;
        }

        if (!TryNormalizeOrganizationIds(request.Organizations, out organizationIds, out validationError))
        {
            return false;
        }

        return TryBuildQuestionRows(request.Criteria, out questionRows, out validationError);
    }

    private bool TryValidateCopyRequest(
        SurveyCopyRequest? request,
        out DateTime startDate,
        out DateTime endDate,
        out string validationError)
    {
        startDate = default;
        endDate = default;
        validationError = string.Empty;

        if (request == null)
        {
            validationError = "Данные для копирования анкеты не предоставлены.";
            return false;
        }

        if (!TryParseDateRange(request.StartDate, request.EndDate, out startDate, out endDate, out validationError))
        {
            return false;
        }

        if (!TryValidateStartDateNotFuture(startDate, out validationError))
        {
            return false;
        }

        return TryValidateEndDateNotPast(endDate, out validationError);
    }

    private IReadOnlyList<string> ValidateExtensionRequest(SurveyExtensionRequest request)
    {
        var errors = new List<string>();

        if (request.SurveyId <= 0)
        {
            errors.Add("Анкета не выбрана.");
        }

        foreach (var extension in request.Extensions)
        {
            if (extension.OrganizationId <= 0)
            {
                errors.Add("Организация не выбрана.");
            }

            if (!DateTime.TryParse(extension.ExtendedUntil, out var endDate))
            {
                errors.Add("Укажите корректную дату конца.");
            }
            else if (endDate.Date < _clock.Today.Date)
            {
                errors.Add("Дата конца не может быть раньше сегодняшней даты.");
            }
        }

        return errors;
    }

    private static bool TryParseDateRange(
        string? rawStartDate,
        string? rawEndDate,
        out DateTime startDate,
        out DateTime endDate,
        out string validationError)
    {
        startDate = default;
        endDate = default;
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(rawStartDate))
        {
            validationError = "Укажите дату начала.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rawEndDate))
        {
            validationError = "Укажите дату конца.";
            return false;
        }

        if (!DateTime.TryParse(rawStartDate, out startDate)
            || !DateTime.TryParse(rawEndDate, out endDate))
        {
            validationError = "Некорректный формат даты.";
            return false;
        }

        return TryValidateDateRange(startDate, endDate, out validationError);
    }

    private static bool TryParseOpenEndedDateRange(
        string? rawStartDate,
        string? rawEndDate,
        out DateTime startDate,
        out DateTime? endDate,
        out string validationError)
    {
        startDate = default;
        endDate = null;
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(rawStartDate))
        {
            validationError = "Укажите дату начала.";
            return false;
        }

        if (!DateTime.TryParse(rawStartDate, out startDate))
        {
            validationError = "Некорректный формат даты.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rawEndDate))
        {
            return true;
        }

        if (!DateTime.TryParse(rawEndDate, out var parsedEndDate))
        {
            validationError = "Некорректный формат даты.";
            return false;
        }

        endDate = parsedEndDate;
        return TryValidateDateRange(startDate, parsedEndDate, out validationError);
    }

    private static bool TryValidateDateRange(DateTime startDate, DateTime endDate, out string validationError)
    {
        validationError = string.Empty;

        if (startDate == default || endDate == default)
        {
            validationError = "Некорректный формат даты.";
            return false;
        }

        if (endDate <= startDate)
        {
            validationError = "Дата конца должна быть позже даты начала.";
            return false;
        }

        return true;
    }

    private bool TryValidateEndDateNotPast(DateTime endDate, out string validationError)
    {
        if (endDate.Date < _clock.Today.Date)
        {
            validationError = "Дата конца не может быть раньше сегодняшней даты.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private bool TryValidateStartDateNotFuture(DateTime startDate, out string validationError)
    {
        if (startDate.Date > _clock.Today.Date)
        {
            validationError = "Дата начала не может быть позже сегодняшней даты.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private static bool TryNormalizeOrganizationIds(
        IEnumerable<int>? rawOrganizationIds,
        out IReadOnlyList<int> organizationIds,
        out string validationError)
    {
        organizationIds = (rawOrganizationIds ?? Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (organizationIds.Count == 0)
        {
            validationError = "Выберите хотя бы одну организацию.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private static bool TryBuildQuestionRows(
        IEnumerable<string>? rawCriteria,
        out IReadOnlyList<SurveyQuestionRow> questionRows,
        out string validationError)
    {
        var criteria = (rawCriteria ?? Array.Empty<string>()).ToList();
        questionRows = Array.Empty<SurveyQuestionRow>();

        if (criteria.Count == 0)
        {
            validationError = "Добавьте хотя бы один критерий оценки.";
            return false;
        }

        if (criteria.Any(string.IsNullOrWhiteSpace))
        {
            validationError = "Заполните все критерии оценки или удалите пустые поля.";
            return false;
        }

        questionRows = criteria
            .Select((text, index) => new SurveyQuestionRow
            {
                QuestionOrder = index + 1,
                QuestionText = text.Trim()
            })
            .ToList();

        validationError = string.Empty;
        return true;
    }
}
