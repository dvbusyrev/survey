using Dapper;
using MainProject.Infrastructure.Database;
using Npgsql;

namespace MainProject.Services.Admin;

public sealed class SurveyExtensionService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SurveyExtensionService> _logger;

    public SurveyExtensionService(IDbConnectionFactory connectionFactory, ILogger<SurveyExtensionService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
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
            foreach (var extension in request.Extensions)
            {
                var endDate = DateTime.Parse(extension.ExtendedUntil);

                connection.Execute(
                    """
                    INSERT INTO public.organization_survey (organization_id, id_survey, extended_until)
                    VALUES (@organizationId, @surveyId, @endDate)
                    ON CONFLICT (organization_id, id_survey) DO UPDATE
                    SET extended_until = EXCLUDED.extended_until;
                    """,
                    new
                    {
                        surveyId = request.SurveyId,
                        organizationId = extension.OrganizationId,
                        endDate
                    },
                    transaction);
            }

            transaction.Commit();

            return new OperationResult
            {
                Success = true,
                Message = "Доступ к анкете успешно продлён",
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
                errors.Add($"Неверная дата окончания: {extension.ExtendedUntil}");
            }
        }

        return errors;
    }
}
