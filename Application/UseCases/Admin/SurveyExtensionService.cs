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

        if (request.Extensions.Count > 1)
        {
            return new OperationResult
            {
                Success = false,
                Message = "Можно продлить анкету только для одной организации за раз",
                Error = "Можно продлить анкету только для одной организации за раз"
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
            var extension = request.Extensions[0];
            var endDate = DateTime.Parse(extension.ExtendedUntil).Date;

            var affectedAssignments = connection.Execute(
                """
                INSERT INTO public.organization_survey (
                    id_organization,
                    id_survey,
                    date_end
                )
                SELECT
                    @organizationId,
                    s.id_survey,
                    @endDate::date
                FROM public.survey s
                WHERE s.id_survey = @surveyId
                ON CONFLICT (id_organization, id_survey) DO UPDATE
                SET
                    date_end = EXCLUDED.date_end;
                """,
                new
                {
                    surveyId = request.SurveyId,
                    organizationId = extension.OrganizationId,
                    endDate
                },
                transaction);

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
                Message = "Доступ к анкете для организации успешно продлён",
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

        if (request.Extensions.Count > 1)
        {
            errors.Add("Можно продлить анкету только для одной организации за раз");
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
