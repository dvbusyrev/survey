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
    private readonly IClock _clock;

    public OrganizationManagementService(IOrganizationRepository organizationRepository, IClock clock)
    {
        _organizationRepository = organizationRepository;
        _clock = clock;
    }

    public Task<OrganizationListPageViewModel> GetActiveOrganizationsPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool openAddOrganizationModal = false,
        CancellationToken cancellationToken = default)
    {
        return GetOrganizationsPageAsync(currentPage, sortBy, sortDirection, includeArchived: false, openAddOrganizationModal, cancellationToken);
    }

    public async Task<OrganizationSurveyAssignmentsPageViewModel> GetOrganizationSurveyAssignmentsPageAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _organizationRepository.GetLatestUnansweredAssignmentsAsync(cancellationToken: cancellationToken);

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

    public Task<OrganizationListPageViewModel> GetArchivedOrganizationsPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default)
    {
        return GetOrganizationsPageAsync(currentPage, sortBy, sortDirection, includeArchived: true, cancellationToken: cancellationToken);
    }

    private async Task<OrganizationListPageViewModel> GetOrganizationsPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool includeArchived,
        bool openAddOrganizationModal = false,
        CancellationToken cancellationToken = default)
    {
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeOrganizationSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeOrganizationSortDirection(null, normalizedSortBy);

        var totalCount = await _organizationRepository.CountAsync(includeArchived, cancellationToken);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var organizations = await _organizationRepository.GetPageAsync(
            includeArchived,
            normalizedSortBy,
            normalizedSortDirection,
            pageWindow.PageSize,
            pageWindow.Offset,
            cancellationToken);

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

    public Task<IReadOnlyList<Organization>> GetArchivedOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        return _organizationRepository.GetAllAsync(includeArchived: true, cancellationToken);
    }

    public Task<IReadOnlyList<OrganizationDataResponse>> GetOrganizationOptionsAsync(CancellationToken cancellationToken = default)
    {
        return _organizationRepository.GetActiveOptionsAsync(cancellationToken);
    }

    public Task<Organization?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _organizationRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<OperationResult> CreateOrganizationAsync(OrganizationSaveRequest request, CancellationToken cancellationToken = default)
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

        var organizationId = await _organizationRepository.CreateAsync(ToWriteModel(request, dateBegin, dateEnd), cancellationToken);

        return new OperationResult
        {
            Success = true,
            Message = "Организация успешно создана",
            EntityId = organizationId,
            ShouldReload = true
        };
    }

    public async Task<OperationResult> UpdateOrganizationAsync(int id, OrganizationSaveRequest request, CancellationToken cancellationToken = default)
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

        var affectedRows = await _organizationRepository.UpdateAsync(id, ToWriteModel(request, dateBegin, dateEnd), cancellationToken);

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Организация успешно обновлена"
                : "Организация не найдена."
        };
    }

    public async Task<OperationResult> ArchiveOrganizationAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _organizationRepository.ArchiveIfUnusedAsync(id, cancellationToken);
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

    public async Task<OrganizationSurveyEndDateUpdateResult> UpdateOrganizationSurveyEndDatesAsync(
        OrganizationSurveyEndDateUpdateRequest request,
        CancellationToken cancellationToken = default)
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
        var assignmentRows = await _organizationRepository.GetLatestUnansweredAssignmentsAsync(organizationIds, cancellationToken);
        var assignmentLookup = assignmentRows
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
            if (!await _organizationRepository.UpdateAssignmentEndDatesAsync(
                    assignments.Select(item => (item.OrganizationId, item.SurveyId)).ToArray(),
                    requestedEndDate,
                    cancellationToken))
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

    private OrganizationSurveyItemViewModel MapOrganizationSurveyItem(OrganizationSurveyAssignmentRecord row)
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
            IsExpired = effectiveEndDate.Date < _clock.Today.Date
        };
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

    private OrganizationSurveyAssignmentUpdateItem BuildOrganizationSurveyAssignmentUpdateItem(
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
            IsExpired = effectiveEndDate.Date < _clock.Today.Date
        };
    }

    private string FormatRemainingText(DateTime effectiveEndDate)
    {
        var daysRemaining = (effectiveEndDate.Date - _clock.Today.Date).Days;

        return daysRemaining switch
        {
            > 0 => $"Осталось {daysRemaining} дн.",
            0 => "Завершается сегодня",
            _ => $"Срок истёк {Math.Abs(daysRemaining)} дн. назад"
        };
    }

    private IReadOnlyList<string> ValidateOrganizationSurveyEndDateUpdateRequest(
        OrganizationSurveyEndDateUpdateRequest request)
    {
        var errors = new List<string>();

        if (!DateTime.TryParse(request.DateEnd, out var requestedEndDate) || requestedEndDate.Date <= _clock.Today.Date)
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
