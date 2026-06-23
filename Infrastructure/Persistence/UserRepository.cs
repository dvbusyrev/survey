using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.User;
using MainProject.Domain.Entities;

namespace MainProject.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public int Count(bool includeArchived)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM public.app_user u WHERE {GetArchivePredicate(includeArchived)};");
    }

    public IReadOnlyList<User> GetPage(bool includeArchived, string sortBy, string sortDirection, int pageSize, int offset)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.Query<User>(
            $"""
            {UserSelectSql}
            WHERE {GetArchivePredicate(includeArchived)}
            ORDER BY {BuildOrderBy(sortBy, sortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { PageSize = pageSize, Offset = offset }).ToList();
    }

    public IReadOnlyList<User> GetAll(bool includeArchived)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.Query<User>(
            $"""
            {UserSelectSql}
            WHERE {GetArchivePredicate(includeArchived)}
            ORDER BY COALESCE(u.full_name, u.login), u.id_user;
            """).ToList();
    }

    public User? GetById(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.QueryFirstOrDefault<User>(
            $"""
            {UserSelectSql}
            WHERE u.id_user = @UserId;
            """,
            new { UserId = userId });
    }

    public IReadOnlyList<SelectionOption> GetActiveOrganizationOptions()
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.Query<SelectionOption>(
            """
            SELECT id_organization AS Id, COALESCE(NULLIF(organization_short_name, ''), organization_name) AS Name
            FROM public.organization
            WHERE date_end IS NULL OR date_end >= CURRENT_DATE
            ORDER BY COALESCE(NULLIF(organization_short_name, ''), organization_name);
            """).ToList();
    }

    public int Create(UserWriteModel user)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.Execute(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, email, date_begin, date_end)
            VALUES (@OrganizationId, @Login, @FullName, @Role, @PasswordHash, @Email, @DateBegin, @DateEnd);
            """, user);
    }

    public int Update(int userId, UserWriteModel user)
    {
        using var connection = _connectionFactory.CreateConnection();
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
        return connection.Execute(sql, new { UserId = userId, user.OrganizationId, user.Login, user.FullName, user.Role, user.Email, user.DateBegin, user.DateEnd, user.PasswordHash });
    }

    public UserDeletionResult DeleteIfAllowed(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();
        var user = connection.QueryFirstOrDefault<UserDeleteCandidate>(
            """
            SELECT id_user AS IdUser, full_name AS FullName, login AS UserName
            FROM public.app_user
            WHERE id_user = @UserId
            FOR UPDATE;
            """,
            new { UserId = userId },
            transaction);
        if (user == null)
        {
            transaction.Commit();
            return new UserDeletionResult(false, false, null, [], []);
        }

        var answeredSurveyNames = GetSurveyNames(connection, transaction, userId, "=");
        var signedSurveyNames = GetSurveyNames(connection, transaction, userId, "<>");
        if (answeredSurveyNames.Count > 0 || signedSurveyNames.Count > 0)
        {
            transaction.Commit();
            return new UserDeletionResult(true, false, user, answeredSurveyNames, signedSurveyNames);
        }

        var affectedRows = connection.Execute(
            "DELETE FROM public.app_user WHERE id_user = @UserId;",
            new { UserId = userId },
            transaction);
        transaction.Commit();
        return new UserDeletionResult(true, affectedRows > 0, user, [], []);
    }

    private static IReadOnlyList<string> GetSurveyNames(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        int userId,
        string signatureOperator)
    {
        return connection.Query<string>(
            $"""
            SELECT DISTINCT COALESCE(NULLIF(TRIM(s.name_survey), ''), 'Анкета #' || audit_row.SurveyId::text) AS survey_name
            FROM (
                SELECT audit_raw.changed_by_user_id, COALESCE(audit_raw.SurveyId, os.id_survey) AS SurveyId, audit_raw.SignatureValue
                FROM (
                    SELECT changed_by_user_id, NULL::integer AS SurveyId, id_organization_survey AS IdOrganizationSurvey,
                           COALESCE(csp, '') AS SignatureValue
                    FROM public.answer_l
                ) audit_raw
                LEFT JOIN public.organization_survey os ON os.id_organization_survey = audit_raw.IdOrganizationSurvey
            ) audit_row
            LEFT JOIN public.survey s ON s.id_survey = audit_row.SurveyId
            WHERE audit_row.changed_by_user_id = @UserId
              AND audit_row.SurveyId IS NOT NULL
              AND audit_row.SignatureValue {signatureOperator} ''
            ORDER BY survey_name;
            """,
            new { UserId = userId }, transaction)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetArchivePredicate(bool includeArchived) => includeArchived
        ? "u.date_end < CURRENT_DATE"
        : "(u.date_end IS NULL OR u.date_end >= CURRENT_DATE)";

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
}
