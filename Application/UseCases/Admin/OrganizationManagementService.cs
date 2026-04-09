using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Admin;

public sealed class OrganizationManagementService : IOrganizationManagementService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrganizationManagementService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public OrganizationListPageViewModel GetActiveOrganizationsPage(bool openAddOrganizationModal = false)
    {
        return new OrganizationListPageViewModel
        {
            Organizations = GetOrganizations(includeArchived: false),
            OpenAddOrganizationModal = openAddOrganizationModal
        };
    }

    public IReadOnlyList<Organization> GetArchivedOrganizations()
    {
        return GetOrganizations(includeArchived: true);
    }

    public IReadOnlyList<OrganizationDataResponse> GetOrganizationOptions()
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.Query<OrganizationDataResponse>(
            """
            SELECT
                organization_id AS Id,
                organization_name AS Name
            FROM public.organization
            WHERE block = false
            ORDER BY organization_name;
            """).ToList();
    }

    public Organization? GetOrganizationById(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.QueryFirstOrDefault<Organization>(
            """
            SELECT
                organization_name,
                email,
                date_begin,
                date_end,
                organization_id
            FROM public.organization
            WHERE organization_id = @id;
            """,
            new { id });
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

        using var connection = _connectionFactory.CreateConnection();

        var organizationId = connection.ExecuteScalar<int>(
            """
            INSERT INTO public.organization (
                organization_name,
                email,
                date_begin,
                date_end,
                block
            )
            VALUES (
                @name,
                @email,
                @dateBegin,
                @dateEnd,
                false
            )
            RETURNING organization_id;
            """,
            new
            {
                name = request.Name.Trim(),
                email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                dateBegin,
                dateEnd
            });

        return new OperationResult
        {
            Success = true,
            Message = "Организация успешно создана.",
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

        using var connection = _connectionFactory.CreateConnection();

        var affectedRows = connection.Execute(
            """
            UPDATE public.organization
            SET
                organization_name = @name,
                email = @email,
                date_begin = @dateBegin,
                date_end = @dateEnd,
                block = CASE
                    WHEN @shouldRestoreAccess THEN false
                    ELSE block
                END
            WHERE organization_id = @id;
            """,
            new
            {
                id,
                name = request.Name.Trim(),
                email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                dateBegin,
                dateEnd,
                shouldRestoreAccess = !dateEnd.HasValue || dateEnd.Value.Date >= DateTime.Today
            });

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
        using var connection = _connectionFactory.CreateConnection();

        var affectedRows = connection.Execute(
            "UPDATE public.organization SET block = true WHERE organization_id = @id;",
            new { id });

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Организация успешно удалена."
                : "Произошла ошибка при удалении организации."
        };
    }

    private IReadOnlyList<Organization> GetOrganizations(bool includeArchived)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.Query<Organization>(
            """
            SELECT
                o.organization_id,
                o.organization_name,
                o.date_begin,
                o.date_end,
                COALESCE((
                    SELECT string_agg(s.name_survey, ', ' ORDER BY s.name_survey)
                    FROM public.organization_survey os
                    INNER JOIN public.survey s
                        ON s.id_survey = os.id_survey
                    WHERE os.organization_id = o.organization_id
                ), 'Не указано') AS survey_names,
                o.block,
                o.email
            FROM public.organization o
            WHERE o.block = @includeArchived
            ORDER BY o.organization_name;
            """,
            new { includeArchived }).ToList();
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
            validationError = "Дата окончания не может быть раньше даты начала.";
            return false;
        }

        return true;
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
