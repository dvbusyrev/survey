using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Audit;
using MainProject.Application.Support;

namespace MainProject.Infrastructure.Persistence;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private const string ExistingAuditTablesSql = """
        SELECT table_name
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = ANY(@TableNames);
        """;

    private const string SourceColumnOrderSql = """
        SELECT
            table_name AS TableName,
            column_name AS ColumnName,
            ordinal_position AS OrdinalPosition
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = ANY(@TableNames)
        ORDER BY table_name, ordinal_position;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> GetEventCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var metadata = await LoadMetadataAsync(connection, cancellationToken);
        var countSql = AuditLogQueryBuilder.BuildAuditEventCountSql(metadata.AvailableAuditTables);
        return string.IsNullOrWhiteSpace(countSql)
            ? 0
            : ClampCount(await connection.QuerySingleAsync<long>(new CommandDefinition(countSql, cancellationToken: cancellationToken)));
    }

    public async Task<AuditLogReadResult> GetPageAsync(
        int currentPage,
        int pageSize,
        string sortBy,
        string sortDirection,
        bool includeDetails,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var metadata = await LoadMetadataAsync(connection, cancellationToken);
        var pageSql = AuditLogQueryBuilder.BuildAuditPageSql(
            metadata.AvailableAuditTables,
            sortBy,
            sortDirection,
            includeDetails);
        var rows = string.IsNullOrWhiteSpace(pageSql)
            ? []
            : (await connection.QueryAsync<AuditLogRow>(new CommandDefinition(
                pageSql,
                new
                {
                    Offset = Math.Max(0, (currentPage - 1) * pageSize),
                    Limit = pageSize,
                    CandidateLimit = Math.Max((currentPage - 1) * pageSize + pageSize, pageSize * 25)
                },
                cancellationToken: cancellationToken))).ToList();

        return new AuditLogReadResult(rows, metadata.SourceColumnOrders);
    }

    public async Task<AuditLogReadResult> GetDetailsAsync(
        long idAudit,
        string? sourceTable,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var metadata = await LoadMetadataAsync(connection, cancellationToken);
        var detailSql = AuditLogQueryBuilder.BuildAuditDetailSql(metadata.AvailableAuditTables, sourceTable);
        if (string.IsNullOrWhiteSpace(detailSql))
        {
            return new AuditLogReadResult([], metadata.SourceColumnOrders);
        }

        var directRows = (await connection.QueryAsync<AuditLogRow>(new CommandDefinition(
            detailSql,
            new { IdAudit = idAudit },
            cancellationToken: cancellationToken))).ToList();
        var primaryRow = directRows.FirstOrDefault();
        if (primaryRow == null
            || string.IsNullOrWhiteSpace(primaryRow.RelatedKind)
            || string.IsNullOrWhiteSpace(primaryRow.RelatedId))
        {
            return new AuditLogReadResult(directRows, metadata.SourceColumnOrders);
        }

        var relatedSql = AuditLogQueryBuilder.BuildAuditRelatedRowsSql(metadata.AvailableAuditTables);
        if (string.IsNullOrWhiteSpace(relatedSql))
        {
            return new AuditLogReadResult(directRows, metadata.SourceColumnOrders);
        }

        var relatedRows = (await connection.QueryAsync<AuditLogRow>(new CommandDefinition(
            relatedSql,
            new
            {
                primaryRow.ChangedAt,
                primaryRow.ChangedByUserId,
                primaryRow.RelatedKind,
                primaryRow.RelatedId
            },
            cancellationToken: cancellationToken))).ToList();

        return new AuditLogReadResult(
            relatedRows.Count > 0 ? relatedRows : directRows,
            metadata.SourceColumnOrders);
    }

    public async Task<AuditLogReadResult> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var metadata = await LoadMetadataAsync(connection, cancellationToken);
        var auditSql = AuditLogQueryBuilder.BuildAuditSql(metadata.AvailableAuditTables);
        var rows = string.IsNullOrWhiteSpace(auditSql)
            ? []
            : (await connection.QueryAsync<AuditLogRow>(new CommandDefinition(auditSql, cancellationToken: cancellationToken))).ToList();
        return new AuditLogReadResult(rows, metadata.SourceColumnOrders);
    }

    public async Task<AuditAnswerContext?> GetAnswerContextAsync(
        int? organizationSurveyId,
        int? answerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (organizationSurveyId is > 0)
        {
            var context = await connection.QuerySingleOrDefaultAsync<AuditAnswerContext>(new CommandDefinition(
                AnswerContextByOrganizationSurveySql,
                new { IdOrganizationSurvey = organizationSurveyId.Value },
                cancellationToken: cancellationToken));
            if (context != null)
            {
                return context;
            }
        }

        return answerId is > 0
            ? await connection.QuerySingleOrDefaultAsync<AuditAnswerContext>(new CommandDefinition(
                AnswerContextByAnswerSql,
                new { IdAnswer = answerId.Value },
                cancellationToken: cancellationToken))
            : null;
    }

    private static async Task<AuditMetadata> LoadMetadataAsync(
        System.Data.IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var auditTableNames = AuditLogTableRegistry.Sources.Select(source => source.AuditTableName).ToArray();
        var sourceTableNames = AuditLogTableRegistry.Sources.Select(source => source.SourceTable).ToArray();
        var availableAuditTables = (await connection.QueryAsync<string>(new CommandDefinition(
                ExistingAuditTablesSql,
                new { TableNames = auditTableNames },
                cancellationToken: cancellationToken)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceColumnOrders = (await connection.QueryAsync<SourceColumnOrderRow>(new CommandDefinition(
                SourceColumnOrderSql,
                new { TableNames = sourceTableNames },
                cancellationToken: cancellationToken)))
            .GroupBy(row => row.TableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.OrderBy(row => row.OrdinalPosition).Select(row => row.ColumnName).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        return new AuditMetadata(availableAuditTables, sourceColumnOrders);
    }

    private static int ClampCount(long count) => count > int.MaxValue ? int.MaxValue : (int)count;

    private const string AnswerContextByOrganizationSurveySql = """
        SELECT
            os.id_survey AS IdSurvey,
            s.name_survey AS SurveyName,
            os.id_organization AS IdOrganization,
            o.organization_name AS OrganizationName
        FROM public.organization_survey os
        LEFT JOIN public.survey s ON s.id_survey = os.id_survey
        LEFT JOIN public.organization o ON o.id_organization = os.id_organization
        WHERE os.id_organization_survey = @IdOrganizationSurvey
        LIMIT 1;
        """;

    private const string AnswerContextByAnswerSql = """
        SELECT
            os.id_survey AS IdSurvey,
            s.name_survey AS SurveyName,
            os.id_organization AS IdOrganization,
            o.organization_name AS OrganizationName
        FROM public.answer a
        INNER JOIN public.organization_survey os ON os.id_organization_survey = a.id_organization_survey
        LEFT JOIN public.survey s ON s.id_survey = os.id_survey
        LEFT JOIN public.organization o ON o.id_organization = os.id_organization
        WHERE a.id_answer = @IdAnswer
        LIMIT 1;
        """;

    private sealed record AuditMetadata(
        HashSet<string> AvailableAuditTables,
        IReadOnlyDictionary<string, IReadOnlyList<string>> SourceColumnOrders);

    private sealed class SourceColumnOrderRow
    {
        public string TableName { get; init; } = string.Empty;
        public string ColumnName { get; init; } = string.Empty;
        public int OrdinalPosition { get; init; }
    }
}
