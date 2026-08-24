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
            await _surveyRepository.GetActiveOrganizationOptionsAsync(connection, cancellationToken));
        var totalCount = await _surveyRepository.CountActiveSurveysAsync(connection, selectedOrganizationIds, cancellationToken);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var pageRows = await _surveyRepository.GetActiveSurveyPageAsync(
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
        var surveys = (await _surveyRepository.GetActiveSurveySummariesAsync(connection, cancellationToken)).ToList();
        await _surveyRepository.AttachQuestionsAsync(connection, surveys, cancellationToken);
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
            var newSurveyId = await _surveyRepository.CreateSurveyAsync(
                connection,
                transaction,
                title,
                description,
                startDate,
                endDate,
                cancellationToken);

            await _surveyRepository.ReplaceSurveyQuestionsAsync(
                connection,
                transaction,
                newSurveyId,
                questionRows.Select(question => new SurveyQuestionItem
                {
                    Id = question.QuestionOrder,
                    Text = question.QuestionText
                }).ToArray(), cancellationToken);
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
                Message = "Анкета успешно создана.",
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

        return new SurveyEditPageViewModel
        {
            Survey = survey,
            AllOrganization = allOrganization,
            SelectedOrganizationIds = selectedOrganization.Select(o => o.Id).ToList(),
            SelectedOrganizationNames = selectedOrganization.Select(o => o.Name).ToList(),
            Criteria = await _surveyRepository.GetSurveyCriteriaAsync(connection, id, cancellationToken)
        };
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
            var affectedRows = await _surveyRepository.UpdateSurveyAsync(
                connection,
                transaction,
                id,
                title,
                description,
                startDate,
                endDate,
                cancellationToken);

            if (affectedRows == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SurveyCommandResult
                {
                    NotFound = true,
                    Message = "Анкета не найдена."
                };
            }

            await _surveyRepository.ReplaceSurveyQuestionsAsync(
                connection,
                transaction,
                id,
                questionRows.Select(question => new SurveyQuestionItem
                {
                    Id = question.QuestionOrder,
                    Text = question.QuestionText
                }).ToArray(),
                cancellationToken);
            await _surveyRepository.ReplaceSurveyOrganizationsPreservingSchedulesAsync(
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
                Message = "Анкета успешно обновлена.",
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
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new SurveyCommandResult
            {
                Success = true,
                Message = affectedSurveyCount == 0
                    ? "Активные анкеты не найдены."
                    : "Период работы активных анкет сохранён."
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
                SELECT id_survey AS SurveyId, name_survey AS SurveyName
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
