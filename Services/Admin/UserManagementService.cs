using System.Text.RegularExpressions;
using Dapper;
using MainProject.Infrastructure.Database;
using MainProject.Infrastructure.Security;
using MainProject.Models;
using Microsoft.AspNetCore.Identity;

namespace MainProject.Services.Admin;

public sealed class UserManagementService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private static readonly PasswordHasher<string> PasswordHasher = new();

    public UserManagementService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public UserListPageViewModel GetActiveUsersPage(bool openAddUserModal = false)
    {
        return new UserListPageViewModel
        {
            Users = GetUsers(includeArchived: false),
            Organizations = GetOrganizationOptions(),
            OpenAddUserModal = openAddUserModal
        };
    }

    public IReadOnlyList<User> GetArchivedUsers()
    {
        return GetUsers(includeArchived: true);
    }

    public User? GetUserById(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.QueryFirstOrDefault<User>(
            """
            SELECT
                u.id_user,
                u.full_name,
                u.name_user,
                u.email,
                COALESCE(o.organization_name, '') AS organization_name,
                COALESCE(u.organization_id, 0) AS organization_id,
                u.name_role,
                u.date_begin,
                u.date_end,
                u.hash_password
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON u.organization_id = o.organization_id
            WHERE u.id_user = @id;
            """,
            new { id });
    }

    public OperationResult CreateUser(UserSaveRequest request)
    {
        if (!TryValidateUserCreateRequest(request, out var organizationId, out var normalizedRole, out var validationError))
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
            INSERT INTO public.app_user (
                organization_id,
                name_user,
                full_name,
                name_role,
                hash_password,
                email,
                date_begin
            )
            VALUES (
                @organizationId,
                @userName,
                @fullName,
                @role,
                @hashPassword,
                @email,
                NOW()
            );
            """,
            new
            {
                organizationId,
                userName = request.Username.Trim(),
                fullName = request.FullName.Trim(),
                role = normalizedRole,
                hashPassword = PasswordHasher.HashPassword(request.Username.Trim(), request.Password),
                email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim()
            });

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? $"Добавлен пользователь: {request.Username.Trim()}"
                : "Не удалось добавить запись в БД"
        };
    }

    public OperationResult UpdateUser(int id, UserUpdateRequest request)
    {
        if (!TryValidateUserUpdateRequest(request, out var organizationId, out var normalizedRole, out var dateBegin, out var dateEnd, out var passwordHash, out var validationError))
        {
            return new OperationResult
            {
                Success = false,
                Message = validationError,
                Error = validationError
            };
        }

        using var connection = _connectionFactory.CreateConnection();

        var sql = """
            UPDATE public.app_user
            SET
                name_user = @userName,
                full_name = @fullName,
                organization_id = @organizationId,
                name_role = @role,
                email = @email,
                date_begin = @dateBegin,
                date_end = @dateEnd
            """;

        if (passwordHash != null)
        {
            sql += ", hash_password = @passwordHash";
        }

        sql += " WHERE id_user = @id";

        var affectedRows = connection.Execute(
            sql,
            new
            {
                id,
                userName = request.Username.Trim(),
                fullName = request.FullName.Trim(),
                organizationId,
                role = normalizedRole,
                email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                dateBegin,
                dateEnd,
                passwordHash
            });

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Данные пользователя успешно обновлены"
                : "Пользователь не найден или данные не изменились"
        };
    }

    public OperationResult DeleteUser(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        var affectedRows = connection.Execute(
            "DELETE FROM public.app_user WHERE id_user = @id;",
            new { id });

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Пользователь успешно удален."
                : "Пользователь с указанным ID не найден."
        };
    }

    private IReadOnlyList<User> GetUsers(bool includeArchived)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = includeArchived
            ? UserQueries.ArchivedUsers
            : UserQueries.ActiveUsers;

        return connection.Query<User>(sql).ToList();
    }

    private IReadOnlyList<SelectionOption> GetOrganizationOptions()
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.Query<SelectionOption>(
            """
            SELECT
                organization_id AS Id,
                organization_name AS Name
            FROM public.organization
            WHERE block = false
            ORDER BY organization_name;
            """).ToList();
    }

    private static bool TryValidateUserCreateRequest(
        UserSaveRequest request,
        out int organizationId,
        out string normalizedRole,
        out string validationError)
    {
        normalizedRole = AppRoles.Normalize(request.Role);
        validationError = string.Empty;

        if (!TryParseOrganizationId(request.OrganizationId, out organizationId))
        {
            validationError = "Не указана корректная организация.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            validationError = "Логин обязателен.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            validationError = "ФИО обязательно.";
            return false;
        }

        if (!AppRoles.IsSupported(normalizedRole))
        {
            validationError = $"Недопустимая роль. Допустимые значения: {string.Join(", ", AppRoles.SupportedRoles)}";
            return false;
        }

        if (!IsPasswordValid(request.Password, out validationError))
        {
            return false;
        }

        return true;
    }

    private static bool TryValidateUserUpdateRequest(
        UserUpdateRequest request,
        out int organizationId,
        out string normalizedRole,
        out DateTime? dateBegin,
        out DateTime? dateEnd,
        out string? passwordHash,
        out string validationError)
    {
        normalizedRole = AppRoles.Normalize(request.Role);
        validationError = string.Empty;
        passwordHash = null;

        if (!TryParseOrganizationId(request.OrganizationId, out organizationId))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Не указана корректная организация.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Логин обязателен.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "ФИО обязательно.";
            return false;
        }

        if (!AppRoles.IsSupported(normalizedRole))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = $"Недопустимая роль. Допустимые значения: {string.Join(", ", AppRoles.SupportedRoles)}";
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

        if (!string.IsNullOrWhiteSpace(request.Password)
            && !string.Equals(request.Password, "keep_original", StringComparison.Ordinal))
        {
            if (!IsPasswordValid(request.Password, out validationError))
            {
                return false;
            }

            passwordHash = PasswordHasher.HashPassword(request.Username.Trim(), request.Password);
        }

        return true;
    }

    private static bool TryParseOrganizationId(string? rawValue, out int organizationId)
    {
        organizationId = 0;
        return int.TryParse(rawValue, out organizationId) && organizationId > 0;
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

    private static bool IsPasswordValid(string password, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Пароль не должен быть пустым.";
            return false;
        }

        if (password.Length < 14)
        {
            errorMessage = "Пароль должен быть длиной не менее 14 символов.";
            return false;
        }

        if (!Regex.IsMatch(password, @"\p{Ll}"))
        {
            errorMessage = "Пароль должен содержать хотя бы одну строчную букву.";
            return false;
        }

        if (!Regex.IsMatch(password, @"\p{Lu}"))
        {
            errorMessage = "Пароль должен содержать хотя бы одну заглавную букву.";
            return false;
        }

        if (!Regex.IsMatch(password, "[0-9]"))
        {
            errorMessage = "Пароль должен содержать хотя бы одну цифру.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[^\p{L}\p{Nd}]"))
        {
            errorMessage = "Пароль должен содержать хотя бы один спецсимвол.";
            return false;
        }

        return true;
    }

    private static class UserQueries
    {
        public const string ActiveUsers = """
            SELECT
                u.id_user,
                COALESCE(o.organization_name, '') AS organization_name,
                u.name_user,
                u.name_role,
                u.hash_password,
                u.date_begin,
                u.date_end,
                u.full_name,
                u.email,
                COALESCE(u.organization_id, 0) AS organization_id
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON u.organization_id = o.organization_id
            WHERE u.date_end IS NULL OR u.date_end >= CURRENT_DATE
            ORDER BY COALESCE(u.full_name, u.name_user), u.id_user;
            """;

        public const string ArchivedUsers = """
            SELECT
                u.id_user,
                COALESCE(o.organization_name, '') AS organization_name,
                u.name_user,
                u.name_role,
                u.hash_password,
                u.date_begin,
                u.date_end,
                u.full_name,
                u.email,
                COALESCE(u.organization_id, 0) AS organization_id
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON u.organization_id = o.organization_id
            WHERE u.date_end < CURRENT_DATE
            ORDER BY COALESCE(u.full_name, u.name_user), u.id_user;
            """;
    }
}
