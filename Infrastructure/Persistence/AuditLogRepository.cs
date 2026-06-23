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

    public int GetEventCount()
    {
        using var connection = _connectionFactory.CreateConnection();
        var metadata = LoadMetadata(connection);
        var countSql = AuditLogQueryBuilder.BuildAuditEventCountSql(metadata.AvailableAuditTables);
        return string.IsNullOrWhiteSpace(countSql)
            ? 0
            : ClampCount(connection.QuerySingle<long>(countSql));
    }

    public AuditLogReadResult GetPage(
        int currentPage,
        int pageSize,
        string sortBy,
        string sortDirection,
        bool includeDetails)
    {
        using var connection = _connectionFactory.CreateConnection();
        var metadata = LoadMetadata(connection);
        var pageSql = AuditLogQueryBuilder.BuildAuditPageSql(
            metadata.AvailableAuditTables,
            sortBy,
            sortDirection,
            includeDetails);
        var rows = string.IsNullOrWhiteSpace(pageSql)
            ? []
            : connection.Query<AuditLogRow>(
                pageSql,
                new
                {
                    Offset = Math.Max(0, (currentPage - 1) * pageSize),
                    Limit = pageSize,
                    CandidateLimit = Math.Max((currentPage - 1) * pageSize + pageSize, pageSize * 25)
                }).ToList();

        return new AuditLogReadResult(rows, metadata.SourceColumnOrders);
    }

    public AuditLogReadResult GetDetails(long idAudit, string? sourceTable)
    {
        using var connection = _connectionFactory.CreateConnection();
        var metadata = LoadMetadata(connection);
        var detailSql = AuditLogQueryBuilder.BuildAuditDetailSql(metadata.AvailableAuditTables, sourceTable);
        if (string.IsNullOrWhiteSpace(detailSql))
        {
            return new AuditLogReadResult([], metadata.SourceColumnOrders);
        }

        var directRows = connection.Query<AuditLogRow>(detailSql, new { IdAudit = idAudit }).ToList();
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

        var relatedRows = connection.Query<AuditLogRow>(
            relatedSql,
            new
            {
                primaryRow.ChangedAt,
                primaryRow.ChangedByUserId,
                primaryRow.RelatedKind,
                primaryRow.RelatedId
            }).ToList();

        return new AuditLogReadResult(
            relatedRows.Count > 0 ? relatedRows : directRows,
            metadata.SourceColumnOrders);
    }

    public AuditLogReadResult GetAll()
    {
        using var connection = _connectionFactory.CreateConnection();
        var metadata = LoadMetadata(connection);
        var auditSql = AuditLogQueryBuilder.BuildAuditSql(metadata.AvailableAuditTables);
        var rows = string.IsNullOrWhiteSpace(auditSql)
            ? []
            : connection.Query<AuditLogRow>(auditSql).ToList();
        return new AuditLogReadResult(rows, metadata.SourceColumnOrders);
    }

    public AuditAnswerContext? GetAnswerContext(int? organizationSurveyId, int? answerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (organizationSurveyId is > 0)
        {
            var context = connection.QuerySingleOrDefault<AuditAnswerContext>(
                AnswerContextByOrganizationSurveySql,
                new { IdOrganizationSurvey = organizationSurveyId.Value });
            if (context != null)
            {
                return context;
            }
        }

        return answerId is > 0
            ? connection.QuerySingleOrDefault<AuditAnswerContext>(AnswerContextByAnswerSql, new { IdAnswer = answerId.Value })
            : null;
    }

    private static AuditMetadata LoadMetadata(System.Data.IDbConnection connection)
    {
        var auditTableNames = AuditLogTableRegistry.Sources.Select(source => source.AuditTableName).ToArray();
        var sourceTableNames = AuditLogTableRegistry.Sources.Select(source => source.SourceTable).ToArray();
        var availableAuditTables = connection.Query<string>(ExistingAuditTablesSql, new { TableNames = auditTableNames })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceColumnOrders = connection.Query<SourceColumnOrderRow>(SourceColumnOrderSql, new { TableNames = sourceTableNames })
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
