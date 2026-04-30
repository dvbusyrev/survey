using System.Text.RegularExpressions;
using System.Text;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.Security;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace MainProject.Application.UseCases.Admin;

public sealed class UserManagementService : IUserManagementService
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

    public UserListPageViewModel GetArchivedUsersPage()
    {
        return new UserListPageViewModel
        {
            Users = GetUsers(includeArchived: true),
            Organizations = GetOrganizationOptions()
        };
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
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, '') AS organization_name,
                COALESCE(u.id_organization, 0) AS OrganizationId,
                u.name_role,
                u.date_begin,
                u.date_end,
                u.hash_password
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON u.id_organization = o.id_organization
            WHERE u.id_user = @id;
            """,
            new { id });
    }

    public OperationResult CreateUser(UserSaveRequest request)
    {
        if (!TryValidateUserCreateRequest(
            request,
            out var organizationId,
            out var normalizedRole,
            out var dateBegin,
            out var dateEnd,
            out var validationError))
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
                id_organization,
                name_user,
                full_name,
                name_role,
                hash_password,
                email,
                date_begin,
                date_end
            )
            VALUES (
                @organizationId,
                @userName,
                @fullName,
                @role,
                @hashPassword,
                @email,
                @dateBegin,
                @dateEnd
            );
            """,
            new
            {
                organizationId,
                userName = request.Username.Trim(),
                fullName = request.FullName.Trim(),
                role = normalizedRole,
                hashPassword = PasswordHasher.HashPassword(request.Username.Trim(), request.Password),
                email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                dateBegin,
                dateEnd
            });

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? $"Добавлен Клиент: {request.Username.Trim()}"
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
                id_organization = @organizationId,
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
                : "Клиент не найден или данные не изменились"
        };
    }

    public OperationResult DeleteUser(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        var user = connection.QueryFirstOrDefault<UserDeleteCandidate>(
            """
            SELECT
                id_user AS IdUser,
                full_name AS FullName,
                name_user AS UserName
            FROM public.app_user
            WHERE id_user = @id;
            """,
            new { id });

        if (user == null)
        {
            return new OperationResult
            {
                Success = false,
                Message = "Пользователь с указанным ID не найден."
            };
        }

        var answeredSurveyNames = GetUserAnsweredSurveyNames(connection, id);
        var signedSurveyNames = GetUserSignedSurveyNames(connection, id);
        if (answeredSurveyNames.Count > 0 || signedSurveyNames.Count > 0)
        {
            return new OperationResult
            {
                Success = false,
                Message = BuildUserDeleteBlockedMessage(
                    ResolveUserDisplayName(user),
                    answeredSurveyNames,
                    signedSurveyNames)
            };
        }

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
                id_organization AS Id,
                COALESCE(NULLIF(organization_short_name, ''), organization_name) AS Name
            FROM public.organization
            WHERE date_end IS NULL
               OR date_end >= CURRENT_DATE
            ORDER BY COALESCE(NULLIF(organization_short_name, ''), organization_name);
            """).ToList();
    }

    private static bool TryValidateUserCreateRequest(
        UserSaveRequest request,
        out int organizationId,
        out string normalizedRole,
        out DateTime? dateBegin,
        out DateTime? dateEnd,
        out string validationError)
    {
        normalizedRole = AppRoles.Normalize(request.Role);
        validationError = string.Empty;
        dateBegin = null;
        dateEnd = null;

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
            validationError = "Дата конца не может быть раньше даты начала.";
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

    private static string ResolveUserDisplayName(UserDeleteCandidate user)
    {
        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            return user.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            return user.UserName.Trim();
        }

        return $"ID {user.IdUser}";
    }

    private static string BuildUserDeleteBlockedMessage(
        string userDisplayName,
        IReadOnlyList<string> answeredSurveyNames,
        IReadOnlyList<string> signedSurveyNames)
    {
        var builder = new StringBuilder();
        builder.Append($"Пользователь \"{userDisplayName}\" не может быть удалён, так как уже работал с анкетами.");

        if (answeredSurveyNames.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Отвечал на анкеты: ");
            builder.Append(string.Join(", ", answeredSurveyNames));
            builder.Append('.');
        }

        if (signedSurveyNames.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Подписывал анкеты: ");
            builder.Append(string.Join(", ", signedSurveyNames));
            builder.Append('.');
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> GetUserAnsweredSurveyNames(
        global::Npgsql.NpgsqlConnection connection,
        int userId)
    {
        return connection.Query<string>(
            """
            SELECT DISTINCT
                COALESCE(NULLIF(TRIM(s.name_survey), ''), 'Анкета #' || audit_row.SurveyId::text) AS survey_name
            FROM (
                SELECT
                    audit_raw.changed_by_user_id,
                    COALESCE(audit_raw.SurveyId, os.id_survey) AS SurveyId,
                    audit_raw.SignatureValue
                FROM (
                    SELECT
                        changed_by_user_id,
                        CASE
                            WHEN COALESCE(row_data->>'id_survey', '') ~ '^[0-9]+$'
                                THEN (row_data->>'id_survey')::integer
                            ELSE NULL
                        END AS SurveyId,
                        CASE
                            WHEN COALESCE(row_data->>'id_organization_survey', record_pk->>'id_organization_survey', '') ~ '^[0-9]+$'
                                THEN COALESCE(row_data->>'id_organization_survey', record_pk->>'id_organization_survey')::integer
                            ELSE NULL
                        END AS IdOrganizationSurvey,
                        COALESCE(row_data->>'csp', '') AS SignatureValue
                    FROM public.answer_l
                ) audit_raw
                LEFT JOIN public.organization_survey os
                    ON os.id_organization_survey = audit_raw.IdOrganizationSurvey
            ) audit_row
            LEFT JOIN public.survey s
                ON s.id_survey = audit_row.SurveyId
            WHERE audit_row.changed_by_user_id = @userId
              AND audit_row.SurveyId IS NOT NULL
              AND audit_row.SignatureValue = ''
            ORDER BY survey_name;
            """,
            new { userId })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetUserSignedSurveyNames(
        global::Npgsql.NpgsqlConnection connection,
        int userId)
    {
        return connection.Query<string>(
            """
            SELECT DISTINCT
                COALESCE(NULLIF(TRIM(s.name_survey), ''), 'Анкета #' || audit_row.SurveyId::text) AS survey_name
            FROM (
                SELECT
                    audit_raw.changed_by_user_id,
                    COALESCE(audit_raw.SurveyId, os.id_survey) AS SurveyId,
                    audit_raw.SignatureValue
                FROM (
                    SELECT
                        changed_by_user_id,
                        CASE
                            WHEN COALESCE(row_data->>'id_survey', '') ~ '^[0-9]+$'
                                THEN (row_data->>'id_survey')::integer
                            ELSE NULL
                        END AS SurveyId,
                        CASE
                            WHEN COALESCE(row_data->>'id_organization_survey', record_pk->>'id_organization_survey', '') ~ '^[0-9]+$'
                                THEN COALESCE(row_data->>'id_organization_survey', record_pk->>'id_organization_survey')::integer
                            ELSE NULL
                        END AS IdOrganizationSurvey,
                        COALESCE(row_data->>'csp', '') AS SignatureValue
                    FROM public.answer_l
                ) audit_raw
                LEFT JOIN public.organization_survey os
                    ON os.id_organization_survey = audit_raw.IdOrganizationSurvey
            ) audit_row
            LEFT JOIN public.survey s
                ON s.id_survey = audit_row.SurveyId
            WHERE audit_row.changed_by_user_id = @userId
              AND audit_row.SurveyId IS NOT NULL
              AND audit_row.SignatureValue <> ''
            ORDER BY survey_name;
            """,
            new { userId })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static class UserQueries
    {
        public const string ActiveUsers = """
            SELECT
                u.id_user,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, '') AS organization_name,
                u.name_user,
                u.name_role,
                u.hash_password,
                u.date_begin,
                u.date_end,
                u.full_name,
                u.email,
                COALESCE(u.id_organization, 0) AS OrganizationId
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON u.id_organization = o.id_organization
            WHERE u.date_end IS NULL OR u.date_end >= CURRENT_DATE
            ORDER BY COALESCE(u.full_name, u.name_user), u.id_user;
            """;

        public const string ArchivedUsers = """
            SELECT
                u.id_user,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, '') AS organization_name,
                u.name_user,
                u.name_role,
                u.hash_password,
                u.date_begin,
                u.date_end,
                u.full_name,
                u.email,
                COALESCE(u.id_organization, 0) AS OrganizationId
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON u.id_organization = o.id_organization
            WHERE u.date_end < CURRENT_DATE
            ORDER BY COALESCE(u.full_name, u.name_user), u.id_user;
            """;
    }

    private sealed class UserDeleteCandidate
    {
        public int IdUser { get; set; }
        public string? FullName { get; set; }
        public string? UserName { get; set; }
    }
}
