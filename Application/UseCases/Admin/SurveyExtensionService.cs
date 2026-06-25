using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using Npgsql;

namespace MainProject.Application.UseCases.Admin;

public sealed class SurveyExtensionService : ISurveyExtensionService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SurveyExtensionService> _logger;
    private readonly ISurveyAssignmentRepository _assignmentRepository;
    private readonly IClock _clock;

    public SurveyExtensionService(
        IDbConnectionFactory connectionFactory,
        ILogger<SurveyExtensionService> logger,
        ISurveyAssignmentRepository assignmentRepository,
        IClock clock)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _assignmentRepository = assignmentRepository;
        _clock = clock;
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
                Message = "Необходимо предоставить данные для продления",
                Error = "Необходимо предоставить данные для продления"
            };
        }

        var validationErrors = ValidateRequest(request);
        if (validationErrors.Count > 0)
        {
            return new OperationResult
            {
                Success = false,
                Message = "Ошибки валидации",
                Error = "Ошибки валидации",
                Errors = validationErrors
            };
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var affectedAssignments = 0;
            foreach (var extension in request.Extensions
                         .GroupBy(item => item.OrganizationId)
                         .Select(group => group.Last()))
            {
                var endDate = DateTime.Parse(extension.ExtendedUntil).Date;

                affectedAssignments += await _assignmentRepository.UpsertSurveyEndDateAsync(
                    connection,
                    transaction,
                    request.SurveyId,
                    extension.OrganizationId,
                    endDate,
                    cancellationToken);
            }

            if (affectedAssignments == 0)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new OperationResult
                {
                    Success = false,
                    Message = "Анкета не найдена",
                    Error = "Анкета не найдена"
                };
            }

            await transaction.CommitAsync(cancellationToken);

            return new OperationResult
            {
                Success = true,
                Message = request.Extensions.Count == 1
                    ? "Доступ к анкете для организации успешно продлён"
                    : "Доступ к анкете для организаций успешно продлён",
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
                Message = "Ошибка базы данных",
                Error = ex.Message,
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
                Message = "Ошибка при обработке запроса",
                Error = ex.Message
            };
        }
    }

    private IReadOnlyList<string> ValidateRequest(SurveyExtensionRequest request)
    {
        var errors = new List<string>();

        if (request.SurveyId <= 0)
        {
            errors.Add("Неверный ID анкеты");
        }

        foreach (var extension in request.Extensions)
        {
            if (extension.OrganizationId <= 0)
            {
                errors.Add($"Неверный ID организации: {extension.OrganizationId}");
            }

            if (!DateTime.TryParse(extension.ExtendedUntil, out var endDate) || endDate.Date <= _clock.Today.Date)
            {
                errors.Add($"Неверная дата конца: {extension.ExtendedUntil}");
            }
        }

        return errors;
    }
}
