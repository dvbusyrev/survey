using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.External.Calendar;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MainProject.Application.UseCases.Surveys;

public partial class SurveyService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly SurveyRepository _surveyRepository;
    private readonly AnswerRepository _answerRepository;
    private readonly IClock _clock;
    private readonly ProductionCalendarService? _productionCalendar;
    private readonly ILogger<SurveyService> _logger;

    protected SurveyService()
    {
        _connectionFactory = null!;
        _surveyRepository = null!;
        _answerRepository = null!;
        _clock = null!;
        _logger = null!;
    }

    public SurveyService(
        IDbConnectionFactory connectionFactory,
        SurveyRepository surveyRepository,
        IClock clock,
        AnswerRepository? answerRepository = null,
        ILogger<SurveyService>? logger = null,
        ProductionCalendarService? productionCalendar = null)
    {
        _connectionFactory = connectionFactory;
        _surveyRepository = surveyRepository;
        _answerRepository = answerRepository!;
        _clock = clock;
        _productionCalendar = productionCalendar;
        _logger = logger ?? NullLogger<SurveyService>.Instance;
    }

    public async Task<SurveyListPageViewModel> GetSurveysPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        CancellationToken cancellationToken = default) =>
        await GetAdminSurveyPageAsync(
            currentPage,
            sortBy,
            sortDirection,
            organizationIds,
            false,
            cancellationToken);

    public async Task<SurveyListPageViewModel> GetSurveyTemplatesPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        CancellationToken cancellationToken = default) =>
        await GetAdminSurveyPageAsync(
            currentPage,
            sortBy,
            sortDirection,
            organizationIds,
            true,
            cancellationToken);

    public async Task<SurveyListPageViewModel> GetPlannedSurveyTemplatesPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        CancellationToken cancellationToken = default)
    {
        await PromotePlannedSurveyTemplatesAsync(cancellationToken);
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var selectedOrganizationIds = ParseSelectedIds(organizationIds);
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeSurveySortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeSurveySortDirection(null, normalizedSortBy);
        var organizationOptions = BuildSelectionOptions(
            await _surveyRepository.GetPlannedSurveyTemplateOrganizationOptionsAsync(connection, cancellationToken));
        var totalCount = await _surveyRepository.CountPlannedSurveyTemplatesAsync(
            connection,
            selectedOrganizationIds,
            cancellationToken);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var rows = await _surveyRepository.GetPlannedSurveyTemplatePageAsync(
            connection,
            selectedOrganizationIds,
            normalizedSortBy,
            normalizedSortDirection,
            pageWindow.PageSize,
            pageWindow.Offset,
            cancellationToken);

        return new SurveyListPageViewModel
        {
            SurveyRows = rows.Select(MapSurveyTablePageRow).ToList(),
            IsTemplateSection = true,
            IsPlannedTemplateSection = true,
            CurrentPage = pageWindow.CurrentPage,
            TotalPages = pageWindow.TotalPages,
            TotalCount = pageWindow.TotalCount,
            PageSize = pageWindow.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            FilterState = new ServerTableFilterStateViewModel
            {
                BasePath = "/survey-templates/planned",
                EnableOrganizationFilter = true,
                OrganizationOptions = organizationOptions,
                SelectedOrganizationIds = selectedOrganizationIds
            }
        };
    }

    private async Task<SurveyListPageViewModel> GetAdminSurveyPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        bool isTemplate,
        CancellationToken cancellationToken)
    {
        if (isTemplate)
        {
            await PromotePlannedSurveyTemplatesAsync(cancellationToken);
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var selectedOrganizationIds = ParseSelectedIds(organizationIds);
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeSurveySortField(hasExplicitSort ? sortBy : null);
        if (!isTemplate && normalizedSortBy == SurveyListSortFields.AutoCreation)
        {
            normalizedSortBy = SurveyListSortFields.Default;
            hasExplicitSort = false;
        }
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeSurveySortDirection(null, normalizedSortBy);

        var organizationOptions = BuildSelectionOptions(
            await _surveyRepository.GetActiveOrganizationOptionsAsync(connection, isTemplate, cancellationToken));
        var totalCount = await _surveyRepository.CountActiveSurveysAsync(
            connection,
            selectedOrganizationIds,
            isTemplate,
            cancellationToken);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var pageRows = await _surveyRepository.GetActiveSurveyPageAsync(
            connection,
            selectedOrganizationIds,
            normalizedSortBy,
            normalizedSortDirection,
            pageWindow.PageSize,
            pageWindow.Offset,
            isTemplate,
            cancellationToken);

        return new SurveyListPageViewModel
        {
            SurveyRows = pageRows.Select(MapSurveyTablePageRow).ToList(),
            IsTemplateSection = isTemplate,
            CurrentPage = pageWindow.CurrentPage,
            TotalPages = pageWindow.TotalPages,
            TotalCount = pageWindow.TotalCount,
            PageSize = pageWindow.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            FilterState = new ServerTableFilterStateViewModel
            {
                BasePath = isTemplate ? "/survey-templates" : "/surveys",
                EnableOrganizationFilter = true,
                OrganizationOptions = organizationOptions,
                SelectedOrganizationIds = selectedOrganizationIds
            }
        };
    }

    public async Task<IReadOnlyList<Survey>> GetSurveysAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var surveys = (await _surveyRepository.GetActiveSurveySummariesAsync(connection, cancellationToken)).ToList();
        await _surveyRepository.AttachQuestionsAsync(connection, surveys, cancellationToken);
        return surveys;
    }

    public async Task<IReadOnlyList<SelectionOption>> GetActiveSurveyTemplateOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        await PromotePlannedSurveyTemplatesAsync(cancellationToken);
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return BuildSelectionOptions(
            await _surveyRepository.GetActiveSurveyTemplateOptionsAsync(connection, cancellationToken));
    }

    public async Task<SurveyCommandResult> CreateSurveyAsync(SurveyAddRequest? request, CancellationToken cancellationToken = default)
    {
        return await CreateSurveyAsync(request, false, false, cancellationToken);
    }

    public async Task<SurveyCommandResult> CreateSurveyTemplateAsync(
        SurveyAddRequest? request,
        CancellationToken cancellationToken = default)
    {
        return await CreateSurveyAsync(request, true, false, cancellationToken);
    }

    public async Task<SurveyCommandResult> CreatePlannedSurveyTemplateAsync(
        SurveyAddRequest? request,
        CancellationToken cancellationToken = default) =>
        await CreateSurveyAsync(request, true, true, cancellationToken);

    private async Task<SurveyCommandResult> CreateSurveyAsync(
        SurveyAddRequest? request,
        bool isTemplate,
        bool isPlannedTemplate,
        CancellationToken cancellationToken)
    {
        if (!TryValidateCreateRequest(
                request,
                isTemplate,
                isPlannedTemplate,
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
            var ancestorId = isPlannedTemplate ? request!.AncestorId : null;
            if (ancestorId.HasValue
                && !await _surveyRepository.IsActiveSurveyTemplateAsync(
                    connection,
                    transaction,
                    ancestorId.Value,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    Message = "Выбранный шаблон-родитель уже не активен. Выберите другой шаблон."
                };
            }

            var newSurveyId = isTemplate
                ? await _surveyRepository.CreateSurveyTemplateAsync(
                    connection,
                    transaction,
                    title,
                    description,
                    startDate,
                    endDate,
                    ancestorId,
                    cancellationToken)
                : await _surveyRepository.CreateSurveyAsync(
                    connection,
                    transaction,
                    title,
                    description,
                    startDate,
                    endDate!.Value,
                    cancellationToken);

            var questions = questionRows.Select(question => new SurveyQuestionItem
            {
                Id = question.QuestionOrder,
                Text = question.QuestionText
            }).ToArray();

            if (isTemplate)
            {
                await _surveyRepository.ReplaceSurveyTemplateQuestionsAsync(
                    connection,
                    transaction,
                    newSurveyId,
                    questions,
                    cancellationToken);
                await _surveyRepository.UpsertSurveyTemplateAssignmentsAsync(
                    connection,
                    transaction,
                    newSurveyId,
                    organizationIds,
                    cancellationToken);
                await GetOrCreateConfigurationAsync(
                    connection,
                    transaction,
                    cancellationToken,
                    lockRow: false);
                await _surveyRepository.SetSurveyTemplateAutoCreationAsync(
                    connection,
                    transaction,
                    SingletonConfigId,
                    newSurveyId,
                    request!.IsAutoCreationEnabled,
                    cancellationToken);
            }
            else
            {
                await _surveyRepository.ReplaceSurveyQuestionsAsync(
                    connection,
                    transaction,
                    newSurveyId,
                    questions,
                    cancellationToken);
                await _surveyRepository.UpsertSurveyAssignmentsAsync(
                    connection,
                    transaction,
                    newSurveyId,
                    organizationIds,
                    startDate,
                    endDate!.Value,
                    cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);

            var createdByAutoCreation = isTemplate && !isPlannedTemplate && request!.IsAutoCreationEnabled
                ? await TryRunPendingAfterTemplateSelectionAsync(cancellationToken)
                : 0;

            var successMessage = isTemplate
                ? isPlannedTemplate ? "Плановый шаблон успешно создан." : "Шаблон успешно создан."
                : "Анкета успешно создана.";
            if (isTemplate && request!.IsAutoCreationEnabled)
            {
                successMessage += " Шаблон успешно добавлен в автосоздание.";
            }
            if (createdByAutoCreation > 0)
            {
                successMessage += $" Создано анкет: {createdByAutoCreation}.";
            }

            return new SurveyCommandResult
            {
                Success = true,
                Message = successMessage,
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

        var survey = await _surveyRepository.GetSurveyWithScheduleAsync(connection, id, cancellationToken);

        if (survey == null)
        {
            return null;
        }

        await _surveyRepository.AttachQuestionsAsync(connection, new[] { survey }, cancellationToken);

        var allOrganization = await _surveyRepository.GetAvailableOrganizationsForSurveyAsync(connection, id, cancellationToken);
        var selectedOrganization = await _surveyRepository.GetSelectedOrganizationsForSurveyAsync(connection, id, cancellationToken);
        var answeredOrganizationIds = await _surveyRepository.GetAnsweredOrganizationIdsForSurveyAsync(
            connection,
            id,
            cancellationToken: cancellationToken);

        return new SurveyEditPageViewModel
        {
            Survey = survey,
            AllOrganization = allOrganization,
            SelectedOrganizationIds = selectedOrganization.Select(o => o.Id).ToList(),
            SelectedOrganizationNames = selectedOrganization.Select(o => o.Name).ToList(),
            AnsweredOrganizationIds = answeredOrganizationIds,
            Criteria = await _surveyRepository.GetSurveyCriteriaAsync(connection, id, cancellationToken),
            HasAnswers = answeredOrganizationIds.Count > 0
        };
    }

    public async Task<SurveyEditPageViewModel?> GetSurveyTemplateEditPageAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var template = await _surveyRepository.GetSurveyTemplateWithScheduleAsync(connection, id, cancellationToken);
        if (template == null)
        {
            return null;
        }

        var organizations = await _surveyRepository.GetAvailableOrganizationsForSurveyTemplateAsync(
            connection,
            id,
            cancellationToken);
        var selectedOrganizations = await _surveyRepository.GetSelectedOrganizationsForSurveyTemplateAsync(
            connection,
            id,
            cancellationToken);
        var ancestorName = template.AncestorId.HasValue
            ? await _surveyRepository.GetSurveyTemplateNameAsync(connection, template.AncestorId.Value, cancellationToken)
            : null;

        return new SurveyEditPageViewModel
        {
            Survey = template,
            AllOrganization = organizations,
            SelectedOrganizationIds = selectedOrganizations.Select(item => item.Id).ToList(),
            SelectedOrganizationNames = selectedOrganizations.Select(item => item.Name).ToList(),
            Criteria = await _surveyRepository.GetSurveyTemplateCriteriaAsync(connection, id, cancellationToken),
            HasAnswers = false,
            AncestorId = template.AncestorId,
            AncestorName = ancestorName,
            IsAutoCreationEnabled = await _surveyRepository.IsSurveyTemplateSelectedForAutoCreationAsync(
                connection,
                null,
                id,
                cancellationToken)
        };
    }

    public async Task<SurveyEditPageViewModel?> GetPlannedSurveyTemplateEditPageAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await PromotePlannedSurveyTemplatesAsync(cancellationToken);
        var editPage = await GetSurveyTemplateEditPageAsync(id, cancellationToken);
        return editPage?.Survey.DateBegin.Date > _clock.Today.Date ? editPage : null;
    }

    public async Task<IReadOnlyList<OrganizationSelectionItem>> GetAssignedOrganizationsForExtensionAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
        {
            return Array.Empty<OrganizationSelectionItem>();
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _surveyRepository.GetUnansweredOrganizationsForSurveyExtensionAsync(
            connection,
            surveyId,
            cancellationToken);
    }

    public async Task<SurveyCommandResult> UpdateSurveyAsync(
        int id,
        SurveyUpdateRequest? model,
        CancellationToken cancellationToken = default) =>
        await UpdateSurveyAsync(id, model, false, false, cancellationToken);

    public async Task<SurveyCommandResult> UpdateSurveyTemplateAsync(
        int id,
        SurveyUpdateRequest? model,
        CancellationToken cancellationToken = default)
    {
        await PromotePlannedSurveyTemplatesAsync(cancellationToken);
        return await UpdateSurveyAsync(id, model, true, false, cancellationToken);
    }

    public async Task<SurveyCommandResult> UpdatePlannedSurveyTemplateAsync(
        int id,
        SurveyUpdateRequest? model,
        CancellationToken cancellationToken = default) =>
        await UpdateSurveyAsync(id, model, true, true, cancellationToken);

    private async Task<SurveyCommandResult> UpdateSurveyAsync(
        int id,
        SurveyUpdateRequest? model,
        bool isTemplate,
        bool isPlannedTemplate,
        CancellationToken cancellationToken)
    {
        if (!TryValidateUpdateRequest(
                model,
                isTemplate,
                isPlannedTemplate,
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
            var currentSurvey = isTemplate
                ? await _surveyRepository.GetSurveyTemplateByIdAsync(
                    connection,
                    transaction,
                    id,
                    cancellationToken)
                : await _surveyRepository.GetSurveyByIdAsync(
                    connection,
                    transaction,
                    id,
                    cancellationToken);
            if (currentSurvey == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    NotFound = true,
                    Message = isTemplate ? "Шаблон не найден." : "Анкета не найдена."
                };
            }

            if (isPlannedTemplate && currentSurvey.DateBegin.Date <= _clock.Today.Date)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    Message = "Плановый шаблон уже начал действовать и перенесён в активные шаблоны."
                };
            }

            var ancestorId = isPlannedTemplate ? model!.AncestorId : null;
            if (ancestorId == id)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    Message = "Шаблон не может быть родителем для самого себя."
                };
            }
            if (ancestorId.HasValue
                && !await _surveyRepository.IsActiveSurveyTemplateAsync(
                    connection,
                    transaction,
                    ancestorId.Value,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    Message = "Выбранный шаблон-родитель уже не активен. Выберите другой шаблон."
                };
            }

            var wasTemplateSelectedForAutoCreation = isTemplate
                && await _surveyRepository.IsSurveyTemplateSelectedForAutoCreationAsync(
                    connection,
                    transaction,
                    id,
                    cancellationToken);
            var isTemplateSelectedForAutoCreation = false;

            var currentQuestions = isTemplate
                ? await _surveyRepository.GetSurveyTemplateQuestionsAsync(
                    connection,
                    transaction,
                    id,
                    cancellationToken)
                : await _surveyRepository.GetSurveyQuestionsAsync(
                    connection,
                    transaction,
                    id,
                    cancellationToken);
            var answeredOrganizationIds = isTemplate
                ? Array.Empty<int>()
                : await _surveyRepository.GetAnsweredOrganizationIdsForSurveyAsync(
                    connection,
                    id,
                    transaction,
                    cancellationToken);
            var requestedCriteria = questionRows
                .OrderBy(question => question.QuestionOrder)
                .Select(question => question.QuestionText)
                .ToArray();
            var currentCriteria = currentQuestions
                .OrderBy(question => question.Id)
                .Select(question => question.Text?.Trim() ?? string.Empty)
                .ToArray();
            var criteriaChanged = !currentCriteria.SequenceEqual(requestedCriteria, StringComparer.Ordinal);

            if (answeredOrganizationIds.Count > 0 && criteriaChanged)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    Message = "Нельзя изменить критерии: по анкете уже есть ответы."
                };
            }

            var removedAnsweredOrganizations = answeredOrganizationIds
                .Except(organizationIds)
                .ToArray();
            if (removedAnsweredOrganizations.Length > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    Message = removedAnsweredOrganizations.Length == 1
                        ? "Нельзя отменить назначение организации: по анкете уже есть ответ."
                        : "Нельзя отменить назначение организаций: по анкете уже есть ответы."
                };
            }

            var affectedRows = isTemplate
                ? await _surveyRepository.UpdateSurveyTemplateAsync(
                    connection,
                    transaction,
                    id,
                    title,
                    description,
                    startDate,
                    endDate,
                    ancestorId,
                    cancellationToken)
                : await _surveyRepository.UpdateSurveyAsync(
                    connection,
                    transaction,
                    id,
                    title,
                    description,
                    startDate,
                    endDate!.Value,
                    cancellationToken);

            if (affectedRows == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    NotFound = true,
                    Message = isTemplate ? "Шаблон не найден." : "Анкета не найдена."
                };
            }

            if (criteriaChanged)
            {
                var questions = questionRows.Select(question => new SurveyQuestionItem
                {
                    Id = question.QuestionOrder,
                    Text = question.QuestionText
                }).ToArray();
                if (isTemplate)
                {
                    await _surveyRepository.ReplaceSurveyTemplateQuestionsAsync(
                        connection,
                        transaction,
                        id,
                        questions,
                        cancellationToken);
                }
                else
                {
                    await _surveyRepository.ReplaceSurveyQuestionsAsync(
                        connection,
                        transaction,
                        id,
                        questions,
                        cancellationToken);
                }
            }
            if (isTemplate)
            {
                await _surveyRepository.ReplaceSurveyTemplateOrganizationsAsync(
                    connection,
                    transaction,
                    id,
                    organizationIds,
                    cancellationToken);
                await GetOrCreateConfigurationAsync(
                    connection,
                    transaction,
                    cancellationToken,
                    lockRow: false);
                isTemplateSelectedForAutoCreation = model!.IsAutoCreationEnabled;
                await _surveyRepository.SetSurveyTemplateAutoCreationAsync(
                    connection,
                    transaction,
                    SingletonConfigId,
                    id,
                    isTemplateSelectedForAutoCreation,
                    cancellationToken);
            }
            else
            {
                await _surveyRepository.ReplaceSurveyOrganizationsPreservingSchedulesAsync(
                    connection,
                    transaction,
                    id,
                    organizationIds,
                    startDate,
                    endDate!.Value,
                    cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);

            var createdByAutoCreation = isTemplate && !isPlannedTemplate && isTemplateSelectedForAutoCreation
                ? await TryRunPendingAfterTemplateSelectionAsync(cancellationToken)
                : 0;

            var successMessage = isTemplate
                ? isPlannedTemplate ? "Плановый шаблон успешно обновлён." : "Шаблон успешно обновлён."
                : "Анкета успешно обновлена.";
            if (isTemplate && wasTemplateSelectedForAutoCreation != isTemplateSelectedForAutoCreation)
            {
                successMessage += isTemplateSelectedForAutoCreation
                    ? " Шаблон успешно добавлен в автосоздание."
                    : " Шаблон успешно удалён из автосоздания.";
            }
            if (createdByAutoCreation > 0)
            {
                successMessage += $" Создано анкет: {createdByAutoCreation}.";
            }

            return new SurveyCommandResult
            {
                Success = true,
                Message = successMessage,
                SurveyId = id
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<int> TryRunPendingAfterTemplateSelectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await RunPendingAsync(cancellationToken);
            return result.Processed ? result.CreatedSurveyCount : 0;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Не удалось выполнить автосоздание сразу после изменения шаблона. Фоновая служба повторит попытку.");
            return 0;
        }
    }

    public async Task<SurveyCommandResult> UpdateActiveSurveysWorkPeriodAsync(
        SurveyWorkPeriodRequest? request,
        CancellationToken cancellationToken = default) =>
        await UpdateActiveSurveyWorkPeriodAsync(request, false, cancellationToken);

    private async Task<SurveyCommandResult> UpdateActiveSurveyWorkPeriodAsync(
        SurveyWorkPeriodRequest? request,
        bool isTemplate,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new SurveyCommandResult
            {
                Message = "Данные периода работы не предоставлены."
            };
        }

        if (!TryValidateDateRange(request.DateBegin, request.DateEnd, out var validationError))
        {
            return new SurveyCommandResult
            {
                Message = validationError
            };
        }

        if (!TryValidateStartDateNotFuture(request.DateBegin, out validationError))
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
                Message = "Дата конца не может быть раньше сегодняшней даты."
            };
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var affectedSurveyCount = await _surveyRepository.UpdateActiveSurveyPeriodAsync(
                connection,
                transaction,
                request.DateBegin,
                request.DateEnd,
                isTemplate,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new SurveyCommandResult
            {
                Success = true,
                Message = affectedSurveyCount == 0
                    ? isTemplate ? "Активные шаблоны не найдены." : "Активные анкеты не найдены."
                    : isTemplate ? "Период работы активных шаблонов сохранён." : "Период работы активных анкет сохранён."
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<OperationResult> SaveExtensionsAsync(
        SurveyExtensionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Extensions.Count == 0)
        {
            return new OperationResult
            {
                Success = false,
                Message = "Выберите хотя бы одну организацию для продления.",
                Error = "Выберите хотя бы одну организацию для продления."
            };
        }

        var validationErrors = ValidateExtensionRequest(request);
        if (validationErrors.Count > 0)
        {
            return new OperationResult
            {
                Success = false,
                Message = "Проверьте данные продления.",
                Error = "Проверьте данные продления.",
                Errors = validationErrors
            };
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var extensions = request.Extensions
            .GroupBy(item => item.OrganizationId)
            .Select(group => group.Last())
            .ToArray();
        var assignedOrganizationIds = (await _surveyRepository.GetSelectedOrganizationsForSurveyAsync(
                connection,
                request.SurveyId,
                cancellationToken))
            .Select(organization => organization.Id)
            .ToHashSet();

        if (extensions.Any(extension => !assignedOrganizationIds.Contains(extension.OrganizationId)))
        {
            const string message = "Продлить анкету можно только для уже назначенных организаций.";
            return new OperationResult
            {
                Success = false,
                Message = message,
                Error = message,
                Errors = [message]
            };
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var affectedAssignments = 0;
            foreach (var extension in extensions)
            {
                var endDate = DateTime.Parse(extension.ExtendedUntil).Date;
                var period = await _surveyRepository.GetAssignmentPeriodForUpdateAsync(
                    connection,
                    transaction,
                    request.SurveyId,
                    extension.OrganizationId,
                    cancellationToken);

                if (period == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    const string message = "Назначение анкеты для организации не найдено.";
                    return new OperationResult
                    {
                        Success = false,
                        Message = message,
                        Error = message,
                        Errors = [message]
                    };
                }

                if (!period.AssignmentDateEnd.HasValue || !period.BaseDateEnd.HasValue)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    const string message = "Доступ к анкете не ограничен датой конца и не требует продления.";
                    return new OperationResult
                    {
                        Success = false,
                        Message = message,
                        Error = message,
                        Errors = [message]
                    };
                }

                if (period.HasAnswer)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    const string message = "Нельзя продлить доступ: организация уже отправила ответ по анкете.";
                    return new OperationResult
                    {
                        Success = false,
                        Message = message,
                        Error = message,
                        Errors = [message]
                    };
                }

                var currentEndDate = period.AssignmentDateEnd.Value > period.BaseDateEnd.Value
                    ? period.AssignmentDateEnd.Value
                    : period.BaseDateEnd.Value;
                if (endDate <= currentEndDate.Date)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    const string message = "Новая дата конца должна быть позже текущей даты конца назначения.";
                    return new OperationResult
                    {
                        Success = false,
                        Message = message,
                        Error = message,
                        Errors = [message]
                    };
                }

                affectedAssignments += await _surveyRepository.UpdateAssignedSurveyEndDateAsync(
                    connection,
                    transaction,
                    request.SurveyId,
                    extension.OrganizationId,
                    endDate,
                    cancellationToken);
            }

            if (affectedAssignments != extensions.Length)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new OperationResult
                {
                    Success = false,
                    Message = "Назначение анкеты для организации не найдено.",
                    Error = "Назначение анкеты для организации не найдено."
                };
            }

            await transaction.CommitAsync(cancellationToken);

            return new OperationResult
            {
                Success = true,
                Message = extensions.Length == 1
                    ? "Доступ к анкете для организации успешно продлён."
                    : "Доступ к анкете для организаций успешно продлён.",
                EntityId = request.SurveyId
            };
        }
        catch (PostgresException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Ошибка PostgreSQL при продлении анкеты {SurveyId}", request.SurveyId);

            return new OperationResult
            {
                Success = false,
                Message = "Не удалось продлить доступ к анкете.",
                Error = "Не удалось продлить доступ к анкете.",
                Code = ex.SqlState
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Ошибка при продлении анкеты {SurveyId}", request.SurveyId);

            return new OperationResult
            {
                Success = false,
                Message = "Не удалось продлить доступ к анкете.",
                Error = "Не удалось продлить доступ к анкете."
            };
        }
    }

    public async Task<OperationResult> UpdateExtensionPeriodAsync(
        int surveyId,
        int organizationId,
        SurveyAssignmentPeriodRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0 || organizationId <= 0)
        {
            return new OperationResult
            {
                Message = "Продлённое назначение не найдено.",
                Code = "extension_not_found"
            };
        }

        if (request == null)
        {
            return new OperationResult { Message = "Данные периода не предоставлены." };
        }

        if (request.DateEnd == default)
        {
            return new OperationResult { Message = "Укажите дату конца." };
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var period = await _surveyRepository.GetAssignmentPeriodForUpdateAsync(
                connection,
                transaction,
                surveyId,
                organizationId,
                cancellationToken);

            if (period?.AssignmentDateEnd == null
                || period.BaseDateEnd == null
                || period.AssignmentDateEnd.Value.Date <= period.BaseDateEnd.Value.Date)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult
                {
                    Message = "Продлённое назначение не найдено.",
                    Code = "extension_not_found"
                };
            }

            var requestedDateEnd = request.DateEnd.Date;
            var baseDateEnd = period.BaseDateEnd.Value.Date;
            if (requestedDateEnd < baseDateEnd)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult
                {
                    Message = "Дата конца продления не может быть раньше даты конца анкеты."
                };
            }

            var hasAnswerAfterRequestedEnd = await _surveyRepository.HasAnswerCompletedAfterAsync(
                connection,
                transaction,
                period.AssignmentId,
                requestedDateEnd,
                cancellationToken);
            if (hasAnswerAfterRequestedEnd)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult
                {
                    Message = requestedDateEnd == baseDateEnd
                        ? "Нельзя удалить продление: по анкете был отправлен ответ в продлённый период."
                        : "Дата конца продления не может быть раньше даты отправки ответа.",
                    Code = "extension_answer_in_extended_period"
                };
            }

            if (requestedDateEnd == baseDateEnd)
            {
                var resetRows = await _surveyRepository.ResetExtensionPeriodAsync(
                    connection,
                    transaction,
                    surveyId,
                    organizationId,
                    cancellationToken);

                if (resetRows == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new OperationResult
                    {
                        Message = "Продлённое назначение не найдено.",
                        Code = "extension_not_found"
                    };
                }

                await transaction.CommitAsync(cancellationToken);
                return new OperationResult
                {
                    Success = true,
                    Message = "Продление успешно отменено.",
                    EntityId = surveyId
                };
            }

            if (!TryValidateDateRange(period.AssignmentDateBegin, requestedDateEnd, out var validationError)
                || !TryValidateEndDateNotPast(requestedDateEnd, out validationError))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult { Message = validationError };
            }

            var affectedRows = await _surveyRepository.UpdateExtensionEndDateAsync(
                connection,
                transaction,
                surveyId,
                organizationId,
                requestedDateEnd,
                cancellationToken);

            if (affectedRows == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult
                {
                    Message = "Продлённое назначение не найдено.",
                    Code = "extension_not_found"
                };
            }

            await transaction.CommitAsync(cancellationToken);
            return new OperationResult
            {
                Success = true,
                Message = "Дата конца продления успешно изменена.",
                EntityId = surveyId
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public virtual async Task<OperationResult> DeleteExtensionAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0 || organizationId <= 0)
        {
            return new OperationResult
            {
                Message = "Продлённое назначение не найдено.",
                Code = "extension_not_found"
            };
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var period = await _surveyRepository.GetAssignmentPeriodForUpdateAsync(
                connection,
                transaction,
                surveyId,
                organizationId,
                cancellationToken);

            if (period?.AssignmentDateEnd == null
                || period.BaseDateEnd == null
                || period.AssignmentDateEnd.Value.Date <= period.BaseDateEnd.Value.Date)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult
                {
                    Message = "Продлённое назначение не найдено.",
                    Code = "extension_not_found"
                };
            }

            if (await _surveyRepository.HasAnswerCompletedAfterAsync(
                    connection,
                    transaction,
                    period.AssignmentId,
                    period.BaseDateEnd.Value,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult
                {
                    Message = "Нельзя удалить продление: по анкете был отправлен ответ в продлённый период.",
                    Code = "extension_answer_in_extended_period"
                };
            }

            var affectedRows = await _surveyRepository.ResetExtensionPeriodAsync(
                connection,
                transaction,
                surveyId,
                organizationId,
                cancellationToken);
            if (affectedRows != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult
                {
                    Message = "Продлённое назначение не найдено.",
                    Code = "extension_not_found"
                };
            }

            await transaction.CommitAsync(cancellationToken);
            return new OperationResult
            {
                Success = true,
                Message = "Продление успешно удалено.",
                EntityId = surveyId
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
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
            var originalSurvey = await _surveyRepository.GetSurveyByIdAsync(connection, transaction, id, cancellationToken);

            if (originalSurvey == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    NotFound = true,
                    Message = "Анкета не найдена."
                };
            }

            var newSurveyId = await _surveyRepository.CreateSurveyAsync(
                connection,
                transaction,
                originalSurvey.NameSurvey,
                originalSurvey.Description,
                startDate,
                endDate,
                cancellationToken);

            await _surveyRepository.CopySurveyQuestionsAsync(connection, transaction, id, newSurveyId, cancellationToken);

            var organizationIds = await _surveyRepository.GetOrganizationIdsAsync(
                connection,
                transaction,
                id,
                cancellationToken);

            await _surveyRepository.UpsertSurveyAssignmentsAsync(
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
                Message = "Анкета успешно скопирована.",
                SurveyId = newSurveyId
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public virtual async Task<OperationResult> DeleteSurveyAsync(int surveyId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var survey = await connection.QueryFirstOrDefaultAsync<SurveyDeletionCandidate>(new CommandDefinition(
                """
                SELECT
                    id_survey AS SurveyId,
                    name_survey AS SurveyName
                FROM public.survey
                WHERE id_survey = @SurveyId
                FOR UPDATE;
                """,
                new { SurveyId = surveyId },
                transaction,
                cancellationToken: cancellationToken));
            if (survey == null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new OperationResult
                {
                    Success = false,
                    Message = "Анкета не найдена.",
                    Code = "survey_not_found"
                };
            }

            var usage = (await connection.QueryAsync<SurveyDeletionUsage>(new CommandDefinition(
                """
                SELECT
                    assignment.id_organization_survey AS AssignmentId,
                    COALESCE(
                        NULLIF(TRIM(o.organization_short_name), ''),
                        NULLIF(TRIM(o.organization_name), ''),
                        'Организация #' || assignment.id_organization::text
                    ) AS OrganizationName,
                    EXISTS (
                        SELECT 1
                        FROM public.answer a
                        WHERE a.id_organization_survey = assignment.id_organization_survey
                    ) AS HasAnswer
                FROM public.organization_survey assignment
                LEFT JOIN public.organization o
                    ON o.id_organization = assignment.id_organization
                WHERE assignment.id_survey = @SurveyId
                ORDER BY OrganizationName
                FOR UPDATE OF assignment;
                """,
                new { SurveyId = surveyId },
                transaction,
                cancellationToken: cancellationToken))).AsList();
            if (usage.Any(item => item.HasAnswer))
            {
                await transaction.CommitAsync(cancellationToken);
                return new OperationResult
                {
                    Success = false,
                    Message = BuildSurveyDeleteBlockedMessage(survey.SurveyName, usage),
                    Code = "survey_in_use"
                };
            }

            var assignmentIds = usage.Select(item => item.AssignmentId).ToArray();
            if (assignmentIds.Length > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM public.answer_draft WHERE id_organization_survey = ANY(@AssignmentIds);",
                    new { AssignmentIds = assignmentIds },
                    transaction,
                    cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM public.organization_survey WHERE id_organization_survey = ANY(@AssignmentIds);",
                    new { AssignmentIds = assignmentIds },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            if (!await _surveyRepository.DeleteSurveyAsync(connection, transaction, surveyId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult
                {
                    Success = false,
                    Message = "Анкета не найдена.",
                    Code = "survey_not_found"
                };
            }

            await transaction.CommitAsync(cancellationToken);
            return new OperationResult
            {
                Success = true,
                Message = "Анкета успешно удалена."
            };
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OperationResult
            {
                Success = false,
                Message = $"Нельзя удалить анкету \"{surveyId}\": она связана с сохранёнными данными.",
                Code = "survey_in_use"
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<OperationResult> DeleteSurveyTemplateAsync(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        if (templateId <= 0)
        {
            return new OperationResult
            {
                Message = "Некорректный идентификатор шаблона.",
                Code = "survey_template_not_found"
            };
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await _surveyRepository.HasPlannedSurveyTemplateDescendantsAsync(
                    connection,
                    transaction,
                    templateId,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult
                {
                    Message = "Нельзя удалить шаблон: он выбран родителем для планового шаблона.",
                    Code = "survey_template_in_use"
                };
            }

            if (!await _surveyRepository.DeleteSurveyTemplateAsync(
                    connection,
                    transaction,
                    templateId,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OperationResult
                {
                    Message = "Шаблон не найден.",
                    Code = "survey_template_not_found"
                };
            }

            await transaction.CommitAsync(cancellationToken);
            return new OperationResult
            {
                Success = true,
                Message = "Шаблон успешно удалён."
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string BuildSurveyDeleteBlockedMessage(
        string? surveyName,
        IReadOnlyList<SurveyDeletionUsage> usage)
    {
        var displayName = string.IsNullOrWhiteSpace(surveyName) ? "Без названия" : surveyName.Trim();
        var answeredOrganizationNames = usage
            .Where(item => item.HasAnswer)
            .Select(item => item.OrganizationName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var message = $"Нельзя удалить анкету \"{displayName}\": по ней есть ответы.";

        if (answeredOrganizationNames.Length > 0)
        {
            message += $"{Environment.NewLine}Ответы получены от: {string.Join(", ", answeredOrganizationNames)}.";
        }

        return message;
    }

    private sealed record SurveyDeletionCandidate(int SurveyId, string? SurveyName);

    private sealed record SurveyDeletionUsage(int AssignmentId, string OrganizationName, bool HasAnswer);
}
