using System.Text;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Organization;
using MainProject.Domain.Entities;

namespace MainProject.Infrastructure.Persistence;

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrganizationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public int Count(bool includeArchived)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM public.organization o WHERE {GetArchivePredicate(includeArchived)};");
    }

    public IReadOnlyList<Organization> GetPage(bool includeArchived, string sortBy, string sortDirection, int pageSize, int offset)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.Query<Organization>(
            $"""
            {OrganizationSelectSql}
            WHERE {GetArchivePredicate(includeArchived)}
            ORDER BY {BuildOrderBy(sortBy, sortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """, new { PageSize = pageSize, Offset = offset }).ToList();
    }

    public IReadOnlyList<Organization> GetAll(bool includeArchived)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.Query<Organization>(
            $"""
            {OrganizationSelectSql}
            WHERE {GetArchivePredicate(includeArchived)}
            ORDER BY o.organization_name;
            """).ToList();
    }

    public IReadOnlyList<OrganizationDataResponse> GetActiveOptions()
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.Query<OrganizationDataResponse>(
            """
            SELECT id_organization AS Id, COALESCE(NULLIF(organization_short_name, ''), organization_name) AS Name
            FROM public.organization
            WHERE date_end IS NULL OR date_end >= CURRENT_DATE
            ORDER BY COALESCE(NULLIF(organization_short_name, ''), organization_name);
            """).ToList();
    }

    public Organization? GetById(int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.QueryFirstOrDefault<Organization>(
            """
            SELECT organization_name, organization_short_name, email, date_begin, date_end, id_organization AS OrganizationId
            FROM public.organization
            WHERE id_organization = @OrganizationId;
            """, new { OrganizationId = organizationId });
    }

    public int Create(OrganizationWriteModel organization)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.ExecuteScalar<int>(
            """
            INSERT INTO public.organization (organization_name, organization_short_name, email, date_begin, date_end)
            VALUES (@Name, @ShortName, @Email, @DateBegin, @DateEnd)
            RETURNING id_organization;
            """, organization);
    }

    public int Update(int organizationId, OrganizationWriteModel organization)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.Execute(
            """
            UPDATE public.organization
            SET organization_name = @Name, organization_short_name = @ShortName, email = @Email,
                date_begin = @DateBegin, date_end = @DateEnd
            WHERE id_organization = @OrganizationId;
            """, new { OrganizationId = organizationId, organization.Name, organization.ShortName, organization.Email, organization.DateBegin, organization.DateEnd });
    }

    public IReadOnlyList<string> GetAssignedSurveyNames(int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return GetAssignedSurveyNames(connection, organizationId);
    }

    private static IReadOnlyList<string> GetAssignedSurveyNames(
        System.Data.IDbConnection connection,
        int organizationId,
        System.Data.IDbTransaction? transaction = null)
    {
        return connection.Query<string>(
            """
            SELECT DISTINCT survey_name
            FROM (
                SELECT COALESCE(NULLIF(TRIM(s.name_survey), ''), 'Анкета #' || os.id_survey::text) AS survey_name
                FROM public.organization_survey os
                LEFT JOIN public.survey s ON s.id_survey = os.id_survey
                WHERE os.id_organization = @OrganizationId

                UNION

                SELECT COALESCE(NULLIF(TRIM(s.name_survey), ''), 'Анкета #' || os.id_survey::text) AS survey_name
                FROM public.answer a
                INNER JOIN public.organization_survey os ON os.id_organization_survey = a.id_organization_survey
                LEFT JOIN public.survey s ON s.id_survey = os.id_survey
                WHERE os.id_organization = @OrganizationId

                UNION

                SELECT COALESCE(
                    NULLIF(TRIM(s.name_survey), ''),
                    CASE WHEN audit_row.survey_id IS NOT NULL THEN 'Анкета #' || audit_row.survey_id::text END
                ) AS survey_name
                FROM (
                    SELECT DISTINCT
                        COALESCE(audit_raw.id_organization, os.id_organization) AS id_organization,
                        COALESCE(audit_raw.survey_id, os.id_survey) AS survey_id
                    FROM (
                        SELECT id_organization, id_survey AS survey_id, id_organization_survey
                        FROM public.organization_survey_l
                    ) audit_raw
                    LEFT JOIN public.organization_survey os
                        ON os.id_organization_survey = audit_raw.id_organization_survey
                ) audit_row
                LEFT JOIN public.survey s ON s.id_survey = audit_row.survey_id
                WHERE audit_row.id_organization = @OrganizationId
            ) assigned_surveys
            WHERE survey_name IS NOT NULL AND BTRIM(survey_name) <> ''
            ORDER BY survey_name;
            """, new { OrganizationId = organizationId }, transaction)
            .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<string> GetAssignedUserNames(int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return GetAssignedUserNames(connection, organizationId);
    }

    private static IReadOnlyList<string> GetAssignedUserNames(
        System.Data.IDbConnection connection,
        int organizationId,
        System.Data.IDbTransaction? transaction = null)
    {
        return connection.Query<string>(
            """
            SELECT DISTINCT user_name
            FROM (
                SELECT COALESCE(NULLIF(TRIM(u.full_name), ''), NULLIF(TRIM(u.login), ''), 'Пользователь #' || u.id_user::text) AS user_name
                FROM public.app_user u
                WHERE u.id_organization = @OrganizationId

                UNION

                SELECT COALESCE(
                    NULLIF(TRIM(u.full_name), ''),
                    NULLIF(TRIM(u.login), ''),
                    NULLIF(TRIM(audit_row.full_name), ''),
                    NULLIF(TRIM(audit_row.user_name), ''),
                    CASE WHEN audit_row.user_id IS NOT NULL THEN 'Пользователь #' || audit_row.user_id::text END
                ) AS user_name
                FROM (
                    SELECT DISTINCT id_user AS user_id, id_organization, full_name, login AS user_name
                    FROM public.app_user_l
                ) audit_row
                LEFT JOIN public.app_user u ON u.id_user = audit_row.user_id
                WHERE audit_row.id_organization = @OrganizationId
            ) assigned_users
            WHERE user_name IS NOT NULL AND BTRIM(user_name) <> ''
            ORDER BY user_name;
            """, new { OrganizationId = organizationId }, transaction)
            .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public OrganizationArchiveResult ArchiveIfUnused(int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();
        var exists = connection.ExecuteScalar<int?>(
            "SELECT id_organization FROM public.organization WHERE id_organization = @OrganizationId FOR UPDATE;",
            new { OrganizationId = organizationId },
            transaction);
        if (!exists.HasValue)
        {
            transaction.Commit();
            return new OrganizationArchiveResult(false, false, [], []);
        }

        var surveyNames = GetAssignedSurveyNames(connection, organizationId, transaction);
        var userNames = GetAssignedUserNames(connection, organizationId, transaction);
        if (surveyNames.Count > 0 || userNames.Count > 0)
        {
            transaction.Commit();
            return new OrganizationArchiveResult(true, false, surveyNames, userNames);
        }

        var affectedRows = connection.Execute(
            """
            UPDATE public.organization
            SET date_end = CASE WHEN date_end IS NULL OR date_end >= CURRENT_DATE
                THEN (CURRENT_DATE - INTERVAL '1 day')::date ELSE date_end END
            WHERE id_organization = @OrganizationId;
            """, new { OrganizationId = organizationId }, transaction);
        transaction.Commit();
        return new OrganizationArchiveResult(true, affectedRows > 0, [], []);
    }

    public IReadOnlyList<OrganizationSurveyAssignmentRecord> GetLatestUnansweredAssignments(IReadOnlyCollection<int>? organizationIds = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = new StringBuilder(
            """
            WITH latest_assignment AS (
                SELECT * FROM (
                    SELECT os.id_organization, os.id_survey, os.id_organization_survey, os.date_end, s.name_survey,
                        ROW_NUMBER() OVER (
                            PARTITION BY os.id_organization, LOWER(BTRIM(s.name_survey))
                            ORDER BY os.date_begin DESC, os.id_survey DESC, os.id_organization_survey DESC) AS assignment_rank
                    FROM public.organization_survey os
                    INNER JOIN public.survey s ON s.id_survey = os.id_survey
                ) ranked
                WHERE assignment_rank = 1
                  AND NOT EXISTS (SELECT 1 FROM public.answer a WHERE a.id_organization_survey = ranked.id_organization_survey)
            )
            SELECT o.id_organization AS OrganizationId,
                   COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS OrganizationName,
                   latest_assignment.id_survey AS SurveyId, latest_assignment.name_survey AS SurveyName,
                   latest_assignment.date_end AS AssignmentDateEnd
            FROM public.organization o
            LEFT JOIN latest_assignment ON latest_assignment.id_organization = o.id_organization
            WHERE o.date_end IS NULL OR o.date_end >= CURRENT_DATE
            """);
        if (organizationIds is { Count: > 0 })
        {
            sql.Append(" AND o.id_organization = ANY(@OrganizationIds)");
        }

        sql.Append(" ORDER BY COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name), latest_assignment.name_survey NULLS LAST;");
        return connection.Query<OrganizationSurveyAssignmentRecord>(sql.ToString(), new { OrganizationIds = organizationIds?.ToArray() ?? [] }).ToList();
    }

    public bool UpdateAssignmentEndDates(IReadOnlyCollection<(int OrganizationId, int SurveyId)> assignments, DateTime dateEnd)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var assignment in assignments)
            {
                var affected = connection.Execute(
                    "UPDATE public.organization_survey SET date_end = @DateEnd WHERE id_organization = @OrganizationId AND id_survey = @SurveyId;",
                    new { DateEnd = dateEnd.Date, assignment.OrganizationId, assignment.SurveyId }, transaction);
                if (affected == 0)
                {
                    transaction.Rollback();
                    return false;
                }
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static string GetArchivePredicate(bool includeArchived) => includeArchived
        ? "o.date_end < CURRENT_DATE"
        : "(o.date_end IS NULL OR o.date_end >= CURRENT_DATE)";

    private static string BuildOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.Ordinal) ? "DESC" : "ASC";
        return sortBy switch
        {
            "date_begin" => $"o.date_begin {direction} NULLS LAST, o.id_organization ASC",
            "date_end" => $"o.date_end {direction} NULLS LAST, o.id_organization ASC",
            _ => $"o.organization_name {direction}, o.id_organization ASC"
        };
    }

    private const string OrganizationSelectSql = """
        SELECT o.id_organization AS OrganizationId, o.organization_name, o.organization_short_name, o.date_begin, o.date_end,
               COALESCE((SELECT string_agg(s.name_survey, ', ' ORDER BY s.name_survey)
                         FROM public.organization_survey os
                         INNER JOIN public.survey s ON s.id_survey = os.id_survey
                         WHERE os.id_organization = o.id_organization), 'Не указано') AS survey_names,
               o.email
        FROM public.organization o
        """;
}
