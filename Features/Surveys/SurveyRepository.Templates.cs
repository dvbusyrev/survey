using Dapper;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using Npgsql;

namespace MainProject.Infrastructure.Persistence;

public sealed partial class SurveyRepository
{
    private const string ActiveSurveyTemplateRowsCte = """
        WITH survey_rows AS (
            SELECT
                template.id_survey_template AS id_survey,
                template.name_survey_template AS name_survey,
                template.date_begin,
                template.date_end,
                template.ancestor_id,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT assignment.id_organization
                        FROM public.organization_survey_template assignment
                        WHERE assignment.id_survey_template = template.id_survey_template
                        ORDER BY assignment.id_organization
                    ),
                    ARRAY[]::integer[]
                ) AS organization_ids,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name)
                        FROM public.organization_survey_template assignment
                        INNER JOIN public.organization organization
                            ON organization.id_organization = assignment.id_organization
                        WHERE assignment.id_survey_template = template.id_survey_template
                        ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name)
                    ),
                    ARRAY[]::text[]
                ) AS organization_names,
                template.name_survey_template AS group_name_survey,
                template.date_begin AS group_date_begin,
                template.date_end AS group_date_end,
                EXISTS (
                    SELECT 1
                    FROM public.survey_template_auto_creation_config auto_creation
                    WHERE auto_creation.id_survey_template = template.id_survey_template
                ) AS is_auto_creation_enabled,
                0 AS row_rank,
                0 AS row_organization_id
            FROM public.survey_template template
            WHERE template.date_begin <= @Today
              AND (template.date_end IS NULL OR template.date_end >= @Today)
        )
        """;

    private const string PlannedSurveyTemplateRowsCte = """
        WITH survey_rows AS (
            SELECT
                template.id_survey_template AS id_survey,
                template.name_survey_template AS name_survey,
                template.date_begin,
                template.date_end,
                template.ancestor_id,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT assignment.id_organization
                        FROM public.organization_survey_template assignment
                        WHERE assignment.id_survey_template = template.id_survey_template
                        ORDER BY assignment.id_organization
                    ),
                    ARRAY[]::integer[]
                ) AS organization_ids,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name)
                        FROM public.organization_survey_template assignment
                        INNER JOIN public.organization organization
                            ON organization.id_organization = assignment.id_organization
                        WHERE assignment.id_survey_template = template.id_survey_template
                        ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name)
                    ),
                    ARRAY[]::text[]
                ) AS organization_names,
                template.name_survey_template AS group_name_survey,
                template.date_begin AS group_date_begin,
                template.date_end AS group_date_end,
                EXISTS (
                    SELECT 1
                    FROM public.survey_template_auto_creation_config auto_creation
                    WHERE auto_creation.id_survey_template = template.id_survey_template
                ) AS is_auto_creation_enabled,
                0 AS row_rank,
                0 AS row_organization_id
            FROM public.survey_template template
            WHERE template.date_begin > @Today
        )
        """;

    private const string ArchivedSurveyTemplateRowsCte = """
        WITH survey_rows AS (
            SELECT
                template.id_survey_template AS id_survey,
                template.name_survey_template AS name_survey,
                template.date_begin,
                template.date_end,
                template.ancestor_id,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT assignment.id_organization
                        FROM public.organization_survey_template assignment
                        WHERE assignment.id_survey_template = template.id_survey_template
                        ORDER BY assignment.id_organization
                    ),
                    ARRAY[]::integer[]
                ) AS organization_ids,
                COALESCE(
                    ARRAY(
                        SELECT DISTINCT COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name)
                        FROM public.organization_survey_template assignment
                        INNER JOIN public.organization organization
                            ON organization.id_organization = assignment.id_organization
                        WHERE assignment.id_survey_template = template.id_survey_template
                        ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name)
                    ),
                    ARRAY[]::text[]
                ) AS organization_names,
                template.name_survey_template AS group_name_survey,
                template.date_begin AS group_date_begin,
                template.date_end AS group_date_end,
                0 AS row_rank,
                0 AS row_organization_id
            FROM public.survey_template template
            WHERE template.date_end IS NOT NULL
              AND template.date_end < @Today
        )
        """;

    public Task<int> CountActiveSurveyTemplatesAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"{ActiveSurveyTemplateRowsCte} SELECT COUNT(*) FROM survey_rows WHERE {ActiveSurveyFilterPredicate};",
            new
            {
                OrganizationIds = organizationIds.ToArray(),
                HasOrganizationFilter = organizationIds.Count > 0,
                Today = _clock.Today.Date
            },
            cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<SurveyAssignmentTableRow>> GetActiveSurveyTemplatePageAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<SurveyAssignmentTableRow>(new CommandDefinition(
            $"""
            {ActiveSurveyTemplateRowsCte}
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                group_name_survey AS OriginalNameSurvey,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                group_date_end AS BaseDateEnd,
                organization_ids AS OrganizationIds,
                organization_names AS OrganizationNames,
                is_auto_creation_enabled AS IsAutoCreationEnabled,
                ancestor_id AS AncestorId,
                row_rank = 1 AS IsExtension,
                NULLIF(row_organization_id, 0) AS ExtensionOrganizationId
            FROM survey_rows
            WHERE {ActiveSurveyFilterPredicate}
            ORDER BY {BuildOrderBy(sortBy, sortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            new
            {
                OrganizationIds = organizationIds.ToArray(),
                HasOrganizationFilter = organizationIds.Count > 0,
                Today = _clock.Today.Date,
                PageSize = pageSize,
                Offset = offset
            },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public Task<int> CountPlannedSurveyTemplatesAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"{PlannedSurveyTemplateRowsCte} SELECT COUNT(*) FROM survey_rows WHERE {ActiveSurveyFilterPredicate};",
            new
            {
                OrganizationIds = organizationIds.ToArray(),
                HasOrganizationFilter = organizationIds.Count > 0,
                Today = _clock.Today.Date
            },
            cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<SurveyAssignmentTableRow>> GetPlannedSurveyTemplatePageAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<SurveyAssignmentTableRow>(new CommandDefinition(
            $"""
            {PlannedSurveyTemplateRowsCte}
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                group_name_survey AS OriginalNameSurvey,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                group_date_end AS BaseDateEnd,
                organization_ids AS OrganizationIds,
                organization_names AS OrganizationNames,
                is_auto_creation_enabled AS IsAutoCreationEnabled,
                ancestor_id AS AncestorId,
                false AS IsExtension,
                NULL::integer AS ExtensionOrganizationId
            FROM survey_rows
            WHERE {ActiveSurveyFilterPredicate}
            ORDER BY {BuildOrderBy(sortBy, sortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            new
            {
                OrganizationIds = organizationIds.ToArray(),
                HasOrganizationFilter = organizationIds.Count > 0,
                Today = _clock.Today.Date,
                PageSize = pageSize,
                Offset = offset
            },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SelectionOption>> GetPlannedSurveyTemplateOrganizationOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            $"""
            {PlannedSurveyTemplateRowsCte}
            SELECT DISTINCT
                organization.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS Name
            FROM survey_rows survey_row
            CROSS JOIN LATERAL unnest(survey_row.organization_ids) AS organization_ids(id_organization)
            INNER JOIN public.organization organization
                ON organization.id_organization = organization_ids.id_organization;
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SelectionOption>> GetActiveSurveyTemplateOrganizationOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            $"""
            {ActiveSurveyTemplateRowsCte}
            SELECT DISTINCT
                organization.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS Name
            FROM survey_rows survey_row
            CROSS JOIN LATERAL unnest(survey_row.organization_ids) AS organization_ids(id_organization)
            INNER JOIN public.organization organization
                ON organization.id_organization = organization_ids.id_organization;
            """,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public Task<int> CountArchivedSurveyTemplatesAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> templateIds,
        DateTime? dateStart,
        DateTime? dateEnd,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"{ArchivedSurveyTemplateRowsCte} SELECT COUNT(*) FROM survey_rows WHERE {ArchivedSurveyFilterPredicate};",
            BuildSurveyTemplateArchiveParameters(organizationIds, templateIds, dateStart, dateEnd),
            cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<SurveyAssignmentTableRow>> GetArchivedSurveyTemplatePageAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> templateIds,
        DateTime? dateStart,
        DateTime? dateEnd,
        string sortBy,
        string sortDirection,
        int pageSize,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var parameters = BuildSurveyTemplateArchiveParameters(organizationIds, templateIds, dateStart, dateEnd);
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);
        var rows = await connection.QueryAsync<SurveyAssignmentTableRow>(new CommandDefinition(
            $"""
            {ArchivedSurveyTemplateRowsCte}
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                group_name_survey AS OriginalNameSurvey,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                group_date_end AS BaseDateEnd,
                organization_ids AS OrganizationIds,
                organization_names AS OrganizationNames,
                ancestor_id AS AncestorId,
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

    public Task<IReadOnlyList<SelectionOption>> GetArchivedSurveyTemplateOrganizationOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default) =>
        QueryTemplateSelectionOptionsAsync(
            connection,
            $"""
            {ArchivedSurveyTemplateRowsCte}
            SELECT DISTINCT
                organization.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS Name
            FROM survey_rows survey_row
            CROSS JOIN LATERAL unnest(survey_row.organization_ids) AS organization_ids(id_organization)
            INNER JOIN public.organization organization
                ON organization.id_organization = organization_ids.id_organization;
            """,
            cancellationToken);

    public Task<IReadOnlyList<SelectionOption>> GetArchivedSurveyTemplateOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default) =>
        QueryTemplateSelectionOptionsAsync(
            connection,
            $"""
            {ArchivedSurveyTemplateRowsCte}
            SELECT DISTINCT
                survey_row.id_survey AS Id,
                template.name_survey_template AS Name
            FROM survey_rows survey_row
            INNER JOIN public.survey_template template
                ON template.id_survey_template = survey_row.id_survey;
            """,
            cancellationToken);

    public Task<IReadOnlyList<SelectionOption>> GetActiveSurveyTemplateOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default) =>
        QueryTemplateSelectionOptionsAsync(
            connection,
            $"""
            {ActiveSurveyTemplateRowsCte}
            SELECT
                id_survey AS Id,
                name_survey AS Name
            FROM survey_rows;
            """,
            cancellationToken);

    public Task<IReadOnlyList<SelectionOption>> GetAutoCreationTemplateSelectionOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default) =>
        QueryTemplateSelectionOptionsAsync(
            connection,
            $"""
            {ActiveSurveyTemplateRowsCte}
            SELECT
                id_survey AS Id,
                name_survey AS Name
            FROM survey_rows
            WHERE cardinality(organization_ids) > 0;
            """,
            cancellationToken);

    private async Task<IReadOnlyList<SelectionOption>> QueryTemplateSelectionOptionsAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            sql,
            new { Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private DynamicParameters BuildSurveyTemplateArchiveParameters(
        IReadOnlyCollection<int> organizationIds,
        IReadOnlyCollection<int> templateIds,
        DateTime? dateStart,
        DateTime? dateEnd)
    {
        var parameters = new DynamicParameters();
        parameters.Add("OrganizationIds", organizationIds.ToArray());
        parameters.Add("HasOrganizationFilter", organizationIds.Count > 0);
        parameters.Add("SurveyIds", templateIds.ToArray());
        parameters.Add("HasSurveyFilter", templateIds.Count > 0);
        parameters.Add("HasDateFilter", dateStart.HasValue && dateEnd.HasValue);
        parameters.Add("DateStart", dateStart);
        parameters.Add("DateEnd", dateEnd);
        parameters.Add("Today", _clock.Today.Date);
        return parameters;
    }

    public Task<int> CreateSurveyTemplateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string name,
        string? description,
        DateTime dateBegin,
        DateTime? dateEnd,
        int? ancestorId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO public.survey_template (
                name_survey_template,
                description,
                date_begin,
                date_end,
                ancestor_id
            )
            VALUES (@Name, @Description, @DateBegin, @DateEnd, @AncestorId)
            RETURNING id_survey_template;
            """,
            new
            {
                Name = name,
                Description = description,
                DateBegin = dateBegin.Date,
                DateEnd = dateEnd?.Date,
                AncestorId = ancestorId
            },
            transaction,
            cancellationToken: cancellationToken));

    public Task<Survey?> GetSurveyTemplateByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId,
        CancellationToken cancellationToken = default) =>
        connection.QueryFirstOrDefaultAsync<Survey>(new CommandDefinition(
            """
            SELECT
                id_survey_template AS IdSurvey,
                name_survey_template AS NameSurvey,
                description AS Description,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                ancestor_id AS AncestorId
            FROM public.survey_template
            WHERE id_survey_template = @TemplateId;
            """,
            new { TemplateId = templateId },
            transaction,
            cancellationToken: cancellationToken));

    public Task<Survey?> GetSurveyTemplateByIdForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int templateId,
        CancellationToken cancellationToken = default) =>
        connection.QueryFirstOrDefaultAsync<Survey>(new CommandDefinition(
            """
            SELECT
                id_survey_template AS IdSurvey,
                name_survey_template AS NameSurvey,
                description AS Description,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                ancestor_id AS AncestorId
            FROM public.survey_template
            WHERE id_survey_template = @TemplateId
            FOR UPDATE;
            """,
            new { TemplateId = templateId },
            transaction,
            cancellationToken: cancellationToken));

    public Task<int> SetSurveyTemplateEndDateIfOpenAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int templateId,
        DateTime dateEnd,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.survey_template
            SET date_end = @DateEnd
            WHERE id_survey_template = @TemplateId
              AND date_end IS NULL;
            """,
            new { TemplateId = templateId, DateEnd = dateEnd.Date },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<Survey>> GetPlannedSurveyTemplateDescendantsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int ancestorId,
        CancellationToken cancellationToken = default)
    {
        var templates = await connection.QueryAsync<Survey>(new CommandDefinition(
            """
            SELECT
                id_survey_template AS IdSurvey,
                name_survey_template AS NameSurvey,
                description AS Description,
                date_begin AS DateBegin,
                date_end AS DateEnd,
                ancestor_id AS AncestorId
            FROM public.survey_template
            WHERE ancestor_id = @AncestorId
            ORDER BY date_begin, id_survey_template;
            """,
            new { AncestorId = ancestorId },
            transaction,
            cancellationToken: cancellationToken));
        return templates.AsList();
    }

    public Task<string?> GetSurveyTemplateNameAsync(
        NpgsqlConnection connection,
        int templateId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT name_survey_template FROM public.survey_template WHERE id_survey_template = @TemplateId;",
            new { TemplateId = templateId },
            cancellationToken: cancellationToken));

    public Task<bool> IsActiveSurveyTemplateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.survey_template
                WHERE id_survey_template = @TemplateId
                  AND date_begin <= @Today
                  AND (date_end IS NULL OR date_end >= @Today)
            );
            """,
            new { TemplateId = templateId, Today = _clock.Today.Date },
            transaction,
            cancellationToken: cancellationToken));

    public Task<Survey?> GetSurveyTemplateWithScheduleAsync(
        NpgsqlConnection connection,
        int templateId,
        CancellationToken cancellationToken = default) =>
        GetSurveyTemplateByIdAsync(connection, null, templateId, cancellationToken);

    public async Task<IReadOnlyList<string>> GetSurveyTemplateCriteriaAsync(
        NpgsqlConnection connection,
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT question_text
            FROM public.survey_template_question
            WHERE id_survey_template = @TemplateId
            ORDER BY question_order;
            """,
            new { TemplateId = templateId },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyTemplateQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<SurveyQuestionItem>(new CommandDefinition(
            """
            SELECT question_order AS Id, question_text AS Text
            FROM public.survey_template_question
            WHERE id_survey_template = @TemplateId
            ORDER BY question_order;
            """,
            new { TemplateId = templateId },
            transaction,
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task ReplaceSurveyTemplateQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int templateId,
        IReadOnlyCollection<SurveyQuestionItem> questions,
        CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.survey_template_question WHERE id_survey_template = @TemplateId;",
            new { TemplateId = templateId },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var question in questions.OrderBy(item => item.Id))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO public.survey_template_question (
                    id_survey_template,
                    question_order,
                    question_text
                )
                VALUES (@TemplateId, @QuestionOrder, @QuestionText);
                """,
                new
                {
                    TemplateId = templateId,
                    QuestionOrder = question.Id,
                    QuestionText = question.Text
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<OrganizationSelectionItem>> GetAvailableOrganizationsForSurveyTemplateAsync(
        NpgsqlConnection connection,
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<OrganizationSelectionItem>(new CommandDefinition(
            """
            SELECT
                organization.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS Name
            FROM public.organization organization
            WHERE (
                    organization.date_begin <= @Today
                    AND organization.date_end >= @Today
                )
                OR EXISTS (
                    SELECT 1
                    FROM public.organization_survey_template assignment
                    WHERE assignment.id_survey_template = @TemplateId
                      AND assignment.id_organization = organization.id_organization
                )
            ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name);
            """,
            new { TemplateId = templateId, Today = _clock.Today.Date },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<OrganizationSelectionItem>> GetSelectedOrganizationsForSurveyTemplateAsync(
        NpgsqlConnection connection,
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<OrganizationSelectionItem>(new CommandDefinition(
            """
            SELECT
                organization.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS Name
            FROM public.organization_survey_template assignment
            INNER JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            WHERE assignment.id_survey_template = @TemplateId
            ORDER BY COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name);
            """,
            new { TemplateId = templateId },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<int>> GetOrganizationIdsForSurveyTemplateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var ids = await connection.QueryAsync<int>(new CommandDefinition(
            """
            SELECT id_organization
            FROM public.organization_survey_template
            WHERE id_survey_template = @TemplateId
            ORDER BY id_organization;
            """,
            new { TemplateId = templateId },
            transaction,
            cancellationToken: cancellationToken));
        return ids.AsList();
    }

    public Task<bool> IsSurveyTemplateSelectedForAutoCreationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.survey_template_auto_creation_config selection
                WHERE selection.id_config = 1
                  AND selection.id_survey_template = @TemplateId
            );
            """,
            new { TemplateId = templateId },
            transaction,
            cancellationToken: cancellationToken));

    public async Task SetSurveyTemplateAutoCreationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int configId,
        int templateId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        if (isEnabled)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO public.survey_template_auto_creation_config (
                    id_config,
                    id_survey_template
                )
                VALUES (@ConfigId, @TemplateId)
                ON CONFLICT (id_config, id_survey_template) DO NOTHING;
                """,
                new { ConfigId = configId, TemplateId = templateId },
                transaction,
                cancellationToken: cancellationToken));
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM public.survey_template_auto_creation_config
            WHERE id_config = @ConfigId
              AND id_survey_template = @TemplateId;
            """,
            new { ConfigId = configId, TemplateId = templateId },
            transaction,
            cancellationToken: cancellationToken));
    }

    public Task<int> RemoveInactiveAutoCreationTemplatesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int configId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM public.survey_template_auto_creation_config selection
            USING public.survey_template template
            WHERE selection.id_config = @ConfigId
              AND template.id_survey_template = selection.id_survey_template
              AND (
                    template.date_end IS NOT NULL
                    AND template.date_end < @Today
                  );
            """,
            new { ConfigId = configId, Today = _clock.Today.Date },
            transaction,
            cancellationToken: cancellationToken));

    public async Task UpsertSurveyTemplateAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int templateId,
        IEnumerable<int> organizationIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var organizationId in NormalizeOrganizationIds(organizationIds))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO public.organization_survey_template (
                    id_organization,
                    id_survey_template
                )
                VALUES (@OrganizationId, @TemplateId)
                ON CONFLICT (id_organization, id_survey_template) DO NOTHING;
                """,
                new
                {
                    OrganizationId = organizationId,
                    TemplateId = templateId
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task ReplaceSurveyTemplateOrganizationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int templateId,
        IEnumerable<int> organizationIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedOrganizationIds = NormalizeOrganizationIds(organizationIds);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM public.organization_survey_template
            WHERE id_survey_template = @TemplateId
              AND NOT (id_organization = ANY(@OrganizationIds));
            """,
            new { TemplateId = templateId, OrganizationIds = normalizedOrganizationIds },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var organizationId in normalizedOrganizationIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO public.organization_survey_template (
                    id_organization,
                    id_survey_template
                )
                VALUES (@OrganizationId, @TemplateId)
                ON CONFLICT (id_organization, id_survey_template) DO NOTHING;
                """,
                new
                {
                    OrganizationId = organizationId,
                    TemplateId = templateId
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    public Task<int> UpdateSurveyTemplateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int templateId,
        string name,
        string? description,
        DateTime dateBegin,
        DateTime? dateEnd,
        int? ancestorId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.survey_template
            SET
                name_survey_template = @Name,
                description = @Description,
                date_begin = @DateBegin,
                date_end = @DateEnd,
                ancestor_id = @AncestorId
            WHERE id_survey_template = @TemplateId;
            """,
            new
            {
                TemplateId = templateId,
                Name = name,
                Description = description,
                DateBegin = dateBegin.Date,
                DateEnd = dateEnd?.Date,
                AncestorId = ancestorId
            },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<int> PromotePlannedSurveyTemplatesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTime today,
        CancellationToken cancellationToken = default)
    {
        var dueTemplates = (await connection.QueryAsync<PlannedTemplatePromotionRow>(new CommandDefinition(
            """
            SELECT
                id_survey_template AS TemplateId,
                ancestor_id AS AncestorId,
                date_begin AS DateBegin
            FROM public.survey_template
            WHERE ancestor_id IS NOT NULL
              AND date_begin <= @Today
            ORDER BY date_begin, id_survey_template
            FOR UPDATE;
            """,
            new { Today = today.Date },
            transaction,
            cancellationToken: cancellationToken))).AsList();

        if (dueTemplates.Count == 0)
        {
            return 0;
        }

        foreach (var parentGroup in dueTemplates.GroupBy(item => item.AncestorId))
        {
            var archiveDate = parentGroup.Min(item => item.DateBegin).Date.AddDays(-1);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE public.survey_template
                SET date_end = CASE
                    WHEN date_end IS NULL OR date_end > @ArchiveDate THEN @ArchiveDate
                    ELSE date_end
                END
                WHERE id_survey_template = @AncestorId;
                """,
                new { AncestorId = parentGroup.Key, ArchiveDate = archiveDate },
                transaction,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.survey_template
            SET ancestor_id = NULL
            WHERE id_survey_template = ANY(@TemplateIds);
            """,
            new { TemplateIds = dueTemplates.Select(item => item.TemplateId).ToArray() },
            transaction,
            cancellationToken: cancellationToken));

        return dueTemplates.Count;
    }

    public Task<bool> HasPlannedSurveyTemplateDescendantsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int templateId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.survey_template
                WHERE ancestor_id = @TemplateId
            );
            """,
            new { TemplateId = templateId },
            transaction,
            cancellationToken: cancellationToken));

    public Task<int> UpdateActiveSurveyTemplatePeriodAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTime dateBegin,
        DateTime dateEnd,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.survey_template
            SET date_begin = @DateBegin,
                date_end = @DateEnd
            WHERE date_begin <= @Today
              AND (date_end IS NULL OR date_end >= @Today);
            """,
            new
            {
                DateBegin = dateBegin.Date,
                DateEnd = dateEnd.Date,
                Today = _clock.Today.Date
            },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<bool> DeleteSurveyTemplateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var deletedId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            DELETE FROM public.survey_template
            WHERE id_survey_template = @TemplateId
            RETURNING id_survey_template;
            """,
            new { TemplateId = templateId },
            transaction,
            cancellationToken: cancellationToken));
        return deletedId.HasValue;
    }

    private sealed class PlannedTemplatePromotionRow
    {
        public int TemplateId { get; init; }
        public int AncestorId { get; init; }
        public DateTime DateBegin { get; init; }
    }
}
