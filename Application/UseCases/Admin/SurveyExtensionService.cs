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

    public SurveyExtensionService(
        IDbConnectionFactory connectionFactory,
        ILogger<SurveyExtensionService> logger,
        ISurveyAssignmentRepository assignmentRepository)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _assignmentRepository = assignmentRepository;
    }

    public OperationResult SaveExtensions(SurveyExtensionRequest request)
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

        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var affectedAssignments = 0;
            foreach (var extension in request.Extensions
                         .GroupBy(item => item.OrganizationId)
                         .Select(group => group.Last()))
            {
                var endDate = DateTime.Parse(extension.ExtendedUntil).Date;

                affectedAssignments += _assignmentRepository.UpsertSurveyEndDate(
                    connection,
                    transaction,
                    request.SurveyId,
                    extension.OrganizationId,
                    endDate);
            }

            if (affectedAssignments == 0)
            {
                transaction.Rollback();

                return new OperationResult
                {
                    Success = false,
                    Message = "Анкета не найдена",
                    Error = "Анкета не найдена"
                };
            }

            transaction.Commit();

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
            transaction.Rollback();
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
            transaction.Rollback();
            _logger.LogError(ex, "Ошибка при продлении анкеты {SurveyId}", request.SurveyId);

            return new OperationResult
            {
                Success = false,
                Message = "Ошибка при обработке запроса",
                Error = ex.Message
            };
        }
    }

    private static IReadOnlyList<string> ValidateRequest(SurveyExtensionRequest request)
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

            if (!DateTime.TryParse(extension.ExtendedUntil, out var endDate) || endDate <= DateTime.Today)
            {
                errors.Add($"Неверная дата конца: {extension.ExtendedUntil}");
            }
        }

        return errors;
    }
}
