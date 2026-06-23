using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using Npgsql;

namespace MainProject.Infrastructure.Persistence;

public sealed class SurveyAssignmentRepository : ISurveyAssignmentRepository
{
    public IReadOnlyList<Survey> GetActiveSurveySummaries(NpgsqlConnection connection)
    {
        return connection.Query<Survey>(
            """
            SELECT
                survey.id_survey AS IdSurvey,
                survey.name_survey AS NameSurvey,
                schedule.date_begin AS DateBegin,
                schedule.date_end AS DateEnd,
                COALESCE(
                    (
                        SELECT string_agg(
                            COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name),
                            ', '
                        )
                        FROM public.organization_survey assignment
                        INNER JOIN public.organization organization
                            ON organization.id_organization = assignment.id_organization
                        WHERE assignment.id_survey = survey.id_survey
                    ),
                    'Не указано'
                ) AS OrganizationName
            FROM public.survey survey
            LEFT JOIN public.survey_schedule schedule
                ON schedule.id_survey = survey.id_survey
            WHERE EXISTS (
                SELECT 1
                FROM public.organization_survey assignment
                WHERE assignment.id_survey = survey.id_survey
                  AND (assignment.date_end IS NULL OR assignment.date_end >= CURRENT_DATE)
            )
            ORDER BY survey.id_survey DESC;
            """).ToList();
    }

    public Survey? GetSurveyWithSchedule(NpgsqlConnection connection, int surveyId)
    {
        return connection.QueryFirstOrDefault<Survey>(
            """
            SELECT
                survey.id_survey AS IdSurvey,
                survey.name_survey AS NameSurvey,
                COALESCE(schedule.date_begin, CURRENT_DATE) AS DateBegin,
                schedule.date_end AS DateEnd,
                survey.description AS Description
            FROM public.survey survey
            LEFT JOIN public.survey_schedule schedule
                ON schedule.id_survey = survey.id_survey
            WHERE survey.id_survey = @SurveyId;
            """,
            new { SurveyId = surveyId });
    }

    public IReadOnlyList<OrganizationSelectionItem> GetAvailableOrganizationsForSurvey(
        NpgsqlConnection connection,
        int surveyId)
    {
        return connection.Query<OrganizationSelectionItem>(
            """
            SELECT
                organization.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS Name
            FROM public.organization organization
            WHERE organization.date_end IS NULL
               OR organization.date_end >= CURRENT_DATE
               OR organization.id_organization IN (
                    SELECT id_organization
                    FROM public.organization_survey
                    WHERE id_survey = @SurveyId
               )
            ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name);
            """,
            new { SurveyId = surveyId }).ToList();
    }

    public IReadOnlyList<OrganizationSelectionItem> GetSelectedOrganizationsForSurvey(
        NpgsqlConnection connection,
        int surveyId)
    {
        return connection.Query<OrganizationSelectionItem>(
            """
            SELECT
                organization.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS Name
            FROM public.organization_survey assignment
            INNER JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            WHERE assignment.id_survey = @SurveyId
            ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name);
            """,
            new { SurveyId = surveyId }).ToList();
    }

    public int UpdateActiveSurveyPeriod(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTime dateBegin,
        DateTime dateEnd)
    {
        return connection.ExecuteScalar<int>(
            """
            WITH active_survey AS (
                SELECT DISTINCT id_survey
                FROM public.organization_survey
                WHERE date_end IS NULL OR date_end >= CURRENT_DATE
            ),
            updated AS (
                UPDATE public.organization_survey assignment
                SET
                    date_begin = @DateBegin,
                    date_end = @DateEnd
                FROM active_survey active
                WHERE assignment.id_survey = active.id_survey
                RETURNING assignment.id_survey
            )
            SELECT COUNT(DISTINCT id_survey)
            FROM updated;
            """,
            new
            {
                DateBegin = dateBegin.Date,
                DateEnd = dateEnd.Date
            },
            transaction);
    }

    public IReadOnlyList<ArchivedSurvey> GetAdminArchivedSurveySummaries(NpgsqlConnection connection)
    {
        return connection.Query<ArchivedSurvey>(
            """
            SELECT
                survey.id_survey AS IdSurvey,
                schedule.date_begin AS DateBegin,
                schedule.date_end AS DateEnd,
                survey.name_survey AS NameSurvey,
                COALESCE(
                    (
                        SELECT string_agg(
                            COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name),
                            ', '
                            ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name)
                        )
                        FROM public.organization_survey assignment
                        INNER JOIN public.organization organization
                            ON organization.id_organization = assignment.id_organization
                        WHERE assignment.id_survey = survey.id_survey
                    ),
                    'Не указано'
                ) AS OrganizationName,
                survey.description AS Description
            FROM public.survey survey
            LEFT JOIN public.survey_schedule schedule
                ON schedule.id_survey = survey.id_survey
            WHERE EXISTS (
                    SELECT 1
                    FROM public.organization_survey assignment
                    WHERE assignment.id_survey = survey.id_survey
                )
              AND EXISTS (
                    SELECT 1
                    FROM public.answer answer
                    INNER JOIN public.organization_survey answered_assignment
                        ON answered_assignment.id_organization_survey = answer.id_organization_survey
                    WHERE answered_assignment.id_survey = survey.id_survey
                )
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.organization_survey active_assignment
                    WHERE active_assignment.id_survey = survey.id_survey
                      AND (active_assignment.date_end IS NULL OR active_assignment.date_end >= CURRENT_DATE)
                )
            ORDER BY survey.id_survey DESC;
            """).ToList();
    }

    public async Task<ArchivedSurvey?> GetArchivedSurveyForCopyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        return await connection.QueryFirstOrDefaultAsync<ArchivedSurvey>(
            new CommandDefinition(
                """
                SELECT
                    survey.id_survey AS IdSurvey,
                    schedule.date_begin AS DateBegin,
                    schedule.date_end AS DateEnd,
                    survey.name_survey AS NameSurvey,
                    survey.description AS Description
                FROM public.survey survey
                LEFT JOIN public.survey_schedule schedule
                    ON schedule.id_survey = survey.id_survey
                WHERE survey.id_survey = @SurveyId
                  AND EXISTS (
                      SELECT 1
                      FROM public.organization_survey assignment
                      WHERE assignment.id_survey = survey.id_survey
                  )
                  AND EXISTS (
                      SELECT 1
                      FROM public.answer answer
                      INNER JOIN public.organization_survey answered_assignment
                          ON answered_assignment.id_organization_survey = answer.id_organization_survey
                      WHERE answered_assignment.id_survey = survey.id_survey
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM public.organization_survey active_assignment
                      WHERE active_assignment.id_survey = survey.id_survey
                        AND (active_assignment.date_end IS NULL OR active_assignment.date_end >= CURRENT_DATE)
                  );
                """,
                new { SurveyId = surveyId },
                transaction,
                cancellationToken: cancellationToken));
    }

    public int CountActiveSurveys(NpgsqlConnection connection, IReadOnlyCollection<int> organizationIds)
    {
        var parameters = new
        {
            OrganizationIds = organizationIds.ToArray(),
            HasOrganizationFilter = organizationIds.Count > 0
        };

        return connection.ExecuteScalar<int>(
            $"{ActiveSurveyRowsCte} SELECT COUNT(*) FROM survey_rows WHERE {ActiveSurveyFilterPredicate};",
            parameters);
    }

    public IReadOnlyList<SurveyAssignmentTableRow> GetActiveSurveyPage(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset)
    {
        var parameters = new
        {
            OrganizationIds = organizationIds.ToArray(),
            HasOrganizationFilter = organizationIds.Count > 0,
            PageSize = pageSize,
            Offset = offset
        };

        return connection.Query<SurveyAssignmentTableRow>(
            $"""
            {ActiveSurveyRowsCte}
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                organization_ids AS OrganizationIds,
                organization_names AS OrganizationNames
            FROM survey_rows
            WHERE {ActiveSurveyFilterPredicate}
            ORDER BY {BuildOrderBy(sortBy, sortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            parameters).ToArray();
    }

    public IReadOnlyList<SelectionOption> GetActiveOrganizationOptions(NpgsqlConnection connection)
    {
        return connection.Query<SelectionOption>(
            """
            SELECT DISTINCT
                o.id_organization AS Id,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS Name
            FROM public.organization_survey os
            INNER JOIN public.organization o
                ON o.id_organization = os.id_organization
            WHERE EXISTS (
                SELECT 1
                FROM public.organization_survey active_assignment
                WHERE active_assignment.id_survey = os.id_survey
                  AND (active_assignment.date_end IS NULL OR active_assignment.date_end >= CURRENT_DATE)
            );
            """).ToArray();
    }

    public int CountArchivedSurveys(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> surveyIds,
        DateTime? dateStart,
        DateTime? dateEnd)
    {
        var parameters = BuildArchivedParameters(organizationIds, surveyIds, dateStart, dateEnd);
        return connection.ExecuteScalar<int>(
            $"{ArchivedSurveyRowsCte} SELECT COUNT(*) FROM survey_rows WHERE {ArchivedSurveyFilterPredicate};",
            parameters);
    }

    public IReadOnlyList<SurveyAssignmentTableRow> GetArchivedSurveyPage(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> surveyIds,
        DateTime? dateStart,
        DateTime? dateEnd,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset)
    {
        var parameters = BuildArchivedParameters(organizationIds, surveyIds, dateStart, dateEnd);
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);

        return connection.Query<SurveyAssignmentTableRow>(
            $"""
            {ArchivedSurveyRowsCte}
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                organization_ids AS OrganizationIds,
                organization_names AS OrganizationNames
            FROM survey_rows
            WHERE {ArchivedSurveyFilterPredicate}
            ORDER BY {BuildOrderBy(sortBy, sortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            parameters).ToArray();
    }

    public IReadOnlyList<SelectionOption> GetArchivedOrganizationOptions(NpgsqlConnection connection)
    {
        return connection.Query<SelectionOption>(
            """
            SELECT DISTINCT
                o.id_organization AS Id,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS Name
            FROM public.organization_survey os
            INNER JOIN public.organization o
                ON o.id_organization = os.id_organization
            WHERE EXISTS (
                    SELECT 1
                    FROM public.organization_survey existing_assignment
                    WHERE existing_assignment.id_survey = os.id_survey
                )
              AND EXISTS (
                    SELECT 1
                    FROM public.answer answer
                    INNER JOIN public.organization_survey answered_assignment
                        ON answered_assignment.id_organization_survey = answer.id_organization_survey
                    WHERE answered_assignment.id_survey = os.id_survey
                )
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.organization_survey active_assignment
                    WHERE active_assignment.id_survey = os.id_survey
                      AND (active_assignment.date_end IS NULL OR active_assignment.date_end >= CURRENT_DATE)
                );
            """).ToArray();
    }

    public IReadOnlyList<SelectionOption> GetArchivedSurveyOptions(NpgsqlConnection connection)
    {
        return connection.Query<SelectionOption>(
            $"""
            {ArchivedSurveyRowsCte}
            SELECT
                id_survey AS Id,
                name_survey AS Name
            FROM survey_rows;
            """).ToArray();
    }

    public int? GetUserOrganizationId(NpgsqlConnection connection, int userId)
    {
        return connection.ExecuteScalar<int?>(
            "SELECT id_organization FROM public.app_user WHERE id_user = @UserId;",
            new { UserId = userId });
    }

    public bool IsActiveAssignment(NpgsqlConnection connection, int surveyId, int organizationId)
    {
        return connection.ExecuteScalar<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.organization_survey assignment
                WHERE assignment.id_survey = @SurveyId
                  AND assignment.id_organization = @OrganizationId
                  AND assignment.date_begin <= CURRENT_DATE
                  AND (assignment.date_end IS NULL OR assignment.date_end >= CURRENT_DATE)
            );
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId
            });
    }

    public UserSurveyAssignmentPageData GetActiveUserSurveyPage(
        NpgsqlConnection connection,
        int organizationId,
        string searchTerm,
        int pageSize,
        int offset)
    {
        var parameters = new DynamicParameters();
        parameters.Add("OrganizationId", organizationId);
        parameters.Add("HasSearch", !string.IsNullOrWhiteSpace(searchTerm));
        parameters.Add("SearchPattern", $"%{searchTerm}%");
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);

        var totalCount = connection.ExecuteScalar<int>(
            $"SELECT COUNT(*) {ActiveUserSurveyBaseSql}",
            parameters);
        var surveys = connection.Query<Survey>(
            $"""
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                description AS Description,
                date_begin AS DateBegin,
                date_end AS DateEnd
            {ActiveUserSurveyBaseSql}
            ORDER BY id_survey DESC
            OFFSET @Offset
            LIMIT @PageSize;
            """,
            parameters).ToList();

        return new UserSurveyAssignmentPageData
        {
            TotalCount = totalCount,
            Surveys = surveys
        };
    }

    public UserSurveyAssignmentPageData GetUserArchivePage(
        NpgsqlConnection connection,
        int organizationId,
        string searchTerm,
        DateTime? exactCompletionDate,
        DateTime? completionDateFrom,
        DateTime? completionDateTo,
        bool signedOnly,
        int pageSize,
        int offset)
    {
        var filters = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("OrganizationId", organizationId);
        parameters.Add("SearchPattern", string.IsNullOrWhiteSpace(searchTerm) ? null : $"%{searchTerm}%");
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filters.Add("archived.name_survey ILIKE @SearchPattern");
        }

        if (exactCompletionDate.HasValue)
        {
            filters.Add("archived.completion_date::date = @ExactCompletionDate");
            parameters.Add("ExactCompletionDate", exactCompletionDate.Value.Date);
        }
        else
        {
            if (completionDateFrom.HasValue)
            {
                filters.Add("archived.completion_date >= @CompletionDateFrom");
                parameters.Add("CompletionDateFrom", completionDateFrom.Value);
            }

            if (completionDateTo.HasValue)
            {
                filters.Add("archived.completion_date <= @CompletionDateTo");
                parameters.Add("CompletionDateTo", completionDateTo.Value);
            }
        }

        if (signedOnly)
        {
            filters.Add("COALESCE(archived.csp, '') <> ''");
        }

        var whereClause = filters.Count == 0
            ? string.Empty
            : "WHERE " + string.Join(" AND ", filters);
        var totalCount = connection.ExecuteScalar<int>(
            $"SELECT COUNT(*) {UserArchiveBaseSql} {whereClause}",
            parameters);
        var surveys = connection.Query<Survey>(
            $"""
            SELECT
                archived.id_survey AS IdSurvey,
                archived.name_survey AS NameSurvey,
                archived.description AS Description,
                archived.date_begin AS DateBegin,
                archived.date_end AS DateEnd,
                archived.completion_date AS CompletionDate,
                archived.csp AS Csp,
                archived.id_organization AS OrganizationId
            {UserArchiveBaseSql}
            {whereClause}
            ORDER BY archived.completion_date DESC
            OFFSET @Offset
            LIMIT @PageSize;
            """,
            parameters).ToList();

        return new UserSurveyAssignmentPageData
        {
            TotalCount = totalCount,
            Surveys = surveys
        };
    }

    public async Task ReplaceSurveyAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IEnumerable<int> organizationIds,
        DateTime dateBegin,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default)
    {
        var normalizedOrganizationIds = NormalizeOrganizationIds(organizationIds);

        if (normalizedOrganizationIds.Length == 0)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM public.organization_survey WHERE id_survey = @SurveyId;",
                    new { SurveyId = surveyId },
                    transaction,
                    cancellationToken: cancellationToken));
            return;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM public.organization_survey
                WHERE id_survey = @SurveyId
                  AND NOT (id_organization = ANY(@OrganizationIds));
                """,
                new
                {
                    SurveyId = surveyId,
                    OrganizationIds = normalizedOrganizationIds
                },
                transaction,
                cancellationToken: cancellationToken));

        await UpsertSurveyAssignmentsAsync(
            connection,
            transaction,
            surveyId,
            normalizedOrganizationIds,
            dateBegin,
            dateEnd,
            cancellationToken);
    }

    public async Task UpsertSurveyAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IEnumerable<int> organizationIds,
        DateTime dateBegin,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default)
    {
        foreach (var organizationId in NormalizeOrganizationIds(organizationIds))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO public.organization_survey (id_organization, id_survey, date_begin, date_end)
                    VALUES (@OrganizationId, @SurveyId, @DateBegin, @DateEnd)
                    ON CONFLICT (id_organization, id_survey) DO UPDATE
                    SET
                        date_begin = EXCLUDED.date_begin,
                        date_end = EXCLUDED.date_end;
                    """,
                    new
                    {
                        OrganizationId = organizationId,
                        SurveyId = surveyId,
                        DateBegin = dateBegin.Date,
                        DateEnd = dateEnd?.Date
                    },
                    transaction,
                    cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<int>> GetOrganizationIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var organizationIds = await connection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT id_organization
                FROM public.organization_survey
                WHERE id_survey = @SurveyId
                ORDER BY id_organization;
                """,
                new { SurveyId = surveyId },
                transaction,
                cancellationToken: cancellationToken));

        return organizationIds.ToArray();
    }

    public async Task<bool> HasSurveyWithScheduleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string surveyName,
        DateTime dateBegin,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default)
    {
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM public.survey survey_copy
                    WHERE survey_copy.name_survey = @SurveyName
                      AND (
                          EXISTS (
                              SELECT 1
                              FROM public.organization_survey assignment
                              WHERE assignment.id_survey = survey_copy.id_survey
                                AND assignment.date_begin = @DateBegin
                                AND assignment.date_end IS NOT DISTINCT FROM @DateEnd::date
                          )
                          OR NOT EXISTS (
                              SELECT 1
                              FROM public.organization_survey assignment
                              WHERE assignment.id_survey = survey_copy.id_survey
                          )
                      )
                );
                """,
                new
                {
                    SurveyName = surveyName,
                    DateBegin = dateBegin.Date,
                    DateEnd = dateEnd?.Date
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    public int UpsertSurveyEndDate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId,
        DateTime dateEnd)
    {
        return connection.Execute(
            """
            INSERT INTO public.organization_survey (
                id_organization,
                id_survey,
                date_end
            )
            SELECT
                @OrganizationId,
                survey.id_survey,
                @DateEnd::date
            FROM public.survey survey
            WHERE survey.id_survey = @SurveyId
            ON CONFLICT (id_organization, id_survey) DO UPDATE
            SET date_end = EXCLUDED.date_end;
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId,
                DateEnd = dateEnd.Date
            },
            transaction);
    }

    public int? GetAssignmentIdForUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId)
    {
        return connection.ExecuteScalar<int?>(
            """
            SELECT id_organization_survey
            FROM public.organization_survey
            WHERE id_organization = @OrganizationId
              AND id_survey = @SurveyId
            FOR UPDATE;
            """,
            new
            {
                OrganizationId = organizationId,
                SurveyId = surveyId
            },
            transaction);
    }

    private static int[] NormalizeOrganizationIds(IEnumerable<int> organizationIds)
        => organizationIds
            .Where(static id => id > 0)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();

    private static DynamicParameters BuildArchivedParameters(
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> surveyIds,
        DateTime? dateStart,
        DateTime? dateEnd)
    {
        var parameters = new DynamicParameters();
        parameters.Add("OrganizationIds", organizationIds.ToArray());
        parameters.Add("HasOrganizationFilter", organizationIds.Count > 0);
        parameters.Add("SurveyIds", surveyIds.ToArray());
        parameters.Add("HasSurveyFilter", surveyIds.Count > 0);
        parameters.Add("HasDateFilter", dateStart.HasValue && dateEnd.HasValue);
        parameters.Add("DateStart", dateStart);
        parameters.Add("DateEnd", dateEnd);
        return parameters;
    }

    private static string BuildOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            ? "DESC"
            : "ASC";

        return sortBy switch
        {
            "name" => $"name_survey {direction}, id_survey DESC",
            "dateBegin" => $"date_begin {direction} NULLS LAST, id_survey DESC",
            "dateEnd" => $"date_end {direction} NULLS LAST, id_survey DESC",
            _ => "id_survey DESC"
        };
    }

    private const string ActiveSurveyFilterPredicate =
        "(@HasOrganizationFilter = false OR organization_ids && @OrganizationIds)";

    private const string ArchivedSurveyFilterPredicate = """
        (@HasOrganizationFilter = false OR organization_ids && @OrganizationIds)
        AND (@HasSurveyFilter = false OR id_survey = ANY(@SurveyIds))
        AND (
            @HasDateFilter = false
            OR (
                date_end IS NOT NULL
                AND date_begin::date >= @DateStart
                AND date_begin::date <= @DateEnd
                AND date_end::date >= @DateStart
                AND date_end::date <= @DateEnd
            )
        )
        """;

    private const string ActiveSurveyRowsCte = """
        WITH survey_rows AS (
            SELECT
                s.id_survey,
                s.name_survey,
                ss.date_begin,
                ss.date_end,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT os2.id_organization
                        FROM public.organization_survey os2
                        WHERE os2.id_survey = s.id_survey
                          AND os2.id_organization IS NOT NULL
                        ORDER BY os2.id_organization
                    ),
                    ARRAY[]::integer[]
                ) AS organization_ids,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT COALESCE(NULLIF(o2.organization_short_name, ''), o2.organization_name)
                        FROM public.organization_survey os2
                        INNER JOIN public.organization o2
                            ON o2.id_organization = os2.id_organization
                        WHERE os2.id_survey = s.id_survey
                          AND COALESCE(NULLIF(o2.organization_short_name, ''), o2.organization_name) IS NOT NULL
                        ORDER BY COALESCE(NULLIF(o2.organization_short_name, ''), o2.organization_name)
                    ),
                    ARRAY[]::text[]
                ) AS organization_names
            FROM public.survey s
            LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
            WHERE EXISTS (
                SELECT 1
                FROM public.organization_survey active_assignment
                WHERE active_assignment.id_survey = s.id_survey
                  AND (active_assignment.date_end IS NULL OR active_assignment.date_end >= CURRENT_DATE)
            )
        )
        """;

    private const string ArchivedSurveyRowsCte = """
        WITH survey_rows AS (
            SELECT
                s.id_survey,
                s.name_survey,
                ss.date_begin,
                ss.date_end,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT os2.id_organization
                        FROM public.organization_survey os2
                        WHERE os2.id_survey = s.id_survey
                          AND os2.id_organization IS NOT NULL
                        ORDER BY os2.id_organization
                    ),
                    ARRAY[]::integer[]
                ) AS organization_ids,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT COALESCE(NULLIF(o2.organization_short_name, ''), o2.organization_name)
                        FROM public.organization_survey os2
                        INNER JOIN public.organization o2
                            ON o2.id_organization = os2.id_organization
                        WHERE os2.id_survey = s.id_survey
                          AND COALESCE(NULLIF(o2.organization_short_name, ''), o2.organization_name) IS NOT NULL
                        ORDER BY COALESCE(NULLIF(o2.organization_short_name, ''), o2.organization_name)
                    ),
                    ARRAY[]::text[]
                ) AS organization_names
            FROM public.survey s
            LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
            WHERE EXISTS (
                    SELECT 1
                    FROM public.organization_survey existing_assignment
                    WHERE existing_assignment.id_survey = s.id_survey
                )
              AND EXISTS (
                    SELECT 1
                    FROM public.answer answer
                    INNER JOIN public.organization_survey answered_assignment
                        ON answered_assignment.id_organization_survey = answer.id_organization_survey
                    WHERE answered_assignment.id_survey = s.id_survey
                )
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.organization_survey active_assignment
                    WHERE active_assignment.id_survey = s.id_survey
                      AND (active_assignment.date_end IS NULL OR active_assignment.date_end >= CURRENT_DATE)
                )
        )
        """;

    private const string ActiveUserSurveyBaseSql = """
        FROM (
            SELECT
                s.id_survey,
                s.name_survey,
                s.description,
                assignment.date_begin,
                assignment.date_end
            FROM public.survey s
            INNER JOIN public.organization_survey assignment
                ON assignment.id_survey = s.id_survey
            WHERE assignment.id_organization = @OrganizationId
              AND assignment.date_begin <= CURRENT_DATE
              AND (assignment.date_end IS NULL OR assignment.date_end >= CURRENT_DATE)
              AND NOT EXISTS (
                  SELECT 1
                  FROM public.answer answer
                  INNER JOIN public.organization_survey answered_assignment
                      ON answered_assignment.id_organization_survey = answer.id_organization_survey
                  WHERE answered_assignment.id_organization = @OrganizationId
                    AND answered_assignment.id_survey = s.id_survey
              )
        ) AS accessible
        WHERE (@HasSearch = FALSE OR accessible.name_survey ILIKE @SearchPattern)
        """;

    private const string UserArchiveBaseSql = """
        FROM (
            SELECT
                s.id_survey,
                s.name_survey,
                s.description,
                COALESCE(assignment.date_begin, schedule.date_begin) AS date_begin,
                COALESCE(assignment.date_end, schedule.date_end) AS date_end,
                answer.completion_date,
                answer.csp,
                assignment.id_organization
            FROM public.survey s
            INNER JOIN public.organization_survey assignment
                ON assignment.id_survey = s.id_survey
            INNER JOIN public.answer answer
                ON answer.id_organization_survey = assignment.id_organization_survey
            LEFT JOIN public.survey_schedule schedule
                ON schedule.id_survey = s.id_survey
            WHERE assignment.id_organization = @OrganizationId
        ) AS archived
        """;
}
