using MainProject.Application.Support;

namespace MainProject.Infrastructure.Persistence;

internal static class AuditLogQueryBuilder
{
    private const string AuditRowJsonExclusions = """
        - 'id_audit'
        - 'operation'
        - 'changed_at'
        - 'changed_by_user_id'
        - 'parent_audit_id'
        - 'date_update'
        """;

    public static string? BuildAuditSql(IReadOnlyCollection<string> availableAuditTables)
    {
        var unionParts = GetAvailableSources(availableAuditTables)
            .Select(source => BuildStructuredAuditSelect(source, includeDetails: true, dateDirection: null, limitPerSource: false))
            .ToArray();

        if (unionParts.Length == 0)
        {
            return null;
        }

        return $$"""
            SELECT
                audit_entries.source_table AS SourceTable,
                audit_entries.source_order AS SourceOrder,
                audit_entries.id_audit AS IdAudit,
                audit_entries.parent_audit_id AS ParentAuditId,
                audit_entries.operation AS Operation,
                audit_entries.changed_at AS ChangedAt,
                audit_entries.changed_by_user_id AS ChangedByUserId,
                COALESCE(actor.full_name, actor.login) AS ActorName,
                audit_entries.target_name AS TargetName,
                audit_entries.target_id AS TargetId,
                audit_entries.related_kind AS RelatedKind,
                audit_entries.related_id AS RelatedId,
                audit_entries.record_pk_json AS RecordPkJson,
                audit_entries.row_data_json AS RowDataJson,
                audit_entries.old_row_data_json AS OldRowDataJson,
                audit_entries.new_row_data_json AS NewRowDataJson
            FROM (
                {{string.Join("\n                UNION ALL\n", unionParts)}}
            ) audit_entries
            LEFT JOIN public.app_user actor
                ON actor.id_user = audit_entries.changed_by_user_id
            ORDER BY audit_entries.changed_at DESC, audit_entries.source_order DESC, audit_entries.id_audit DESC;
            """;
    }

    public static string? BuildAuditPageSql(
        IReadOnlyCollection<string> availableAuditTables,
        string sortBy,
        string sortDirection,
        bool includeDetails)
    {
        var isDateSort = string.Equals(sortBy, "date", StringComparison.Ordinal);
        var dateDirection = string.Equals(sortDirection, "asc", StringComparison.Ordinal) ? "ASC" : "DESC";
        var unionParts = GetAvailableSources(availableAuditTables)
            .Select(source => BuildStructuredAuditSelect(source, includeDetails, dateDirection, isDateSort))
            .ToArray();

        if (unionParts.Length == 0)
        {
            return null;
        }

        return $$"""
            WITH audit_entries AS (
                {{string.Join("\n                UNION ALL\n", unionParts)}}
            ),
            event_entries AS (
                SELECT
                    audit_entries.*,
                    {{BuildAuditEventKeyExpression("audit_entries")}} AS event_key
                FROM audit_entries
            ),
            event_groups AS (
                SELECT
                    event_key,
                    MAX(changed_at) AS changed_at,
                    MAX(source_order) AS source_order,
                    MAX(id_audit) AS id_audit,
                    MAX(changed_by_user_id) AS changed_by_user_id,
                    MAX(operation) AS operation,
                    MIN(source_table) AS source_table
                FROM event_entries
                GROUP BY event_key
            ),
            event_page AS (
                SELECT event_groups.event_key
                FROM event_groups
                LEFT JOIN public.app_user actor
                    ON actor.id_user = event_groups.changed_by_user_id
                ORDER BY {{BuildAuditEventPageOrderBy(sortBy, sortDirection)}}
                OFFSET @Offset
                LIMIT @Limit
            )
            SELECT
                audit_entries.source_table AS SourceTable,
                audit_entries.source_order AS SourceOrder,
                audit_entries.id_audit AS IdAudit,
                audit_entries.parent_audit_id AS ParentAuditId,
                audit_entries.operation AS Operation,
                audit_entries.changed_at AS ChangedAt,
                audit_entries.changed_by_user_id AS ChangedByUserId,
                COALESCE(actor.full_name, actor.login) AS ActorName,
                audit_entries.target_name AS TargetName,
                audit_entries.target_id AS TargetId,
                audit_entries.related_kind AS RelatedKind,
                audit_entries.related_id AS RelatedId,
                audit_entries.record_pk_json AS RecordPkJson,
                audit_entries.row_data_json AS RowDataJson,
                audit_entries.old_row_data_json AS OldRowDataJson,
                audit_entries.new_row_data_json AS NewRowDataJson
            FROM event_entries audit_entries
            INNER JOIN event_page
                ON event_page.event_key = audit_entries.event_key
            LEFT JOIN public.app_user actor
                ON actor.id_user = audit_entries.changed_by_user_id
            ORDER BY {{BuildAuditPageOrderBy(sortBy, sortDirection)}}
            """;
    }

    public static string? BuildAuditEventCountSql(IReadOnlyCollection<string> availableAuditTables)
    {
        var unionParts = GetAvailableSources(availableAuditTables)
            .Select(source => BuildStructuredAuditSelect(source, includeDetails: false, dateDirection: null, limitPerSource: false))
            .ToArray();

        if (unionParts.Length == 0)
        {
            return null;
        }

        return $$"""
            WITH audit_entries AS (
                {{string.Join("\n                UNION ALL\n", unionParts)}}
            )
            SELECT COUNT(DISTINCT {{BuildAuditEventKeyExpression("audit_entries")}})::bigint
            FROM audit_entries;
            """;
    }

    public static string? BuildAuditDetailSql(IReadOnlyCollection<string> availableAuditTables, string? sourceTable)
    {
        var sources = GetAvailableSources(availableAuditTables)
            .Where(source => string.IsNullOrWhiteSpace(sourceTable)
                || string.Equals(source.SourceTable, sourceTable.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (sources.Length == 0 && !string.IsNullOrWhiteSpace(sourceTable))
        {
            sources = GetAvailableSources(availableAuditTables).ToArray();
        }

        var unionParts = sources
            .Select(source => BuildStructuredAuditSelect(source, includeDetails: true, dateDirection: null, limitPerSource: false))
            .ToArray();

        return unionParts.Length == 0
            ? null
            : BuildAuditRowsSql(
                unionParts,
                "audit_entries.id_audit = @IdAudit",
                "ORDER BY audit_entries.changed_at DESC, audit_entries.source_order DESC, audit_entries.id_audit DESC LIMIT 1;");
    }

    public static string? BuildAuditRelatedRowsSql(IReadOnlyCollection<string> availableAuditTables)
    {
        var unionParts = GetAvailableSources(availableAuditTables)
            .Select(source => BuildStructuredAuditSelect(source, includeDetails: true, dateDirection: null, limitPerSource: false))
            .ToArray();

        return unionParts.Length == 0
            ? null
            : BuildAuditRowsSql(
                unionParts,
                """
                audit_entries.changed_at = @ChangedAt
                AND COALESCE(audit_entries.changed_by_user_id, -1) = COALESCE(@ChangedByUserId, -1)
                AND audit_entries.related_kind = @RelatedKind
                AND audit_entries.related_id = @RelatedId
                """,
                "ORDER BY audit_entries.changed_at ASC, audit_entries.source_order ASC, audit_entries.id_audit ASC;");
    }

    private static IEnumerable<AuditLogTableDefinition> GetAvailableSources(IReadOnlyCollection<string> availableAuditTables)
    {
        return AuditLogTableRegistry.Sources.Where(source => availableAuditTables.Contains(source.AuditTableName));
    }

    private static string BuildAuditRowsSql(string[] unionParts, string whereClause, string orderByClause)
    {
        return $$"""
            SELECT
                audit_entries.source_table AS SourceTable,
                audit_entries.source_order AS SourceOrder,
                audit_entries.id_audit AS IdAudit,
                audit_entries.parent_audit_id AS ParentAuditId,
                audit_entries.operation AS Operation,
                audit_entries.changed_at AS ChangedAt,
                audit_entries.changed_by_user_id AS ChangedByUserId,
                COALESCE(actor.full_name, actor.login) AS ActorName,
                audit_entries.target_name AS TargetName,
                audit_entries.target_id AS TargetId,
                audit_entries.related_kind AS RelatedKind,
                audit_entries.related_id AS RelatedId,
                audit_entries.record_pk_json AS RecordPkJson,
                audit_entries.row_data_json AS RowDataJson,
                audit_entries.old_row_data_json AS OldRowDataJson,
                audit_entries.new_row_data_json AS NewRowDataJson
            FROM (
                {{string.Join("\n                UNION ALL\n", unionParts)}}
            ) audit_entries
            LEFT JOIN public.app_user actor
                ON actor.id_user = audit_entries.changed_by_user_id
            WHERE {{whereClause}}
            {{orderByClause}}
            """;
    }

    private static string BuildStructuredAuditSelect(
        AuditLogTableDefinition source,
        bool includeDetails,
        string? dateDirection,
        bool limitPerSource)
    {
        var sourceOrder = AuditLogTableRegistry.GetSourceOrder(source.SourceTable);
        var recordPkExpression = BuildRecordPkJsonExpression(source);
        var currentRowDataExpression = BuildAuditRowDataJsonExpression("current_row");
        var parentRowDataExpression = BuildAuditRowDataJsonExpression("parent_row");
        var oldRowDataExpression = $$"""
            CASE
                WHEN current_row.operation = 'DELETE' THEN {{currentRowDataExpression}}
                WHEN current_row.operation = 'UPDATE' AND parent_row.id_audit IS NOT NULL THEN {{parentRowDataExpression}}
                ELSE NULL::jsonb
            END
            """;
        var newRowDataExpression = $$"""
            CASE
                WHEN current_row.operation IN ('INSERT', 'UPDATE') THEN {{currentRowDataExpression}}
                ELSE NULL::jsonb
            END
            """;
        var eventFingerprintExpression = $$"""
            md5(CONCAT_WS(
                CHR(31),
                current_row.changed_at::text,
                COALESCE(current_row.changed_by_user_id::text, ''),
                '{{source.SourceTable}}',
                current_row.operation,
                {{recordPkExpression}}::text,
                {{currentRowDataExpression}}::text,
                COALESCE(({{oldRowDataExpression}})::text, 'null'),
                COALESCE(({{newRowDataExpression}})::text, 'null')
            ))
            """;
        var detailsProjection = includeDetails
            ? $$"""
                {{recordPkExpression}}::text AS record_pk_json,
                {{currentRowDataExpression}}::text AS row_data_json,
                ({{oldRowDataExpression}})::text AS old_row_data_json,
                ({{newRowDataExpression}})::text AS new_row_data_json
                """
            : """
                NULL::text AS record_pk_json,
                NULL::text AS row_data_json,
                NULL::text AS old_row_data_json,
                NULL::text AS new_row_data_json
                """;
        var sourceFrom = limitPerSource
            ? $$"""
                (
                    SELECT *
                    FROM public.{{source.AuditTableName}}
                    ORDER BY changed_at {{dateDirection}}, id_audit {{dateDirection}}
                    LIMIT @CandidateLimit
                ) current_row
                """
            : $"public.{source.AuditTableName} current_row";

        return $$"""
            SELECT
                '{{source.SourceTable}}'::text AS source_table,
                {{sourceOrder}}::integer AS source_order,
                current_row.id_audit,
                current_row.parent_audit_id,
                current_row.operation,
                current_row.changed_at,
                current_row.changed_by_user_id,
                {{source.TargetNameSql}} AS target_name,
                {{source.TargetIdSql}} AS target_id,
                {{source.RelatedKindSql}} AS related_kind,
                {{source.RelatedIdSql}} AS related_id,
                {{eventFingerprintExpression}} AS event_fingerprint,
                {{detailsProjection}}
            FROM {{sourceFrom}}
            LEFT JOIN public.{{source.AuditTableName}} parent_row
                ON parent_row.id_audit = current_row.parent_audit_id
            """;
    }

    private static string BuildRecordPkJsonExpression(AuditLogTableDefinition source)
    {
        var parts = source.PrimaryKeyColumns
            .Select(column => $"'{column}', current_row.{column}")
            .ToArray();

        return parts.Length == 0 ? "'{}'::jsonb" : $"jsonb_build_object({string.Join(", ", parts)})";
    }

    private static string BuildAuditRowDataJsonExpression(string tableAlias)
    {
        return $"(to_jsonb({tableAlias}) {AuditRowJsonExclusions})";
    }

    private static string BuildAuditEventKeyExpression(string tableAlias)
    {
        return $"""
            CASE
                WHEN NULLIF({tableAlias}.related_kind, '') IS NOT NULL
                     AND NULLIF({tableAlias}.related_id, '') IS NOT NULL
                    THEN CONCAT(
                        'related|',
                        {tableAlias}.related_kind,
                        '|',
                        {tableAlias}.related_id,
                        '|',
                        COALESCE({tableAlias}.changed_by_user_id::text, ''),
                        '|',
                        {tableAlias}.changed_at::text)
                ELSE CONCAT('audit|', {tableAlias}.event_fingerprint)
            END
            """;
    }

    private static string BuildAuditEventPageOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "asc", StringComparison.Ordinal) ? "ASC" : "DESC";
        return sortBy switch
        {
            "user" => $"COALESCE(actor.full_name, actor.login, '') {direction}, event_groups.changed_at DESC, event_groups.source_order DESC, event_groups.id_audit DESC",
            "event" => $"CASE event_groups.operation WHEN 'INSERT' THEN 'Добавление' WHEN 'UPDATE' THEN 'Изменение' WHEN 'DELETE' THEN 'Удаление' ELSE event_groups.operation END {direction}, event_groups.changed_at DESC, event_groups.source_order DESC, event_groups.id_audit DESC",
            "description" => $"event_groups.source_table {direction}, event_groups.changed_at DESC, event_groups.source_order DESC, event_groups.id_audit DESC",
            _ => $"event_groups.changed_at {direction}, event_groups.source_order {direction}, event_groups.id_audit {direction}"
        };
    }

    private static string BuildAuditPageOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "asc", StringComparison.Ordinal) ? "ASC" : "DESC";
        return sortBy switch
        {
            "user" => $"COALESCE(actor.full_name, actor.login, '') {direction}, audit_entries.changed_at DESC, audit_entries.source_order DESC, audit_entries.id_audit DESC",
            "event" => $"CASE audit_entries.operation WHEN 'INSERT' THEN 'Добавление' WHEN 'UPDATE' THEN 'Изменение' WHEN 'DELETE' THEN 'Удаление' ELSE audit_entries.operation END {direction}, audit_entries.changed_at DESC, audit_entries.source_order DESC, audit_entries.id_audit DESC",
            "description" => $"audit_entries.source_table {direction}, audit_entries.changed_at DESC, audit_entries.source_order DESC, audit_entries.id_audit DESC",
            _ => $"audit_entries.changed_at {direction}, audit_entries.source_order {direction}, audit_entries.id_audit {direction}"
        };
    }
}
