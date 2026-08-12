using System.Data;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Configuration;
using MainProject.Application.UseCases.Answers;
using MainProject.Domain.Entities;
using Npgsql;

namespace MainProject.Infrastructure.Persistence;

public sealed class SurveyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IClock _clock;

    public SurveyRepository(IDbConnectionFactory connectionFactory, IClock clock)
    {
        _connectionFactory = connectionFactory;
        _clock = clock;
    }

    public SurveyRepository(IClock clock)
    {
        _connectionFactory = null!;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Survey>> GetActiveSurveySummariesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var surveys = await connection.QueryAsync<Survey>(new CommandDefinition(
            """
            SELECT
                survey.id_survey AS IdSurvey,
                survey.name_survey AS NameSurvey,
                survey.date_begin AS DateBegin,
                survey.date_end AS DateEnd,
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
            WHERE EXISTS (
                    SELECT 1
                    FROM public.organization_survey assignment
                    WHERE assignment.id_survey = survey.id_survey
                )
              AND (
                    (
                        survey.date_begin IS NOT NULL
                        AND survey.date_begin <= @Today
                        AND (survey.date_end IS NULL OR survey.date_end >= @Today)
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM public.organization_survey assignment
                        WHERE assignment.id_survey = survey.id_survey
                          AND assignment.date_end > survey.date_end
                          AND assignment.date_begin <= @Today
                          AND (assignment.date_end IS NULL OR assignment.date_end >= @Today)
                    )
                )
            ORDER BY survey.id_survey DESC;
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return surveys.AsList();
    }

    public Task<Survey?> GetSurveyWithScheduleAsync(
        NpgsqlConnection connection,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        return connection.QueryFirstOrDefaultAsync<Survey>(new CommandDefinition(
            """
            SELECT
                survey.id_survey AS IdSurvey,
                survey.name_survey AS NameSurvey,
                COALESCE(survey.date_begin, @Today) AS DateBegin,
                survey.date_end AS DateEnd,
                survey.description AS Description
            FROM public.survey survey
            WHERE survey.id_survey = @SurveyId;
            """,
            new { SurveyId = surveyId, Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<OrganizationSelectionItem>> GetAvailableOrganizationsForSurveyAsync(
        NpgsqlConnection connection,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var organizations = await connection.QueryAsync<OrganizationSelectionItem>(new CommandDefinition(
            """
            SELECT
                organization.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS Name
            FROM public.organization organization
            WHERE (
                    (organization.date_begin IS NULL OR organization.date_begin <= @Today)
                    AND (organization.date_end IS NULL OR organization.date_end >= @Today)
                )
               OR organization.id_organization IN (
                    SELECT id_organization
                    FROM public.organization_survey
                    WHERE id_survey = @SurveyId
               )
            ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name);
            """,
            new { SurveyId = surveyId, Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return organizations.AsList();
    }

    public async Task<IReadOnlyList<OrganizationSelectionItem>> GetSelectedOrganizationsForSurveyAsync(
        NpgsqlConnection connection,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var organizations = await connection.QueryAsync<OrganizationSelectionItem>(new CommandDefinition(
            """
            SELECT
                organization.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS Name,
                assignment.date_end AS DateEnd,
                survey.date_end AS SurveyDateEnd
            FROM public.organization_survey assignment
            INNER JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            INNER JOIN public.survey survey
                ON survey.id_survey = assignment.id_survey
            WHERE assignment.id_survey = @SurveyId
            ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name);
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken));
        return organizations.AsList();
    }

    public async Task<IReadOnlyList<OrganizationSelectionItem>> GetUnansweredOrganizationsForSurveyExtensionAsync(
        NpgsqlConnection connection,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var organizations = await connection.QueryAsync<OrganizationSelectionItem>(new CommandDefinition(
            """
            SELECT
                organization.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS Name,
                assignment.date_end AS DateEnd,
                survey.date_end AS SurveyDateEnd
            FROM public.organization_survey assignment
            INNER JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            INNER JOIN public.survey survey
                ON survey.id_survey = assignment.id_survey
            WHERE assignment.id_survey = @SurveyId
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.answer answer
                    WHERE answer.id_organization_survey = assignment.id_organization_survey
                )
            ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name);
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken));
        return organizations.AsList();
    }

    public Task<int> UpdateActiveSurveyPeriodAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTime dateBegin,
        DateTime dateEnd,
        CancellationToken cancellationToken = default)
    {
        return connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            WITH active_survey_ids AS (
                SELECT survey.id_survey
                FROM public.survey survey
                WHERE survey.date_begin IS NOT NULL
                  AND survey.date_begin <= @Today
                  AND (survey.date_end IS NULL OR survey.date_end >= @Today)

                UNION

                SELECT assignment.id_survey
                FROM public.organization_survey assignment
                INNER JOIN public.survey survey
                    ON survey.id_survey = assignment.id_survey
                WHERE assignment.date_end > survey.date_end
                  AND assignment.date_begin <= @Today
                  AND (assignment.date_end IS NULL OR assignment.date_end >= @Today)
            ),
            active_survey AS MATERIALIZED (
                SELECT
                    survey.id_survey,
                    survey.date_begin,
                    survey.date_end
                FROM public.survey survey
                INNER JOIN active_survey_ids active
                    ON active.id_survey = survey.id_survey
            ),
            updated_assignments AS (
                UPDATE public.organization_survey assignment
                SET
                    date_begin = @DateBegin::date,
                    date_end = CASE
                        WHEN assignment.date_end > active.date_end
                         AND assignment.date_end > @DateEnd::date
                            THEN assignment.date_end
                        ELSE @DateEnd::date
                    END
                FROM active_survey active
                WHERE assignment.id_survey = active.id_survey
                RETURNING assignment.id_survey
            ),
            updated_surveys AS (
                UPDATE public.survey survey
                SET
                    date_begin = @DateBegin::date,
                    date_end = @DateEnd::date
                FROM active_survey active
                WHERE survey.id_survey = active.id_survey
                RETURNING survey.id_survey
            )
            SELECT COUNT(*)
            FROM updated_surveys;
            """,
            new
            {
                DateBegin = dateBegin.Date,
                DateEnd = dateEnd.Date,
                Today = _clock.Today.Date
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ArchivedSurvey>> GetAdminArchivedSurveySummariesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var surveys = await connection.QueryAsync<ArchivedSurvey>(new CommandDefinition(
            """
            SELECT
                survey.id_survey AS IdSurvey,
                survey.date_begin AS DateBegin,
                survey.date_end AS DateEnd,
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
            WHERE EXISTS (
                    SELECT 1
                    FROM public.organization_survey assignment
                    WHERE assignment.id_survey = survey.id_survey
                )
              AND (
                    (
                        survey.date_begin IS NOT NULL
                        AND (
                            survey.date_begin > @Today
                            OR (survey.date_end IS NOT NULL AND survey.date_end < @Today)
                        )
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM public.organization_survey assignment
                        WHERE assignment.id_survey = survey.id_survey
                          AND assignment.date_end > survey.date_end
                          AND (
                              assignment.date_begin > @Today
                              OR (assignment.date_end IS NOT NULL AND assignment.date_end < @Today)
                          )
                    )
                )
            ORDER BY survey.id_survey DESC;
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return surveys.AsList();
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
                    survey.date_begin AS DateBegin,
                    survey.date_end AS DateEnd,
                    survey.name_survey AS NameSurvey,
                    survey.description AS Description
                FROM public.survey survey
                WHERE survey.id_survey = @SurveyId
                  AND EXISTS (
                        SELECT 1
                        FROM public.organization_survey assignment
                        WHERE assignment.id_survey = survey.id_survey
                  )
                  AND (
                        (
                            survey.date_begin IS NOT NULL
                            AND (
                                survey.date_begin > @Today
                                OR (survey.date_end IS NOT NULL AND survey.date_end < @Today)
                            )
                        )
                        OR EXISTS (
                            SELECT 1
                            FROM public.organization_survey assignment
                            WHERE assignment.id_survey = survey.id_survey
                              AND assignment.date_end > survey.date_end
                              AND (
                                  assignment.date_begin > @Today
                                  OR (assignment.date_end IS NOT NULL AND assignment.date_end < @Today)
                              )
                        )
                  );
                """,
                new { SurveyId = surveyId, Today = _clock.Today.Date },
                transaction,
                cancellationToken: cancellationToken));
    }

    public Task<int> CountActiveSurveysAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            OrganizationIds = organizationIds.ToArray(),
            HasOrganizationFilter = organizationIds.Count > 0,
            Today = _clock.Today.Date
        };

        return connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"{ActiveSurveyRowsCte} SELECT COUNT(*) FROM survey_rows WHERE {ActiveSurveyFilterPredicate};",
            parameters,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SurveyAssignmentTableRow>> GetActiveSurveyPageAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            OrganizationIds = organizationIds.ToArray(),
            HasOrganizationFilter = organizationIds.Count > 0,
            PageSize = pageSize,
            Offset = offset,
            Today = _clock.Today.Date
        };

        var rows = await connection.QueryAsync<SurveyAssignmentTableRow>(new CommandDefinition(
            $"""
            {ActiveSurveyRowsCte}
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                group_name_survey AS OriginalNameSurvey,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                group_date_end AS BaseDateEnd,
                organization_ids AS OrganizationIds,
                organization_names AS OrganizationNames,
                row_rank = 1 AS IsExtension,
                NULLIF(row_organization_id, 0) AS ExtensionOrganizationId
            FROM survey_rows
            WHERE {ActiveSurveyFilterPredicate}
            ORDER BY {BuildOrderBy(sortBy, sortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            parameters,
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SelectionOption>> GetActiveOrganizationOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var options = await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            $"""
            {ActiveSurveyRowsCte}
            SELECT DISTINCT
                o.id_organization AS Id,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS Name
            FROM survey_rows survey_row
            CROSS JOIN LATERAL unnest(survey_row.organization_ids) AS organization_ids(id_organization)
            INNER JOIN public.organization o
                ON o.id_organization = organization_ids.id_organization;
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return options.AsList();
    }

    public Task<int> CountArchivedSurveysAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> surveyIds,
        DateTime? dateStart,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default)
    {
        var parameters = BuildArchivedParameters(organizationIds, surveyIds, dateStart, dateEnd);
        return connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"{ArchivedSurveyRowsCte} SELECT COUNT(*) FROM survey_rows WHERE {ArchivedSurveyFilterPredicate};",
            parameters,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SurveyAssignmentTableRow>> GetArchivedSurveyPageAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> surveyIds,
        DateTime? dateStart,
        DateTime? dateEnd,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var parameters = BuildArchivedParameters(organizationIds, surveyIds, dateStart, dateEnd);
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);

        var rows = await connection.QueryAsync<SurveyAssignmentTableRow>(new CommandDefinition(
            $"""
            {ArchivedSurveyRowsCte}
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                group_name_survey AS OriginalNameSurvey,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                group_date_end AS BaseDateEnd,
                organization_ids AS OrganizationIds,
                organization_names AS OrganizationNames,
                row_rank = 1 AS IsExtension,
                NULLIF(row_organization_id, 0) AS ExtensionOrganizationId
            FROM survey_rows
            WHERE {ArchivedSurveyFilterPredicate}
            ORDER BY {BuildOrderBy(sortBy, sortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            parameters,
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SelectionOption>> GetArchivedOrganizationOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var options = await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            $"""
            {ArchivedSurveyRowsCte}
            SELECT DISTINCT
                o.id_organization AS Id,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS Name
            FROM survey_rows survey_row
            CROSS JOIN LATERAL unnest(survey_row.organization_ids) AS organization_ids(id_organization)
            INNER JOIN public.organization o
                ON o.id_organization = organization_ids.id_organization;
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return options.AsList();
    }

    public async Task<IReadOnlyList<SelectionOption>> GetArchivedSurveyOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var options = await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            $"""
            {ArchivedSurveyRowsCte}
            SELECT DISTINCT
                survey_row.id_survey AS Id,
                survey.name_survey AS Name
            FROM survey_rows survey_row
            INNER JOIN public.survey survey
                ON survey.id_survey = survey_row.id_survey;
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return options.AsList();
    }

    public async Task<IReadOnlyList<SelectionOption>> GetUserArchivedSurveyOptionsAsync(
        NpgsqlConnection connection,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        var options = await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            $"""
            SELECT DISTINCT
                archived.id_survey AS Id,
                archived.name_survey AS Name
            {UserArchiveBaseSql}
            ORDER BY Name, Id;
            """,
            new { OrganizationId = organizationId, Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return options.AsList();
    }

    public Task<int?> GetUserOrganizationIdAsync(
        NpgsqlConnection connection,
        int userId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT id_organization FROM public.app_user WHERE id_user = @UserId;",
            new { UserId = userId },
            cancellationToken: cancellationToken));

    public Task<bool> IsActiveAssignmentAsync(
        NpgsqlConnection connection,
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.organization_survey assignment
                WHERE assignment.id_survey = @SurveyId
                  AND assignment.id_organization = @OrganizationId
                  AND assignment.date_begin <= @Today
                  AND (assignment.date_end IS NULL OR assignment.date_end >= @Today)
            );
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId,
                Today = _clock.Today.Date
            },
            cancellationToken: cancellationToken));

    public async Task<UserSurveyAssignmentPageData> GetActiveUserSurveyPageAsync(
        NpgsqlConnection connection,
        int organizationId,
        string searchTerm,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("OrganizationId", organizationId);
        parameters.Add("HasSearch", !string.IsNullOrWhiteSpace(searchTerm));
        parameters.Add("SearchPattern", $"%{searchTerm}%");
        parameters.Add("PageSize", pageSize);
        parameters.Add("Today", _clock.Today.Date);

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) {ActiveUserSurveyBaseSql}",
            parameters,
            cancellationToken: cancellationToken));
        parameters.Add("Offset", NormalizePageOffset(totalCount, pageSize, offset));
        var surveys = (await connection.QueryAsync<Survey>(new CommandDefinition(
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
            parameters,
            cancellationToken: cancellationToken))).ToList();

        return new UserSurveyAssignmentPageData
        {
            TotalCount = totalCount,
            Surveys = surveys
        };
    }

    public async Task<UserSurveyAssignmentPageData> GetUserArchivePageAsync(
        NpgsqlConnection connection,
        int organizationId,
        string searchTerm,
        IReadOnlyCollection<int> surveyIds,
        DateTime? exactCompletionDate,
        DateTime? completionDateFrom,
        DateTime? completionDateTo,
        bool signedOnly,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("OrganizationId", organizationId);
        parameters.Add("SearchPattern", string.IsNullOrWhiteSpace(searchTerm) ? null : $"%{searchTerm}%");
        parameters.Add("SurveyIds", surveyIds.ToArray());
        parameters.Add("PageSize", pageSize);
        parameters.Add("Today", _clock.Today.Date);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filters.Add("archived.name_survey ILIKE @SearchPattern");
        }

        if (surveyIds.Count > 0)
        {
            filters.Add("archived.id_survey = ANY(@SurveyIds)");
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
                filters.Add("archived.completion_date::date >= @CompletionDateFrom");
                parameters.Add("CompletionDateFrom", completionDateFrom.Value);
            }

            if (completionDateTo.HasValue)
            {
                filters.Add("archived.completion_date::date <= @CompletionDateTo");
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
        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) {UserArchiveBaseSql} {whereClause}",
            parameters,
            cancellationToken: cancellationToken));
        parameters.Add("Offset", NormalizePageOffset(totalCount, pageSize, offset));
        var surveys = await connection.QueryAsync<Survey>(new CommandDefinition(
            $"""
            SELECT
                archived.id_survey AS IdSurvey,
                archived.name_survey AS NameSurvey,
                archived.description AS Description,
                archived.date_begin AS DateBegin,
                archived.date_end AS DateEnd,
                archived.completion_date AS CompletionDate,
                archived.csp AS Csp,
                archived.id_organization AS OrganizationId,
                archived.id_answer AS IdAnswer
            {UserArchiveBaseSql}
            {whereClause}
            ORDER BY archived.completion_date DESC NULLS LAST, archived.date_end DESC NULLS LAST, archived.id_survey DESC
            OFFSET @Offset
            LIMIT @PageSize;
            """,
            parameters,
            cancellationToken: cancellationToken));

        return new UserSurveyAssignmentPageData
        {
            TotalCount = totalCount,
            Surveys = surveys.AsList()
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

    public async Task ReplaceSurveyOrganizationsPreservingSchedulesAsync(
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
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM public.organization_survey WHERE id_survey = @SurveyId;",
                new { SurveyId = surveyId },
                transaction,
                cancellationToken: cancellationToken));
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
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

        foreach (var organizationId in normalizedOrganizationIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO public.organization_survey (id_organization, id_survey, date_begin, date_end)
                VALUES (@OrganizationId, @SurveyId, @DateBegin, @DateEnd)
                ON CONFLICT (id_organization, id_survey) DO NOTHING;
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
                    FROM public.survey survey
                    WHERE lower(btrim(survey.name_survey)) = lower(btrim(@SurveyName))
                      AND survey.date_begin = @DateBegin
                      AND survey.date_end IS NOT DISTINCT FROM @DateEnd::date
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

    public Task<int> UpdateAssignedSurveyEndDateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId,
        DateTime dateEnd,
        CancellationToken cancellationToken = default)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.organization_survey assignment
            SET date_end = @DateEnd::date
            FROM public.survey survey
            WHERE survey.id_survey = assignment.id_survey
              AND assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId
              AND assignment.date_end < @DateEnd::date
              AND survey.date_end < @DateEnd::date;
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId,
                DateEnd = dateEnd.Date
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    public Task<SurveyAssignmentPeriodState?> GetAssignmentPeriodForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        return connection.QuerySingleOrDefaultAsync<SurveyAssignmentPeriodState>(new CommandDefinition(
            """
            SELECT
                assignment.id_organization_survey AS AssignmentId,
                assignment.date_begin AS AssignmentDateBegin,
                assignment.date_end AS AssignmentDateEnd,
                survey.date_begin AS BaseDateBegin,
                survey.date_end AS BaseDateEnd,
                EXISTS (
                    SELECT 1
                    FROM public.answer answer
                    WHERE answer.id_organization_survey = assignment.id_organization_survey
                ) AS HasAnswer
            FROM public.organization_survey assignment
            INNER JOIN public.survey survey
                ON survey.id_survey = assignment.id_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId
            FOR UPDATE OF assignment;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId },
            transaction,
            cancellationToken: cancellationToken));
    }

    public Task<bool> HasAnswerCompletedAfterAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int assignmentId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        return connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.answer answer
                WHERE answer.id_organization_survey = @AssignmentId
                  AND answer.completion_date::date > @Date::date
            );
            """,
            new { AssignmentId = assignmentId, Date = date.Date },
            transaction,
            cancellationToken: cancellationToken));
    }

    public Task<int> UpdateExtensionEndDateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId,
        DateTime dateEnd,
        CancellationToken cancellationToken = default)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.organization_survey assignment
            SET date_end = @DateEnd
            FROM public.survey survey
            WHERE survey.id_survey = assignment.id_survey
              AND assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId
              AND assignment.date_end > survey.date_end
              AND @DateEnd::date > survey.date_end;
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId,
                DateEnd = dateEnd.Date
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    public Task<int> ResetExtensionPeriodAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.organization_survey assignment
            SET
                date_begin = survey.date_begin,
                date_end = survey.date_end
            FROM public.survey survey
            WHERE survey.id_survey = assignment.id_survey
              AND assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId
              AND survey.date_begin IS NOT NULL
              AND assignment.date_end > survey.date_end;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId },
            transaction,
            cancellationToken: cancellationToken));
    }

    public Task<int?> GetAssignmentIdForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT id_organization_survey
            FROM public.organization_survey
            WHERE id_organization = @OrganizationId
              AND id_survey = @SurveyId
            FOR UPDATE;
            """,
            new { OrganizationId = organizationId, SurveyId = surveyId },
            transaction,
            cancellationToken: cancellationToken));

    public Task<bool> IsAssignmentActiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int assignmentId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT date_begin <= @Today
               AND (date_end IS NULL OR date_end >= @Today)
            FROM public.organization_survey
            WHERE id_organization_survey = @AssignmentId;
            """,
            new
            {
                AssignmentId = assignmentId,
                Today = _clock.Today.Date
            },
            transaction,
            cancellationToken: cancellationToken));

    private static int[] NormalizeOrganizationIds(IEnumerable<int> organizationIds)
        => organizationIds
            .Where(static id => id > 0)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();

    private static int NormalizePageOffset(int totalCount, int pageSize, int requestedOffset)
    {
        var normalizedPageSize = Math.Max(pageSize, 1);
        var totalPages = totalCount <= 0
            ? 1
            : (int)Math.Ceiling((double)totalCount / normalizedPageSize);
        var maxOffset = (totalPages - 1) * normalizedPageSize;
        return Math.Min(Math.Max(requestedOffset, 0), maxOffset);
    }

    private DynamicParameters BuildArchivedParameters(
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
        parameters.Add("Today", _clock.Today.Date);
        return parameters;
    }

    private static string BuildOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            ? "DESC"
            : "ASC";

        return sortBy switch
        {
            "name" => $"group_name_survey {direction}, id_survey DESC, row_rank, row_organization_id",
            "dateBegin" => $"group_date_begin {direction} NULLS LAST, id_survey DESC, row_rank, row_organization_id",
            "dateEnd" => $"group_date_end {direction} NULLS LAST, id_survey DESC, row_rank, row_organization_id",
            _ => "group_name_survey ASC, id_survey DESC, row_rank, row_organization_id"
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
        WITH base_survey_rows AS (
            SELECT
                s.id_survey,
                s.name_survey,
                s.date_begin,
                s.date_end,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT assignment.id_organization
                        FROM public.organization_survey assignment
                        WHERE assignment.id_survey = s.id_survey
                          AND assignment.date_begin <= @Today
                          AND (assignment.date_end IS NULL OR assignment.date_end >= @Today)
                        ORDER BY assignment.id_organization
                    ),
                    ARRAY[]::integer[]
                ) AS organization_ids,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)
                        FROM public.organization_survey assignment
                        INNER JOIN public.organization o
                            ON o.id_organization = assignment.id_organization
                        WHERE assignment.id_survey = s.id_survey
                          AND assignment.date_begin <= @Today
                          AND (assignment.date_end IS NULL OR assignment.date_end >= @Today)
                        ORDER BY COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)
                    ),
                    ARRAY[]::text[]
                ) AS organization_names,
                s.name_survey AS group_name_survey,
                s.date_begin AS group_date_begin,
                s.date_end AS group_date_end,
                0 AS row_rank,
                0 AS row_organization_id
            FROM public.survey s
            WHERE s.date_begin IS NOT NULL
              AND (
                    (
                        s.date_begin <= @Today
                        AND (s.date_end IS NULL OR s.date_end >= @Today)
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM public.organization_survey extension
                        WHERE extension.id_survey = s.id_survey
                          AND extension.date_end > s.date_end
                          AND extension.date_begin <= @Today
                          AND extension.date_end >= @Today
                    )
                )
              AND EXISTS (
                    SELECT 1
                    FROM public.organization_survey assignment
                    WHERE assignment.id_survey = s.id_survey
                )
        ),
        extension_survey_rows AS (
            SELECT
                s.id_survey,
                s.name_survey || ': продление для ' ||
                    COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS name_survey,
                assignment.date_begin,
                assignment.date_end,
                ARRAY[assignment.id_organization]::integer[] AS organization_ids,
                ARRAY[COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)]::text[] AS organization_names,
                s.name_survey AS group_name_survey,
                s.date_begin AS group_date_begin,
                s.date_end AS group_date_end,
                1 AS row_rank,
                assignment.id_organization AS row_organization_id
            FROM public.organization_survey assignment
            INNER JOIN public.survey s
                ON s.id_survey = assignment.id_survey
            INNER JOIN public.organization o
                ON o.id_organization = assignment.id_organization
            WHERE assignment.date_end > s.date_end
              AND assignment.date_begin <= @Today
              AND (assignment.date_end IS NULL OR assignment.date_end >= @Today)
        ),
        survey_rows AS (
            SELECT * FROM base_survey_rows
            UNION ALL
            SELECT * FROM extension_survey_rows
        )
        """;

    private const string ArchivedSurveyRowsCte = """
        WITH base_survey_rows AS (
            SELECT
                s.id_survey,
                s.name_survey,
                s.date_begin,
                s.date_end,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT assignment.id_organization
                        FROM public.organization_survey assignment
                        WHERE assignment.id_survey = s.id_survey
                          AND assignment.date_begin IS NOT DISTINCT FROM s.date_begin
                          AND assignment.date_end IS NOT DISTINCT FROM s.date_end
                        ORDER BY assignment.id_organization
                    ),
                    ARRAY[]::integer[]
                ) AS organization_ids,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)
                        FROM public.organization_survey assignment
                        INNER JOIN public.organization o
                            ON o.id_organization = assignment.id_organization
                        WHERE assignment.id_survey = s.id_survey
                          AND assignment.date_begin IS NOT DISTINCT FROM s.date_begin
                          AND assignment.date_end IS NOT DISTINCT FROM s.date_end
                        ORDER BY COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)
                    ),
                    ARRAY[]::text[]
                ) AS organization_names,
                s.name_survey AS group_name_survey,
                s.date_begin AS group_date_begin,
                s.date_end AS group_date_end,
                0 AS row_rank,
                0 AS row_organization_id
            FROM public.survey s
            WHERE s.date_begin IS NOT NULL
              AND (
                    s.date_begin > @Today
                    OR (s.date_end IS NOT NULL AND s.date_end < @Today)
                )
              AND EXISTS (
                    SELECT 1
                    FROM public.organization_survey assignment
                    WHERE assignment.id_survey = s.id_survey
                )
        ),
        extension_survey_rows AS (
            SELECT
                s.id_survey,
                s.name_survey || ': продление для ' ||
                    COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS name_survey,
                assignment.date_begin,
                assignment.date_end,
                ARRAY[assignment.id_organization]::integer[] AS organization_ids,
                ARRAY[COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)]::text[] AS organization_names,
                s.name_survey AS group_name_survey,
                s.date_begin AS group_date_begin,
                s.date_end AS group_date_end,
                1 AS row_rank,
                assignment.id_organization AS row_organization_id
            FROM public.organization_survey assignment
            INNER JOIN public.survey s
                ON s.id_survey = assignment.id_survey
            INNER JOIN public.organization o
                ON o.id_organization = assignment.id_organization
            WHERE assignment.date_end > s.date_end
              AND (
                    assignment.date_begin > @Today
                    OR (assignment.date_end IS NOT NULL AND assignment.date_end < @Today)
                )
        ),
        survey_rows AS (
            SELECT * FROM base_survey_rows
            UNION ALL
            SELECT * FROM extension_survey_rows
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
              AND (assignment.date_begin IS NULL OR assignment.date_begin <= @Today)
              AND (assignment.date_end IS NULL OR assignment.date_end >= @Today)
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
                assignment.date_begin,
                assignment.date_end,
                answer.completion_date,
                answer.csp,
                answer.id_answer,
                assignment.id_organization
            FROM public.survey s
            INNER JOIN public.organization_survey assignment
                ON assignment.id_survey = s.id_survey
            LEFT JOIN public.answer answer
                ON answer.id_organization_survey = assignment.id_organization_survey
            WHERE assignment.id_organization = @OrganizationId
              AND (
                    answer.id_answer IS NOT NULL
                    OR (assignment.date_begin IS NOT NULL AND assignment.date_begin > @Today)
                    OR (assignment.date_end IS NOT NULL AND assignment.date_end < @Today)
                )
        ) AS archived
        """;

    public Task<int> CreateSurveyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string name,
        string? description,
        DateTime? dateBegin,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO public.survey (name_survey, description, date_begin, date_end)
            VALUES (@Name, @Description, @DateBegin, @DateEnd)
            RETURNING id_survey;
            """,
            new
            {
                Name = name,
                Description = description,
                DateBegin = dateBegin?.Date,
                DateEnd = dateEnd?.Date
            },
            transaction,
            cancellationToken: cancellationToken));

    public Task<int> UpdateSurveyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        string name,
        string? description,
        DateTime dateBegin,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            WITH current_survey AS MATERIALIZED (
                SELECT id_survey, date_begin, date_end
                FROM public.survey
                WHERE id_survey = @SurveyId
                FOR UPDATE
            ),
            updated_assignments AS (
                UPDATE public.organization_survey assignment
                SET
                    date_begin = @DateBegin::date,
                    date_end = CASE
                        WHEN assignment.date_end > current.date_end
                         AND assignment.date_end > @DateEnd::date
                            THEN assignment.date_end
                        ELSE @DateEnd::date
                    END
                FROM current_survey current
                WHERE assignment.id_survey = current.id_survey
                RETURNING assignment.id_organization_survey
            ),
            updated_survey AS (
                UPDATE public.survey survey
                SET
                    name_survey = @Name,
                    description = @Description,
                    date_begin = @DateBegin::date,
                    date_end = @DateEnd::date
                FROM current_survey current
                WHERE survey.id_survey = current.id_survey
                RETURNING survey.id_survey
            )
            SELECT COUNT(*)
            FROM updated_survey;
            """,
            new
            {
                SurveyId = surveyId,
                Name = name,
                Description = description,
                DateBegin = dateBegin.Date,
                DateEnd = dateEnd?.Date
            },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<bool> DeleteSurveyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var deletedId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "DELETE FROM public.survey WHERE id_survey = @SurveyId RETURNING id_survey;",
            new { SurveyId = surveyId },
            transaction,
            cancellationToken: cancellationToken));
        return deletedId.HasValue;
    }

    public Task<Survey?> GetSurveyByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int surveyId,
        CancellationToken cancellationToken = default) =>
        connection.QueryFirstOrDefaultAsync<Survey>(new CommandDefinition(
            """
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                description AS Description,
                date_begin AS DateBegin,
                date_end AS DateEnd
            FROM public.survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = surveyId },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<string>> GetSurveyCriteriaAsync(
        NpgsqlConnection connection,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var criteria = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT question_text
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken));
        return criteria.AsList();
    }

    public async Task AttachQuestionsAsync(
        NpgsqlConnection connection,
        IEnumerable<Survey> surveys,
        CancellationToken cancellationToken = default)
    {
        var surveyList = surveys.ToList();
        if (surveyList.Count == 0)
        {
            return;
        }

        var questionRows = await connection.QueryAsync<SurveyQuestionRow>(new CommandDefinition(
            """
            SELECT
                id_survey AS SurveyId,
                question_order AS QuestionOrder,
                question_text AS QuestionText
            FROM public.survey_question
            WHERE id_survey = ANY(@SurveyIds)
            ORDER BY id_survey, question_order;
            """,
            new { SurveyIds = surveyList.Select(survey => survey.IdSurvey).Distinct().ToArray() },
            cancellationToken: cancellationToken));
        var lookup = questionRows
            .GroupBy(row => row.SurveyId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => new SurveyQuestionItem
                {
                    Id = row.QuestionOrder,
                    Text = row.QuestionText
                }).ToList());

        foreach (var survey in surveyList)
        {
            survey.Questions = lookup.GetValueOrDefault(survey.IdSurvey, []);
        }
    }

    public async Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<SurveyQuestionItem>(new CommandDefinition(
            """
            SELECT question_order AS Id, question_text AS Text
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = surveyId },
            transaction,
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task ReplaceSurveyQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IReadOnlyCollection<SurveyQuestionItem> questions,
        CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.survey_question WHERE id_survey = @SurveyId;",
            new { SurveyId = surveyId },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var question in questions.OrderBy(question => question.Id))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO public.survey_question (id_survey, question_order, question_text)
                VALUES (@SurveyId, @QuestionOrder, @QuestionText);
                """,
                new { SurveyId = surveyId, QuestionOrder = question.Id, QuestionText = question.Text },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    public Task CopySurveyQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int sourceSurveyId,
        int targetSurveyId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.survey_question (id_survey, question_order, question_text)
            SELECT @TargetSurveyId, question_order, question_text
            FROM public.survey_question
            WHERE id_survey = @SourceSurveyId
            ON CONFLICT (id_survey, question_order) DO UPDATE
            SET question_text = EXCLUDED.question_text;
            """,
            new { SourceSurveyId = sourceSurveyId, TargetSurveyId = targetSurveyId },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<SurveySelectionItem>> GetSurveySelectionOptionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var surveys = await connection.QueryAsync<SurveySelectionItem>(new CommandDefinition(
            """
            SELECT id_survey AS Id, name_survey AS Name
            FROM public.survey
            ORDER BY lower(name_survey), id_survey;
            """,
            transaction: transaction,
            cancellationToken: cancellationToken));
        return surveys.ToArray();
    }

    public async Task<IReadOnlyList<SurveySelectionItem>> GetAutoCreationSurveySelectionOptionsAsync(
        NpgsqlConnection connection,
        int configId,
        CancellationToken cancellationToken = default)
    {
        var surveys = await connection.QueryAsync<SurveySelectionItem>(new CommandDefinition(
            """
            SELECT DISTINCT ON (lower(btrim(s.name_survey)))
                s.id_survey AS Id,
                s.name_survey AS Name
            FROM public.survey s
            LEFT JOIN public.survey_auto_creation_config selected
              ON selected.id_config = @ConfigId
             AND selected.id_survey = s.id_survey
            ORDER BY
                lower(btrim(s.name_survey)),
                (selected.id_survey IS NOT NULL) DESC,
                s.id_survey DESC;
            """,
            new { ConfigId = configId },
            cancellationToken: cancellationToken));
        return surveys.ToArray();
    }

    public async Task<IReadOnlySet<int>> GetExistingSurveyIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IReadOnlyCollection<int> surveyIds,
        CancellationToken cancellationToken = default)
    {
        if (surveyIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var ids = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT id_survey FROM public.survey WHERE id_survey = ANY(@SurveyIds);",
            new { SurveyIds = surveyIds.ToArray() },
            transaction,
            cancellationToken: cancellationToken));
        return ids.ToHashSet();
    }

    public Task<int> GetDistinctSurveyNameCountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IReadOnlyCollection<int> surveyIds,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(DISTINCT lower(btrim(name_survey)))
            FROM public.survey
            WHERE id_survey = ANY(@SurveyIds);
            """,
            new { SurveyIds = surveyIds.ToArray() },
            transaction,
            cancellationToken: cancellationToken));

    public Task<bool> HasCurrentAutoCreationStorageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT
                to_regclass('public.auto_creation_config') IS NOT NULL
                AND to_regclass('public.survey_auto_creation_config') IS NOT NULL
                AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'auto_creation_config'
                      AND column_name = 'reporting_period' AND is_nullable = 'NO'
                )
                AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'auto_creation_config'
                      AND column_name = 'reporting_offset_business_days' AND is_nullable = 'NO'
                )
                AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'auto_creation_config'
                      AND column_name = 'working_period' AND is_nullable = 'NO'
                )
                AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'organization_survey'
                      AND column_name = 'date_end' AND is_nullable = 'YES'
                );
            """,
            transaction: transaction,
            cancellationToken: cancellationToken));

    public async Task<AutoCreationConfigRecord> GetOrCreateAutoCreationConfigAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int configId,
        string defaultReportingPeriod,
        int defaultReportingOffsetBusinessDays,
        int defaultWorkingPeriod,
        bool lockRow,
        CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.auto_creation_config
                (id_config, reporting_period, reporting_offset_business_days, working_period, is_enabled)
            VALUES (@ConfigId, @ReportingPeriod, @ReportingOffsetBusinessDays, @WorkingPeriod, FALSE)
            ON CONFLICT (id_config) DO NOTHING;
            """,
            new
            {
                ConfigId = configId,
                ReportingPeriod = defaultReportingPeriod,
                ReportingOffsetBusinessDays = defaultReportingOffsetBusinessDays,
                WorkingPeriod = defaultWorkingPeriod
            },
            transaction,
            cancellationToken: cancellationToken));

        var lockClause = lockRow ? "FOR UPDATE" : string.Empty;
        return await connection.QuerySingleAsync<AutoCreationConfigRecord>(new CommandDefinition(
            $"""
            SELECT
                c.id_config AS IdConfig,
                c.reporting_period AS ReportingPeriod,
                c.reporting_offset_business_days AS ReportingOffsetBusinessDays,
                c.working_period AS WorkingPeriod,
                c.is_enabled AS IsEnabled
            FROM public.auto_creation_config c
            WHERE c.id_config = @ConfigId
            {lockClause};
            """,
            new { ConfigId = configId },
            transaction,
            cancellationToken: cancellationToken));
    }

    public Task SetAutoCreationEnabledAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int configId,
        bool isEnabled,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            "UPDATE public.auto_creation_config SET is_enabled = @IsEnabled WHERE id_config = @ConfigId;",
            new { ConfigId = configId, IsEnabled = isEnabled },
            transaction,
            cancellationToken: cancellationToken));

    public async Task SaveAutoCreationConfigAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int configId,
        string reportingPeriod,
        int reportingOffsetBusinessDays,
        int workingPeriod,
        bool isEnabled,
        IReadOnlyCollection<int> surveyIds,
        CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.auto_creation_config
                (id_config, reporting_period, reporting_offset_business_days, working_period, is_enabled)
            VALUES (@ConfigId, @ReportingPeriod, @ReportingOffsetBusinessDays, @WorkingPeriod, @IsEnabled)
            ON CONFLICT (id_config) DO UPDATE SET
                reporting_period = EXCLUDED.reporting_period,
                reporting_offset_business_days = EXCLUDED.reporting_offset_business_days,
                working_period = EXCLUDED.working_period,
                is_enabled = EXCLUDED.is_enabled;
            """,
            new
            {
                ConfigId = configId,
                ReportingPeriod = reportingPeriod,
                ReportingOffsetBusinessDays = reportingOffsetBusinessDays,
                WorkingPeriod = workingPeriod,
                IsEnabled = isEnabled
            },
            transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.survey_auto_creation_config WHERE id_config = @ConfigId;",
            new { ConfigId = configId },
            transaction,
            cancellationToken: cancellationToken));
        foreach (var surveyId in surveyIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO public.survey_auto_creation_config (id_config, id_survey) VALUES (@ConfigId, @SurveyId);",
                new { ConfigId = configId, SurveyId = surveyId },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<int>> GetSelectedAutoCreationSurveyIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int configId,
        CancellationToken cancellationToken = default)
    {
        var ids = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT id_survey FROM public.survey_auto_creation_config WHERE id_config = @ConfigId ORDER BY id_survey;",
            new { ConfigId = configId },
            transaction,
            cancellationToken: cancellationToken));
        return ids.ToArray();
    }

    public async Task<IReadOnlyList<SurveySelectionItem>> GetSelectedAutoCreationSurveysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int configId,
        CancellationToken cancellationToken = default)
    {
        var surveys = await connection.QueryAsync<SurveySelectionItem>(new CommandDefinition(
            """
            SELECT s.id_survey AS Id, s.name_survey AS Name
            FROM public.survey_auto_creation_config cs
            INNER JOIN public.survey s ON s.id_survey = cs.id_survey
            WHERE cs.id_config = @ConfigId
            ORDER BY lower(s.name_survey), s.id_survey;
            """,
            new { ConfigId = configId },
            transaction,
            cancellationToken: cancellationToken));
        return surveys.ToArray();
    }

    public Task<int> GetSelectedAutoCreationSurveyCountAsync(
        NpgsqlConnection connection,
        int configId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.survey_auto_creation_config WHERE id_config = @ConfigId;",
            new { ConfigId = configId },
            cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<int>(new CommandDefinition(
            """
            SELECT DISTINCT EXTRACT(YEAR FROM report_year)::integer AS report_year
            FROM public.survey_schedule schedule
            CROSS JOIN LATERAL generate_series(
                date_trunc('year', schedule.date_begin),
                date_trunc('year', schedule.date_end),
                interval '1 year'
            ) AS report_year
            WHERE schedule.date_begin IS NOT NULL
              AND schedule.date_end IS NOT NULL
            ORDER BY report_year DESC;
            """,
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<string?> GetSurveyNameAsync(int surveyId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT name_survey FROM public.survey WHERE id_survey = @SurveyId;",
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<SurveyQuestionItem>(new CommandDefinition(
            """
            SELECT question_order AS Id, question_text AS Text
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<AnswerRecord>> GetSurveyAnswersAsync(
        int surveyId,
        int? organizationId,
        DateTime periodStart,
        DateTime periodEndExclusive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var answers = (await connection.QueryAsync<AnswerRecord>(new CommandDefinition(
            """
            SELECT
                answer.id_answer,
                answer.id_organization_survey AS IdOrganizationSurvey,
                assignment.id_organization AS OrganizationId,
                assignment.id_survey,
                answer.csp,
                answer.completion_date,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS organization_name
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            WHERE assignment.id_survey = @SurveyId
              AND (@OrganizationId IS NULL OR assignment.id_organization = @OrganizationId)
              AND answer.completion_date >= @PeriodStart
              AND answer.completion_date < @PeriodEndExclusive
              AND EXISTS (
                  SELECT 1 FROM public.answer_item answer_item
                  WHERE answer_item.id_answer = answer.id_answer
              )
            ORDER BY answer.completion_date DESC;
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId,
                PeriodStart = periodStart,
                PeriodEndExclusive = periodEndExclusive
            },
            cancellationToken: cancellationToken))).ToList();

        await AttachAnswerItemsAsync(connection, answers, cancellationToken);
        return answers;
    }

    public async Task<IReadOnlyList<Survey>> GetSurveysAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var surveys = (await connection.QueryAsync<Survey>(new CommandDefinition(
            """
            SELECT
                survey.id_survey,
                survey.name_survey,
                schedule.date_end,
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
                ) AS organization_name
            FROM public.survey survey
            LEFT JOIN public.survey_schedule schedule
                ON schedule.id_survey = survey.id_survey;
            """,
            cancellationToken: cancellationToken))).ToList();

        await AttachSurveyQuestionsAsync(connection, surveys, cancellationToken);
        return surveys;
    }

    public async Task<IReadOnlyList<AnswerRecord>> GetAnswersAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var answers = (await connection.QueryAsync<AnswerRecord>(new CommandDefinition(
            """
            SELECT
                answer.id_organization_survey AS IdOrganizationSurvey,
                assignment.id_organization AS OrganizationId,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS organization_name,
                answer.csp,
                answer.id_answer,
                assignment.id_survey,
                survey.name_survey,
                answer.completion_date
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            LEFT JOIN public.survey survey
                ON survey.id_survey = assignment.id_survey
            WHERE EXISTS (
                SELECT 1 FROM public.answer_item answer_item
                WHERE answer_item.id_answer = answer.id_answer
            )
            ORDER BY answer.completion_date DESC;
            """,
            cancellationToken: cancellationToken))).ToList();

        await AttachAnswerItemsAsync(connection, answers, cancellationToken);
        return answers;
    }

    private static async Task AttachSurveyQuestionsAsync(
        IDbConnection connection,
        IEnumerable<Survey> surveys,
        CancellationToken cancellationToken)
    {
        var surveyList = surveys.ToList();
        if (surveyList.Count == 0)
        {
            return;
        }

        var surveyIds = surveyList.Select(survey => survey.IdSurvey).Distinct().ToArray();
        var rows = await connection.QueryAsync<SurveyQuestionLookupRow>(new CommandDefinition(
            """
            SELECT id_survey AS SurveyId, question_order AS QuestionOrder, question_text AS QuestionText
            FROM public.survey_question
            WHERE id_survey = ANY(@SurveyIds)
            ORDER BY id_survey, question_order;
            """,
            new { SurveyIds = surveyIds },
            cancellationToken: cancellationToken));
        var lookup = rows.GroupBy(row => row.SurveyId).ToDictionary(
            group => group.Key,
            group => group.Select(row => new SurveyQuestionItem
            {
                Id = row.QuestionOrder,
                Text = row.QuestionText
            }).ToList());

        foreach (var survey in surveyList)
        {
            survey.Questions = lookup.GetValueOrDefault(survey.IdSurvey, []);
        }
    }

    private static async Task AttachAnswerItemsAsync(
        IDbConnection connection,
        IEnumerable<AnswerRecord> answers,
        CancellationToken cancellationToken)
    {
        var answerList = answers.ToList();
        if (answerList.Count == 0)
        {
            return;
        }

        var answerIds = answerList.Select(answer => answer.IdAnswer).Distinct().ToArray();
        var rows = await connection.QueryAsync<AnswerItemLookupRow>(new CommandDefinition(
            """
            SELECT
                id_answer AS AnswerId,
                question_order AS QuestionOrder,
                question_text AS QuestionText,
                rating AS Rating,
                comment AS Comment
            FROM public.answer_item
            WHERE id_answer = ANY(@AnswerIds)
            ORDER BY id_answer, question_order;
            """,
            new { AnswerIds = answerIds },
            cancellationToken: cancellationToken));
        var lookup = rows.GroupBy(row => row.AnswerId).ToDictionary(
            group => group.Key,
            group => group.Select(row => new AnswerPayloadItem
            {
                QuestionId = row.QuestionOrder.ToString(),
                QuestionText = row.QuestionText,
                Rating = row.Rating,
                Comment = row.Comment
            }).ToList());

        foreach (var answer in answerList)
        {
            answer.Answers = lookup.GetValueOrDefault(answer.IdAnswer, []);
        }
    }

    private sealed class SurveyQuestionLookupRow
    {
        public int SurveyId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
    }

    private sealed class AnswerItemLookupRow
    {
        public int AnswerId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public int? Rating { get; init; }
        public string? Comment { get; init; }
    }

    private sealed class SurveyQuestionRow
    {
        public int SurveyId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
    }
}
