using System.Text;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Organization;
using MainProject.Application.Support;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Admin;

public sealed class OrganizationManagementService : IOrganizationManagementService
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationManagementService(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public OrganizationListPageViewModel GetActiveOrganizationsPage(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool openAddOrganizationModal = false)
    {
        return GetOrganizationsPage(currentPage, sortBy, sortDirection, includeArchived: false, openAddOrganizationModal);
    }

    public OrganizationSurveyAssignmentsPageViewModel GetOrganizationSurveyAssignmentsPage()
    {
        var rows = _organizationRepository.GetLatestUnansweredAssignments();

        return new OrganizationSurveyAssignmentsPageViewModel
        {
            Organizations = rows
                .GroupBy(row => new { row.OrganizationId, row.OrganizationName })
                .Select(group => new OrganizationSurveyGroupViewModel
                {
                    OrganizationId = group.Key.OrganizationId,
                    OrganizationName = group.Key.OrganizationName,
                    Surveys = group
                        .Where(row =>
                            row.SurveyId.HasValue
                            && row.AssignmentDateEnd.HasValue
                            && !string.IsNullOrWhiteSpace(row.SurveyName))
                        .Select(MapOrganizationSurveyItem)
                        .ToList()
                })
                .ToList()
        };
    }

    public OrganizationListPageViewModel GetArchivedOrganizationsPage(
        int currentPage,
        string? sortBy,
        string? sortDirection)
    {
        return GetOrganizationsPage(currentPage, sortBy, sortDirection, includeArchived: true);
    }

    private OrganizationListPageViewModel GetOrganizationsPage(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool includeArchived,
        bool openAddOrganizationModal = false)
    {
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeOrganizationSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeOrganizationSortDirection(null, normalizedSortBy);

        var totalCount = _organizationRepository.Count(includeArchived);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var organizations = _organizationRepository.GetPage(
            includeArchived,
            normalizedSortBy,
            normalizedSortDirection,
            pageWindow.PageSize,
            pageWindow.Offset);

        return new OrganizationListPageViewModel
        {
            Organizations = organizations,
            OpenAddOrganizationModal = openAddOrganizationModal,
            CurrentPage = pageWindow.CurrentPage,
            TotalPages = pageWindow.TotalPages,
            TotalCount = pageWindow.TotalCount,
            PageSize = pageWindow.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            ViewModeIsArchive = includeArchived
        };
    }

    public IReadOnlyList<Organization> GetArchivedOrganizations()
    {
        return _organizationRepository.GetAll(includeArchived: true);
    }

    public IReadOnlyList<OrganizationDataResponse> GetOrganizationOptions()
    {
        return _organizationRepository.GetActiveOptions();
    }

    public Organization? GetOrganizationById(int id)
    {
        return _organizationRepository.GetById(id);
    }

    public OperationResult CreateOrganization(OrganizationSaveRequest request)
    {
        if (!TryValidateOrganizationRequest(request, out var dateBegin, out var dateEnd, out var validationError))
        {
            return new OperationResult
            {
                Success = false,
                Message = validationError,
                Error = validationError
            };
        }

        var organizationId = _organizationRepository.Create(ToWriteModel(request, dateBegin, dateEnd));

        return new OperationResult
        {
            Success = true,
            Message = "Организация успешно создана",
            EntityId = organizationId,
            ShouldReload = true
        };
    }

    public OperationResult UpdateOrganization(int id, OrganizationSaveRequest request)
    {
        if (!TryValidateOrganizationRequest(request, out var dateBegin, out var dateEnd, out var validationError))
        {
            return new OperationResult
            {
                Success = false,
                Message = validationError,
                Error = validationError
            };
        }

        var affectedRows = _organizationRepository.Update(id, ToWriteModel(request, dateBegin, dateEnd));

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Организация успешно обновлена"
                : "Организация не найдена."
        };
    }

    public OperationResult ArchiveOrganization(int id)
    {
        var result = _organizationRepository.ArchiveIfUnused(id);
        if (!result.Found)
        {
            return new OperationResult
            {
                Success = false,
                Message = "Произошла ошибка при удалении организации."
            };
        }

        if (result.SurveyNames.Count > 0 || result.UserNames.Count > 0)
        {
            var message = BuildArchiveRestrictedMessage(result.SurveyNames, result.UserNames);

            return new OperationResult
            {
                Success = false,
                Message = message,
                Error = message,
                Code = "organization_in_use"
            };
        }

        return new OperationResult
        {
            Success = result.Archived,
            Message = result.Archived
                ? "Организация успешно удалена"
                : "Произошла ошибка при удалении организации."
        };
    }

    public OrganizationSurveyEndDateUpdateResult UpdateOrganizationSurveyEndDates(
        OrganizationSurveyEndDateUpdateRequest request)
    {
        var validationErrors = ValidateOrganizationSurveyEndDateUpdateRequest(request);
        if (validationErrors.Count > 0)
        {
            return new OrganizationSurveyEndDateUpdateResult
            {
                Success = false,
                Message = "Не удалось обновить дату конца анкет.",
                Errors = validationErrors
            };
        }

        var assignments = request.Assignments
            .Where(item => item.OrganizationId > 0 && item.SurveyId > 0)
            .DistinctBy(item => (item.OrganizationId, item.SurveyId))
            .ToList();

        var requestedEndDate = DateTime.Parse(request.DateEnd).Date;

        var organizationIds = assignments.Select(item => item.OrganizationId).Distinct().ToArray();
        var assignmentLookup = _organizationRepository.GetLatestUnansweredAssignments(organizationIds)
            .Where(row => row.SurveyId.HasValue)
            .ToDictionary(
                row => (row.OrganizationId, row.SurveyId!.Value),
                row => row);

        var missingAssignments = assignments
            .Where(item => !assignmentLookup.ContainsKey((item.OrganizationId, item.SurveyId)))
            .Select(item => $"Связка организации {item.OrganizationId} и анкеты {item.SurveyId} не найдена.")
            .ToList();

        if (missingAssignments.Count > 0)
        {
            return new OrganizationSurveyEndDateUpdateResult
            {
                Success = false,
                Message = "Не удалось обновить дату конца анкет.",
                Errors = missingAssignments
            };
        }

        try
        {
            if (!_organizationRepository.UpdateAssignmentEndDates(
                    assignments.Select(item => (item.OrganizationId, item.SurveyId)).ToArray(),
                    requestedEndDate))
            {
                return new OrganizationSurveyEndDateUpdateResult
                {
                    Success = false,
                    Message = "Не удалось обновить дату конца анкет.",
                    Errors = ["Не удалось обновить одну или несколько связок организации и анкеты."]
                };
            }
        }
        catch (Exception ex)
        {
            return new OrganizationSurveyEndDateUpdateResult
            {
                Success = false,
                Message = "Не удалось обновить дату конца анкет.",
                Error = ex.Message
            };
        }

        return new OrganizationSurveyEndDateUpdateResult
        {
            Success = true,
            Message = "Дата конца для выбранных анкет обновлена.",
            UpdatedAssignments = assignments
                .Select(assignment =>
                    BuildOrganizationSurveyAssignmentUpdateItem(
                        assignmentLookup[(assignment.OrganizationId, assignment.SurveyId)],
                        requestedEndDate))
                .ToList()
        };
    }

    private static OrganizationSurveyItemViewModel MapOrganizationSurveyItem(OrganizationSurveyAssignmentRecord row)
    {
        var effectiveEndDate = row.AssignmentDateEnd!.Value.Date;

        return new OrganizationSurveyItemViewModel
        {
            SurveyId = row.SurveyId!.Value,
            SurveyName = row.SurveyName ?? string.Empty,
            BaseEndDateIso = row.AssignmentDateEnd.Value.ToString("yyyy-MM-dd"),
            EffectiveEndDateDisplay = effectiveEndDate.ToString("dd.MM.yyyy"),
            EffectiveEndDateIso = effectiveEndDate.ToString("yyyy-MM-dd"),
            RemainingText = FormatRemainingText(effectiveEndDate),
            IsExpired = effectiveEndDate.Date < DateTime.Today
        };
    }

    private static List<Organization> SortOrganizations(
        IEnumerable<Organization> organizations,
        string? sortBy,
        string? sortDirection)
    {
        var normalizedSortBy = NormalizeOrganizationSortField(sortBy);
        var normalizedSortDirection = NormalizeOrganizationSortDirection(sortDirection, normalizedSortBy);
        var descending = string.Equals(normalizedSortDirection, "desc", StringComparison.Ordinal);

        IOrderedEnumerable<Organization> orderedOrganizations = normalizedSortBy switch
        {
            OrganizationListSortFields.DateBegin => descending
                ? organizations.OrderByDescending(organization => organization.DateBegin ?? DateTime.MinValue)
                : organizations.OrderBy(organization => organization.DateBegin ?? DateTime.MaxValue),
            OrganizationListSortFields.DateEnd => descending
                ? organizations.OrderByDescending(organization => organization.DateEnd ?? DateTime.MinValue)
                : organizations.OrderBy(organization => organization.DateEnd ?? DateTime.MaxValue),
            _ => descending
                ? organizations.OrderByDescending(organization => organization.OrganizationName, AppListPaging.RuStringComparer)
                : organizations.OrderBy(organization => organization.OrganizationName, AppListPaging.RuStringComparer)
        };

        return orderedOrganizations
            .ThenBy(organization => organization.OrganizationId)
            .ToList();
    }

    private static string NormalizeOrganizationSortField(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            OrganizationListSortFields.DateBegin => OrganizationListSortFields.DateBegin,
            OrganizationListSortFields.DateEnd => OrganizationListSortFields.DateEnd,
            _ => OrganizationListSortFields.Name
        };
    }

    private static string NormalizeOrganizationSortDirection(string? sortDirection, string sortField)
    {
        if (string.Equals(sortDirection?.Trim(), "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        if (string.Equals(sortDirection?.Trim(), "desc", StringComparison.OrdinalIgnoreCase))
        {
            return "desc";
        }

        return sortField switch
        {
            OrganizationListSortFields.DateBegin => "desc",
            OrganizationListSortFields.DateEnd => "desc",
            _ => "asc"
        };
    }

    private static OrganizationSurveyAssignmentUpdateItem BuildOrganizationSurveyAssignmentUpdateItem(
        OrganizationSurveyAssignmentRecord row,
        DateTime requestedEndDate)
    {
        var effectiveEndDate = requestedEndDate.Date;

        return new OrganizationSurveyAssignmentUpdateItem
        {
            OrganizationId = row.OrganizationId,
            SurveyId = row.SurveyId!.Value,
            EffectiveEndDateDisplay = effectiveEndDate.ToString("dd.MM.yyyy"),
            EffectiveEndDateIso = effectiveEndDate.ToString("yyyy-MM-dd"),
            RemainingText = FormatRemainingText(effectiveEndDate),
            IsExpired = effectiveEndDate.Date < DateTime.Today
        };
    }

    private static string FormatRemainingText(DateTime effectiveEndDate)
    {
        var daysRemaining = (effectiveEndDate.Date - DateTime.Today).Days;

        return daysRemaining switch
        {
            > 0 => $"Осталось {daysRemaining} дн.",
            0 => "Завершается сегодня",
            _ => $"Срок истёк {Math.Abs(daysRemaining)} дн. назад"
        };
    }

    private static IReadOnlyList<string> ValidateOrganizationSurveyEndDateUpdateRequest(
        OrganizationSurveyEndDateUpdateRequest request)
    {
        var errors = new List<string>();

        if (!DateTime.TryParse(request.DateEnd, out var requestedEndDate) || requestedEndDate.Date <= DateTime.Today)
        {
            errors.Add("Укажите корректную дату конца позже текущего дня.");
        }

        if (request.Assignments.Count == 0)
        {
            errors.Add("Выберите хотя бы одну анкету организации.");
        }

        foreach (var assignment in request.Assignments)
        {
            if (assignment.OrganizationId <= 0)
            {
                errors.Add("Некорректный идентификатор организации.");
            }

            if (assignment.SurveyId <= 0)
            {
                errors.Add("Некорректный идентификатор анкеты.");
            }
        }

        return errors;
    }

    private static string BuildArchiveRestrictedMessage(
        IReadOnlyList<string> surveyNames,
        IReadOnlyList<string> userNames)
    {
        var builder = new StringBuilder();

        builder.Append((surveyNames.Count, userNames.Count) switch
        {
            (> 0, > 0) => "Нельзя удалить организацию: для неё уже заводились анкеты и выбирались пользователи.",
            (> 0, 0) => "Нельзя удалить организацию: для неё уже заводились анкеты.",
            (0, > 0) => "Нельзя удалить организацию: для неё уже выбирались пользователи.",
            _ =>
                "Нельзя удалить организацию."
        });

        if (surveyNames.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Анкеты: ");
            builder.Append(string.Join(", ", surveyNames));
            builder.Append('.');
        }

        if (userNames.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Пользователи: ");
            builder.Append(string.Join(", ", userNames));
            builder.Append('.');
        }

        return builder.ToString();
    }

    private static bool TryValidateOrganizationRequest(
        OrganizationSaveRequest request,
        out DateTime? dateBegin,
        out DateTime? dateEnd,
        out string validationError)
    {
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Название организации обязательно для заполнения.";
            return false;
        }

        if (!TryParseOptionalDate(request.DateBegin, out dateBegin, out validationError))
        {
            dateEnd = null;
            return false;
        }

        if (!TryParseOptionalDate(request.DateEnd, out dateEnd, out validationError))
        {
            return false;
        }

        if (dateBegin.HasValue && dateEnd.HasValue && dateEnd.Value < dateBegin.Value)
        {
            validationError = "Дата конца не может быть раньше даты начала.";
            return false;
        }

        return true;
    }

    private static OrganizationWriteModel ToWriteModel(
        OrganizationSaveRequest request,
        DateTime? dateBegin,
        DateTime? dateEnd)
    {
        return new OrganizationWriteModel(
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            dateBegin,
            dateEnd);
    }

    private static bool TryParseOptionalDate(string? rawValue, out DateTime? parsedValue, out string validationError)
    {
        parsedValue = null;
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        if (DateTime.TryParse(rawValue, out var date))
        {
            parsedValue = date;
            return true;
        }

        validationError = "Некорректный формат даты.";
        return false;
    }

}
