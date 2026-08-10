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
        out string title,
        out string description,
        out DateTime startDate,
        out DateTime endDate,
        out IReadOnlyList<int> organizationIds,
        out IReadOnlyList<SurveyQuestionRow> questionRows,
        out string validationError)
    {
        title = string.Empty;
        description = string.Empty;
        startDate = default;
        endDate = default;
        organizationIds = Array.Empty<int>();
        questionRows = Array.Empty<SurveyQuestionRow>();
        validationError = string.Empty;

        if (request == null)
        {
            validationError = "Данные анкеты не предоставлены.";
            return false;
        }

        title = request.Title?.Trim() ?? string.Empty;
        description = request.Description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            validationError = "Введите название анкеты.";
            return false;
        }

        if (!TryParseDateRange(request.StartDate, request.EndDate, out startDate, out endDate, out validationError))
        {
            return false;
        }

        if (!TryValidateEndDateNotPast(endDate, out validationError))
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
        out string title,
        out string description,
        out DateTime startDate,
        out DateTime endDate,
        out IReadOnlyList<int> organizationIds,
        out IReadOnlyList<SurveyQuestionRow> questionRows,
        out string validationError)
    {
        title = string.Empty;
        description = string.Empty;
        startDate = default;
        endDate = default;
        organizationIds = Array.Empty<int>();
        questionRows = Array.Empty<SurveyQuestionRow>();
        validationError = string.Empty;

        if (request == null)
        {
            validationError = "Данные анкеты не предоставлены.";
            return false;
        }

        title = request.Title?.Trim() ?? string.Empty;
        description = request.Description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            validationError = "Введите название анкеты.";
            return false;
        }

        if (!TryValidateDateRange(request.StartDate, request.EndDate, out validationError))
        {
            return false;
        }

        startDate = request.StartDate;
        endDate = request.EndDate;

        if (!TryValidateEndDateNotPast(endDate, out validationError))
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

            if (!DateTime.TryParse(extension.ExtendedUntil, out var endDate) || endDate.Date <= _clock.Today.Date)
            {
                errors.Add("Дата конца должна быть позже сегодняшней даты.");
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
