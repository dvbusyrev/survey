using System.Text.RegularExpressions;
using System.Text;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Infrastructure.Security;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace MainProject.Application.UseCases.Admin;

public sealed class UserManagementService
{
    private const string DuplicateLoginMessage = "Пользователь с таким логином существует.";
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IClock _clock;
    private static readonly PasswordHasher<string> PasswordHasher = new();

    public UserManagementService(IDbConnectionFactory connectionFactory, IClock clock)
    {
        _connectionFactory = connectionFactory;
        _clock = clock;
    }

    public Task<UserListPageViewModel> GetActiveUsersPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool openAddUserModal = false,
        CancellationToken cancellationToken = default)
    {
        return GetUsersPageAsync(currentPage, sortBy, sortDirection, includeArchived: false, openAddUserModal, cancellationToken);
    }

    public Task<IReadOnlyList<User>> GetArchivedUsersAsync(CancellationToken cancellationToken = default)
    {
        return GetUsersAsync(includeArchived: true, cancellationToken);
    }

    public Task<UserListPageViewModel> GetArchivedUsersPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default)
    {
        return GetUsersPageAsync(currentPage, sortBy, sortDirection, includeArchived: true, cancellationToken: cancellationToken);
    }

    private async Task<UserListPageViewModel> GetUsersPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool includeArchived,
        bool openAddUserModal = false,
        CancellationToken cancellationToken = default)
    {
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeUserSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeUserSortDirection(null, normalizedSortBy);

        var totalCount = await CountAsync(includeArchived, cancellationToken);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var users = await GetPageAsync(
            includeArchived,
            normalizedSortBy,
            normalizedSortDirection,
            pageWindow.PageSize,
            pageWindow.Offset,
            cancellationToken);
        var organizations = await GetActiveOrganizationOptionsAsync(cancellationToken);

        return new UserListPageViewModel
        {
            Users = users,
            Organizations = organizations,
            OpenAddUserModal = openAddUserModal,
            CurrentPage = pageWindow.CurrentPage,
            TotalPages = pageWindow.TotalPages,
            TotalCount = pageWindow.TotalCount,
            PageSize = pageWindow.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            ViewModeIsArchive = includeArchived
        };
    }

    public Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, cancellationToken);
    }

    public async Task<OperationResult> CreateUserAsync(UserSaveRequest request, CancellationToken cancellationToken = default)
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

        int affectedRows;
        try
        {
            affectedRows = await CreateAsync(new UserWriteModel(
                organizationId,
                request.Username.Trim(),
                request.FullName.Trim(),
                normalizedRole,
                PasswordHasher.HashPassword(request.Username.Trim(), request.Password),
                string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                dateBegin,
                dateEnd), cancellationToken);
        }
        catch (PostgresException ex) when (IsDuplicateLoginViolation(ex))
        {
            return new OperationResult
            {
                Success = false,
                Message = DuplicateLoginMessage,
                Error = DuplicateLoginMessage
            };
        }

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Пользователь успешно добавлен."
                : "Не удалось создать пользователя."
        };
    }

    public async Task<OperationResult> UpdateUserAsync(int id, UserUpdateRequest request, CancellationToken cancellationToken = default)
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

        int affectedRows;
        try
        {
            affectedRows = await UpdateAsync(id, new UserWriteModel(
                organizationId,
                request.Username.Trim(),
                request.FullName.Trim(),
                normalizedRole,
                passwordHash,
                string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                dateBegin,
                dateEnd), cancellationToken);
        }
        catch (PostgresException ex) when (IsDuplicateLoginViolation(ex))
        {
            return new OperationResult
            {
                Success = false,
                Message = DuplicateLoginMessage,
                Error = DuplicateLoginMessage
            };
        }

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Данные пользователя успешно обновлены."
                : "Пользователь не найден или данные не изменились."
        };
    }

    public async Task<OperationResult> DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        UserDeletionResult result;
        try
        {
            result = await DeleteIfAllowedAsync(id, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation)
        {
            const string message = "Нельзя удалить пользователя: он связан с сохранёнными ответами анкет.";
            return new OperationResult
            {
                Success = false,
                Message = message,
                Error = message,
                Code = "user_in_use"
            };
        }

        if (!result.Found || result.User == null)
        {
            return new OperationResult
            {
                Success = false,
                Message = "Пользователь не найден.",
                Code = "user_not_found"
            };
        }

        if (result.AnsweredSurveyNames.Count > 0 || result.SignedSurveyNames.Count > 0)
        {
            return new OperationResult
            {
                Success = false,
                Message = BuildUserDeleteBlockedMessage(
                    ResolveUserDisplayName(result.User),
                    result.AnsweredSurveyNames,
                    result.SignedSurveyNames),
                Code = "user_in_use"
            };
        }

        return new OperationResult
        {
            Success = result.Deleted,
            Message = result.Deleted
                ? "Пользователь успешно удалён."
                : "Пользователь не найден.",
            Code = result.Deleted ? null : "user_not_found"
        };
    }

    private Task<IReadOnlyList<User>> GetUsersAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        return GetAllAsync(includeArchived, cancellationToken);
    }

    private async Task<int> CountAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM public.app_user u WHERE {GetArchivePredicate(includeArchived)};",
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyList<User>> GetPageAsync(
        bool includeArchived,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var users = await connection.QueryAsync<User>(new CommandDefinition(
            $"""
            {UserSelectSql}
            WHERE {GetArchivePredicate(includeArchived)}
            ORDER BY {BuildOrderBy(sortBy, sortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { PageSize = pageSize, Offset = offset, Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return users.AsList();
    }

    private async Task<IReadOnlyList<User>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var users = await connection.QueryAsync<User>(new CommandDefinition(
            $"""
            {UserSelectSql}
            WHERE {GetArchivePredicate(includeArchived)}
            ORDER BY COALESCE(u.full_name, u.login), u.id_user;
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return users.AsList();
    }

    private async Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<User>(new CommandDefinition(
            $"""
            {UserSelectSql}
            WHERE u.id_user = @UserId;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyList<SelectionOption>> GetActiveOrganizationOptionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var options = await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            """
            SELECT id_organization AS Id, COALESCE(NULLIF(organization_short_name, ''), organization_name) AS Name
            FROM public.organization
            WHERE date_end IS NULL OR date_end >= @Today
            ORDER BY COALESCE(NULLIF(organization_short_name, ''), organization_name);
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return options.AsList();
    }

    private async Task<int> CreateAsync(UserWriteModel user, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, email, date_begin, date_end)
            VALUES (@OrganizationId, @Login, @FullName, @Role, @PasswordHash, @Email, @DateBegin, @DateEnd);
            """,
            user,
            cancellationToken: cancellationToken));
    }

    private async Task<int> UpdateAsync(int userId, UserWriteModel user, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var sql = """
            UPDATE public.app_user
            SET login = @Login, full_name = @FullName, id_organization = @OrganizationId,
                role = @Role, email = @Email, date_begin = @DateBegin, date_end = @DateEnd
            """;
        if (user.PasswordHash != null)
        {
            sql += ", password = @PasswordHash";
        }

        sql += " WHERE id_user = @UserId;";
        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { UserId = userId, user.OrganizationId, user.Login, user.FullName, user.Role, user.Email, user.DateBegin, user.DateEnd, user.PasswordHash },
            cancellationToken: cancellationToken));
    }

    private async Task<UserDeletionResult> DeleteIfAllowedAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var user = await connection.QueryFirstOrDefaultAsync<UserDeleteCandidate>(new CommandDefinition(
            """
            SELECT id_user AS IdUser, full_name AS FullName, login AS UserName
            FROM public.app_user
            WHERE id_user = @UserId
            FOR UPDATE;
            """,
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken));
        if (user == null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new UserDeletionResult(false, false, null, [], []);
        }

        var answeredSurveyNames = await GetSurveyNamesAsync(connection, transaction, userId, signed: false, cancellationToken);
        var signedSurveyNames = await GetSurveyNamesAsync(connection, transaction, userId, signed: true, cancellationToken);
        if (answeredSurveyNames.Count > 0 || signedSurveyNames.Count > 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new UserDeletionResult(true, false, user, answeredSurveyNames, signedSurveyNames);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.answer_draft_participant WHERE id_user = @UserId;",
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.app_user WHERE id_user = @UserId;",
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return new UserDeletionResult(true, affectedRows > 0, user, [], []);
    }

    private static async Task<IReadOnlyList<string>> GetSurveyNamesAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        int userId,
        bool signed,
        CancellationToken cancellationToken)
    {
        var surveyNames = await connection.QueryAsync<string>(new CommandDefinition(
            """
            WITH user_participation AS (
                SELECT participant.participation_type, assignment.id_survey
                FROM public.answer_participant participant
                INNER JOIN public.answer answer
                    ON answer.id_answer = participant.id_answer
                INNER JOIN public.organization_survey assignment
                    ON assignment.id_organization_survey = answer.id_organization_survey
                WHERE participant.id_user = @UserId
            )
            SELECT DISTINCT COALESCE(NULLIF(TRIM(s.name_survey), ''), 'Анкета #' || participation.id_survey::text) AS survey_name
            FROM user_participation participation
            LEFT JOIN public.survey s ON s.id_survey = participation.id_survey
            WHERE (@Signed AND participation.participation_type = 'signed')
               OR (NOT @Signed AND participation.participation_type <> 'signed')
            ORDER BY survey_name;
            """,
            new { UserId = userId, Signed = signed },
            transaction,
            cancellationToken: cancellationToken));
        return surveyNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetArchivePredicate(bool includeArchived) => includeArchived
        ? "u.date_end < @Today"
        : "(u.date_end IS NULL OR u.date_end >= @Today)";

    private static string BuildOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.Ordinal) ? "DESC" : "ASC";
        return sortBy switch
        {
            "organization" => $"COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, '') {direction}, u.id_user ASC",
            "role" => $"CASE LOWER(COALESCE(u.role, '')) WHEN 'admin' THEN 'Администратор' WHEN 'administrator' THEN 'Администратор' WHEN 'user' THEN 'Клиент' WHEN 'client' THEN 'Клиент' ELSE COALESCE(u.role, '') END {direction}, u.id_user ASC",
            "date_begin" => $"u.date_begin {direction} NULLS LAST, u.id_user ASC",
            "date_end" => $"u.date_end {direction} NULLS LAST, u.id_user ASC",
            _ => $"COALESCE(NULLIF(u.full_name, ''), u.login, '') {direction}, u.id_user ASC"
        };
    }

    private const string UserSelectSql = """
        SELECT
            u.id_user,
            u.full_name,
            u.login AS NameUser,
            u.email,
            COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, '') AS organization_name,
            COALESCE(u.id_organization, 0) AS OrganizationId,
            u.role AS NameRole,
            u.date_begin,
            u.date_end,
            u.password AS HashPassword
        FROM public.app_user u
        LEFT JOIN public.organization o ON u.id_organization = o.id_organization
        """;

    private static string NormalizeUserSortField(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            UserListSortFields.Organization => UserListSortFields.Organization,
            UserListSortFields.Role => UserListSortFields.Role,
            UserListSortFields.DateBegin => UserListSortFields.DateBegin,
            UserListSortFields.DateEnd => UserListSortFields.DateEnd,
            _ => UserListSortFields.Name
        };
    }

    private static string NormalizeUserSortDirection(string? sortDirection, string sortField)
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
            UserListSortFields.DateBegin => "desc",
            UserListSortFields.DateEnd => "desc",
            _ => "asc"
        };
    }

    private bool TryValidateUserCreateRequest(
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
            validationError = "Выберите организацию.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            validationError = "Введите логин.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            validationError = "Введите ФИО.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            validationError = "Выберите роль.";
            return false;
        }

        if (!AppRoles.IsSupported(normalizedRole))
        {
            validationError = $"Недопустимая роль. Допустимые значения: {string.Join(", ", AppRoles.SupportedRoles)}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.DateBegin))
        {
            validationError = "Укажите дату начала.";
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

        if (!TryValidateEndDateNotPast(dateEnd, out validationError))
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

    private bool TryValidateUserUpdateRequest(
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
            validationError = "Выберите организацию.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Введите логин.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Введите ФИО.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Выберите роль.";
            return false;
        }

        if (!AppRoles.IsSupported(normalizedRole))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = $"Недопустимая роль. Допустимые значения: {string.Join(", ", AppRoles.SupportedRoles)}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.DateBegin))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Укажите дату начала.";
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

        if (!TryValidateEndDateNotPast(dateEnd, out validationError))
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

    private bool TryValidateEndDateNotPast(DateTime? dateEnd, out string validationError)
    {
        if (dateEnd.HasValue && dateEnd.Value.Date < _clock.Today.Date)
        {
            validationError = "Дата конца не может быть раньше сегодняшней даты.";
            return false;
        }

        validationError = string.Empty;
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

    private static bool IsDuplicateLoginViolation(PostgresException exception)
    {
        return exception.SqlState == PostgresErrorCodes.UniqueViolation
            && (string.Equals(exception.ConstraintName, "app_user_login_key", StringComparison.OrdinalIgnoreCase)
                || string.Equals(exception.ConstraintName, "app_user_name_user_key", StringComparison.OrdinalIgnoreCase));
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
        builder.Append($"Нельзя удалить пользователя \"{userDisplayName}\": он связан с сохранёнными ответами анкет.");

        if (answeredSurveyNames.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Связанные анкеты: ");
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
}

internal sealed class UserDeleteCandidate
{
    public int IdUser { get; init; }
    public string? FullName { get; init; }
    public string? UserName { get; init; }
}

internal sealed record UserWriteModel(
    int OrganizationId,
    string Login,
    string FullName,
    string Role,
    string? PasswordHash,
    string? Email,
    DateTime? DateBegin,
    DateTime? DateEnd);

internal sealed record UserDeletionResult(
    bool Found,
    bool Deleted,
    UserDeleteCandidate? User,
    IReadOnlyList<string> AnsweredSurveyNames,
    IReadOnlyList<string> SignedSurveyNames);
