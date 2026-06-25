using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Surveys;

public sealed class SurveyAdminService : ISurveyAdminService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISurveyAssignmentRepository _assignmentRepository;
    private readonly ISurveyDefinitionRepository _definitionRepository;
    private readonly IClock _clock;

    public SurveyAdminService(
        IDbConnectionFactory connectionFactory,
        ISurveyAssignmentRepository assignmentRepository,
        ISurveyDefinitionRepository definitionRepository,
        IClock clock)
    {
        _connectionFactory = connectionFactory;
        _assignmentRepository = assignmentRepository;
        _definitionRepository = definitionRepository;
        _clock = clock;
    }

    public async Task<SurveyListPageViewModel> GetSurveysPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var selectedOrganizationIds = ParseSelectedIds(organizationIds);
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeSurveySortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeSurveySortDirection(null, normalizedSortBy);

        var organizationOptions = BuildSelectionOptions(
            await _assignmentRepository.GetActiveOrganizationOptionsAsync(connection, cancellationToken));
        var totalCount = await _assignmentRepository.CountActiveSurveysAsync(connection, selectedOrganizationIds, cancellationToken);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var pageRows = await _assignmentRepository.GetActiveSurveyPageAsync(
            connection,
            selectedOrganizationIds,
            normalizedSortBy,
            normalizedSortDirection,
            pageWindow.PageSize,
            pageWindow.Offset,
            cancellationToken);

        return new SurveyListPageViewModel
        {
            SurveyRows = pageRows.Select(MapSurveyTablePageRow).ToList(),
            CurrentPage = pageWindow.CurrentPage,
            TotalPages = pageWindow.TotalPages,
            TotalCount = pageWindow.TotalCount,
            PageSize = pageWindow.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            FilterState = new ServerTableFilterStateViewModel
            {
                BasePath = "/surveys",
                EnableOrganizationFilter = true,
                OrganizationOptions = organizationOptions,
                SelectedOrganizationIds = selectedOrganizationIds
            }
        };
    }

    public async Task<IReadOnlyList<Survey>> GetSurveysAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var surveys = (await _assignmentRepository.GetActiveSurveySummariesAsync(connection, cancellationToken)).ToList();
        await _definitionRepository.AttachQuestionsAsync(connection, surveys, cancellationToken);
        return surveys;
    }

    public async Task<SurveyCommandResult> CreateSurveyAsync(SurveyAddRequest? request, CancellationToken cancellationToken = default)
    {
        if (!TryValidateCreateRequest(
                request,
                out var title,
                out var description,
                out var startDate,
                out var endDate,
                out var organizationIds,
                out var questionRows,
                out var validationError))
        {
            return new SurveyCommandResult
            {
                Message = validationError
            };
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var newSurveyId = await _definitionRepository.CreateAsync(
                connection, transaction, title, description, cancellationToken);

            await _definitionRepository.ReplaceQuestionsAsync(
                connection,
                transaction,
                newSurveyId,
                questionRows.Select(question => new SurveyQuestionItem
                {
                    Id = question.QuestionOrder,
                    Text = question.QuestionText
                }).ToArray(), cancellationToken);
            await _assignmentRepository.UpsertSurveyAssignmentsAsync(
                connection,
                transaction,
                newSurveyId,
                organizationIds,
                startDate,
                endDate,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SurveyCommandResult
            {
                Success = true,
                Message = "Анкета успешно создана",
                SurveyId = newSurveyId
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SurveyEditPageViewModel?> GetSurveyEditPageAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var survey = await _assignmentRepository.GetSurveyWithScheduleAsync(connection, id, cancellationToken);

        if (survey == null)
        {
            return null;
        }

        await _definitionRepository.AttachQuestionsAsync(connection, new[] { survey }, cancellationToken);

        var allOrganization = await _assignmentRepository.GetAvailableOrganizationsForSurveyAsync(connection, id, cancellationToken);
        var selectedOrganization = await _assignmentRepository.GetSelectedOrganizationsForSurveyAsync(connection, id, cancellationToken);

        return new SurveyEditPageViewModel
        {
            Survey = survey,
            AllOrganization = allOrganization,
            SelectedOrganizationIds = selectedOrganization.Select(o => o.Id).ToList(),
            SelectedOrganizationNames = selectedOrganization.Select(o => o.Name).ToList(),
            Criteria = await _definitionRepository.GetCriteriaAsync(connection, id, cancellationToken)
        };
    }

    public async Task<SurveyCommandResult> UpdateSurveyAsync(
        int id,
        SurveyUpdateRequest? model,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateUpdateRequest(
                model,
                out var title,
                out var description,
                out var startDate,
                out var endDate,
                out var organizationIds,
                out var questionRows,
                out var validationError))
        {
            return new SurveyCommandResult
            {
                Message = validationError
            };
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var affectedRows = await _definitionRepository.UpdateAsync(
                connection,
                transaction,
                id,
                title,
                description,
                cancellationToken);

            if (affectedRows == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    NotFound = true,
                    Message = "Анкета не найдена"
                };
            }

            await _definitionRepository.ReplaceQuestionsAsync(
                connection,
                transaction,
                id,
                questionRows.Select(question => new SurveyQuestionItem
                {
                    Id = question.QuestionOrder,
                    Text = question.QuestionText
                }).ToArray(),
                cancellationToken);
            await _assignmentRepository.ReplaceSurveyAssignmentsAsync(
                connection,
                transaction,
                id,
                organizationIds,
                startDate,
                endDate,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SurveyCommandResult
            {
                Success = true,
                Message = "Анкета успешно обновлена",
                SurveyId = id
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SurveyCommandResult> UpdateActiveSurveysWorkPeriodAsync(
        SurveyWorkPeriodRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return new SurveyCommandResult
            {
                Message = "Неверные данные запроса"
            };
        }

        if (!TryValidateDateRange(request.DateBegin, request.DateEnd, out var validationError))
        {
            return new SurveyCommandResult
            {
                Message = validationError
            };
        }

        if (request.DateEnd.Date < _clock.Today.Date)
        {
            return new SurveyCommandResult
            {
                Message = "Дата конца не может быть раньше сегодняшней даты"
            };
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var affectedSurveyCount = await _assignmentRepository.UpdateActiveSurveyPeriodAsync(
                connection,
                transaction,
                request.DateBegin,
                request.DateEnd,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new SurveyCommandResult
            {
                Success = true,
                Message = affectedSurveyCount == 0
                    ? "Активные анкеты не найдены"
                    : "Период работы активных анкет сохранён"
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Survey?> GetSurveyForCopyAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var survey = await _assignmentRepository.GetSurveyWithScheduleAsync(connection, id, cancellationToken);

        if (survey != null)
        {
            await _definitionRepository.AttachQuestionsAsync(connection, new[] { survey }, cancellationToken);
        }

        return survey;
    }

    public async Task<SurveyCommandResult> CopySurveyAsync(
        int id,
        SurveyCopyRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateCopyRequest(request, out var startDate, out var endDate, out var validationError))
        {
            return new SurveyCommandResult
            {
                Message = validationError
            };
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var originalSurvey = await _definitionRepository.GetByIdAsync(connection, transaction, id, cancellationToken);

            if (originalSurvey == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    NotFound = true,
                    Message = "Анкета не найдена"
                };
            }

            var newSurveyId = await _definitionRepository.CreateAsync(
                connection,
                transaction,
                $"{originalSurvey.NameSurvey} (Копия)",
                originalSurvey.Description,
                cancellationToken);

            await _definitionRepository.CopyQuestionsAsync(connection, transaction, id, newSurveyId, cancellationToken);

            var organizationIds = await _assignmentRepository.GetOrganizationIdsAsync(
                connection,
                transaction,
                id,
                cancellationToken);

            await _assignmentRepository.UpsertSurveyAssignmentsAsync(
                connection,
                transaction,
                newSurveyId,
                organizationIds,
                startDate,
                endDate,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new SurveyCommandResult
            {
                Success = true,
                Message = "Анкета успешно скопирована",
                SurveyId = newSurveyId
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static bool TryValidateCreateRequest(
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
            validationError = "Неверные данные запроса";
            return false;
        }

        title = request.Title?.Trim() ?? string.Empty;
        description = request.Description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            validationError = "Название анкеты обязательно";
            return false;
        }

        if (!TryParseDateRange(request.StartDate, request.EndDate, out startDate, out endDate, out validationError))
        {
            return false;
        }

        if (!TryNormalizeOrganizationIds(request.Organizations, out organizationIds, out validationError))
        {
            return false;
        }

        return TryBuildQuestionRows(request.Criteria, out questionRows, out validationError);
    }

    private static bool TryValidateUpdateRequest(
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
            validationError = "Данные анкеты не предоставлены";
            return false;
        }

        title = request.Title?.Trim() ?? string.Empty;
        description = request.Description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            validationError = "Название анкеты обязательно";
            return false;
        }

        if (!TryValidateDateRange(request.StartDate, request.EndDate, out validationError))
        {
            return false;
        }

        startDate = request.StartDate;
        endDate = request.EndDate;

        if (!TryNormalizeOrganizationIds(request.Organizations, out organizationIds, out validationError))
        {
            return false;
        }

        return TryBuildQuestionRows(request.Criteria, out questionRows, out validationError);
    }

    private static bool TryValidateCopyRequest(
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
            validationError = "Неверные данные запроса";
            return false;
        }

        return TryParseDateRange(request.StartDate, request.EndDate, out startDate, out endDate, out validationError);
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

        if (!DateTime.TryParse(rawStartDate, out startDate)
            || !DateTime.TryParse(rawEndDate, out endDate))
        {
            validationError = "Неверный формат даты";
            return false;
        }

        return TryValidateDateRange(startDate, endDate, out validationError);
    }

    private static bool TryValidateDateRange(DateTime startDate, DateTime endDate, out string validationError)
    {
        validationError = string.Empty;

        if (startDate == default || endDate == default)
        {
            validationError = "Неверный формат даты";
            return false;
        }

        if (endDate <= startDate)
        {
            validationError = "Дата конца должна быть позже даты начала";
            return false;
        }

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
            validationError = "Выберите хотя бы одну организацию";
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
        questionRows = (rawCriteria ?? Array.Empty<string>())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select((text, index) => new SurveyQuestionRow
            {
                QuestionOrder = index + 1,
                QuestionText = text.Trim()
            })
            .ToList();

        if (questionRows.Count == 0)
        {
            validationError = "Добавьте хотя бы один критерий";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    public async Task<IReadOnlyList<Survey>?> DeleteSurveyAsync(int surveyId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await _definitionRepository.DeleteAsync(connection, transaction, surveyId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            await transaction.CommitAsync(cancellationToken);
            return await GetSurveysAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static SurveyTableRowViewModel MapSurveyTablePageRow(SurveyAssignmentTableRow row)
    {
        return new SurveyTableRowViewModel
        {
            IdSurvey = row.IdSurvey,
            NameSurvey = row.NameSurvey ?? string.Empty,
            DateBegin = row.DateBegin,
            DateEnd = row.DateEnd,
            OrganizationIds = row.OrganizationIds ?? Array.Empty<int>(),
            OrganizationNames = row.OrganizationNames ?? Array.Empty<string>()
        };
    }

    private static IReadOnlyList<int> ParseSelectedIds(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? Array.Empty<int>()
            : rawValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
    }

    private static IReadOnlyList<SelectionOption> BuildSelectionOptions(IEnumerable<SelectionOption> options)
    {
        return options
            .Where(option => option.Id > 0 && !string.IsNullOrWhiteSpace(option.Name))
            .GroupBy(option => option.Id)
            .Select(group => group.First())
            .OrderBy(option => option.Name, AppListPaging.RuStringComparer)
            .ThenBy(option => option.Id)
            .ToList();
    }

    private static string NormalizeSurveySortField(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            SurveyListSortFields.Name => SurveyListSortFields.Name,
            SurveyListSortFields.DateBegin => SurveyListSortFields.DateBegin,
            SurveyListSortFields.DateEnd => SurveyListSortFields.DateEnd,
            _ => SurveyListSortFields.Default
        };
    }

    private static string NormalizeSurveySortDirection(string? sortDirection, string sortField)
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
            SurveyListSortFields.Name => "asc",
            _ => "desc"
        };
    }

}
