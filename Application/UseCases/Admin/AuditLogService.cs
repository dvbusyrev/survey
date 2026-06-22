using System.Globalization;
using System.Data;
using System.Text;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.Support;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;
using Newtonsoft.Json.Linq;

namespace MainProject.Application.UseCases.Admin;

public sealed class AuditLogService : IAuditLogService
{
    private const string RedactedValue = "[REDACTED]";
    private const string AuditRowJsonExclusions = """
        - 'id_audit'
        - 'operation'
        - 'changed_at'
        - 'changed_by_user_id'
        - 'parent_audit_id'
        """;
    private static readonly StringComparer AuditLogStringComparer = StringComparer.Create(new CultureInfo("ru-RU"), true);
    private static readonly AuditSourceDefinition[] AuditSources =
    [
        new(
            "app_user",
            "app_user_l",
            ["id_user"],
            "COALESCE(NULLIF(current_row.full_name, ''), NULLIF(current_row.login, ''), 'ID ' || current_row.id_user::text)",
            "current_row.id_user::text",
            "NULL::text",
            "NULL::text"),
        new(
            "organization",
            "organization_l",
            ["id_organization"],
            "COALESCE(NULLIF(current_row.organization_name, ''), 'ID ' || current_row.id_organization::text)",
            "current_row.id_organization::text",
            "NULL::text",
            "NULL::text"),
        new(
            "survey",
            "survey_l",
            ["id_survey"],
            "COALESCE(NULLIF(current_row.name_survey, ''), 'Анкета ' || current_row.id_survey::text)",
            "current_row.id_survey::text",
            "'survey'::text",
            "current_row.id_survey::text"),
        new(
            "survey_question",
            "survey_question_l",
            ["id_question"],
            "COALESCE(NULLIF(current_row.question_text, ''), 'Вопрос ' || current_row.question_order::text || ' анкеты ' || current_row.id_survey::text)",
            "current_row.id_question::text",
            "'survey'::text",
            "current_row.id_survey::text"),
        new(
            "organization_survey",
            "organization_survey_l",
            ["id_organization_survey"],
            "'Организация ' || current_row.id_organization::text || ' / анкета ' || current_row.id_survey::text",
            "current_row.id_organization_survey::text",
            "'survey'::text",
            "current_row.id_survey::text"),
        new(
            "answer",
            "answer_l",
            ["id_answer"],
            "'Ответ ' || current_row.id_answer::text || ', назначение ' || current_row.id_organization_survey::text",
            "current_row.id_answer::text",
            "'answer'::text",
            "current_row.id_answer::text"),
        new(
            "answer_item",
            "answer_item_l",
            ["id_item"],
            "'Вопрос ' || current_row.question_order::text || ' ответа ' || current_row.id_answer::text",
            "current_row.id_item::text",
            "'answer'::text",
            "current_row.id_answer::text"),
        new(
            "auto_creation_config",
            "auto_creation_config_l",
            ["id_config"],
            "'Конфигурация ' || current_row.id_config::text",
            "current_row.id_config::text",
            "'auto_creation_config'::text",
            "current_row.id_config::text"),
        new(
            "survey_auto_creation_config",
            "survey_auto_creation_config_l",
            ["id_config", "id_survey"],
            "'Конфигурация ' || current_row.id_config::text || ' / анкета ' || current_row.id_survey::text",
            "current_row.id_config::text || ', ' || current_row.id_survey::text",
            "'auto_creation_config'::text",
            "current_row.id_config::text"),
        new(
            "email_config",
            "email_config_l",
            ["id_config"],
            "'Почтовая конфигурация ' || current_row.id_config::text",
            "current_row.id_config::text",
            "'email_config'::text",
            "current_row.id_config::text"),
        new(
            "theme_config",
            "theme_config_l",
            ["id_config"],
            "'Конфигурация темы ' || current_row.id_config::text",
            "current_row.id_config::text",
            "'theme_config'::text",
            "current_row.id_config::text")
    ];

    private static readonly Dictionary<string, int> AuditSourceOrder = AuditSources
        .Select((source, index) => new { source.SourceTable, Index = index })
        .ToDictionary(item => item.SourceTable, item => item.Index, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SurveyChainTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "survey",
        "survey_question",
        "organization_survey"
    };

    private static readonly HashSet<string> AnswerChainTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "answer",
        "answer_item"
    };

    private static readonly HashSet<string> AutoCreationConfigChainTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto_creation_config",
        "survey_auto_creation_config"
    };

    private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "hash_password",
        "password",
        "csp",
        "key_csp",
        "signature",
        "email",
        "recipient_emails",
        "smtp_user_name",
        "smtp_password",
        "from_address"
    };

    private static readonly HashSet<string> IgnoredChangedFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "date_update",
        "user_update"
    };

    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public AuditLogPageViewModel GetLogsPage(int currentPage, int pageSize, string? sortBy, string? sortDirection)
    {
        return GetLogsPageInternal(currentPage, pageSize, sortBy, sortDirection, stripDetails: true);
    }

    public Log? GetLogDetails(long idLog, string? sourceTable, int currentPage, int pageSize, string? sortBy, string? sortDirection)
    {
        if (idLog <= 0)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var metadata = LoadAuditMetadata(connection);
        var rows = GetAuditDetailRows(connection, metadata.AvailableAuditTables, idLog, sourceTable);
        if (rows.Count == 0)
        {
            return null;
        }

        var logs = MapAuditRows(rows, metadata.SourceColumnOrders, reconstructPreviousSnapshots: false);
        var log = logs.FirstOrDefault(log => log.IdLog == idLog) ?? logs.FirstOrDefault();
        if (log != null)
        {
            EnrichAnswerContext(connection, log);
        }

        return log;
    }

    private AuditLogPageViewModel GetLogsPageInternal(
        int currentPage,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        bool stripDetails)
    {
        var normalizedPageSize = pageSize > 0 ? pageSize : AppListPaging.DefaultPageSize;
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeSortDirection(null, normalizedSortBy);
        using var connection = _connectionFactory.CreateConnection();
        var metadata = LoadAuditMetadata(connection);
        var totalCount = GetAuditEventCount(connection, metadata.AvailableAuditTables);
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling((double)totalCount / normalizedPageSize);
        var normalizedPage = Math.Clamp(currentPage, 1, totalPages);
        var pageRows = GetAuditPageRows(
            connection,
            metadata.AvailableAuditTables,
            normalizedPage,
            normalizedPageSize,
            normalizedSortBy,
            normalizedSortDirection,
            includeDetails: !stripDetails);
        var pageLogs = SortLogsForPage(
                MapAuditRows(pageRows, metadata.SourceColumnOrders, reconstructPreviousSnapshots: false),
                normalizedSortBy,
                normalizedSortDirection)
            .Take(normalizedPageSize)
            .ToList();
        if (stripDetails)
        {
            StripLogDetails(pageLogs);
        }

        return new AuditLogPageViewModel
        {
            Logs = pageLogs,
            CurrentPage = normalizedPage,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PageSize = normalizedPageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty
        };
    }

    public IReadOnlyList<Log> GetLogs()
    {
        using var connection = _connectionFactory.CreateConnection();
        var metadata = LoadAuditMetadata(connection);
        var auditSql = BuildAuditSql(metadata.AvailableAuditTables);
        if (string.IsNullOrWhiteSpace(auditSql))
        {
            return Array.Empty<Log>();
        }

        var rows = connection.Query<AuditLogRow>(auditSql).ToList();
        var groupedLogs = MapAuditRows(rows, metadata.SourceColumnOrders, reconstructPreviousSnapshots: true);
        AssignDisplayLogIds(groupedLogs);

        return groupedLogs
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.IdLog)
            .ToList();
    }

    private static List<Log> MapAuditRows(
        IReadOnlyList<AuditLogRow> rows,
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourceColumnOrders,
        bool reconstructPreviousSnapshots)
    {
        var orderedRows = reconstructPreviousSnapshots
            ? rows
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.SourceOrder)
                .ThenBy(row => row.IdAudit)
                .ToList()
            : rows.ToList();
        var previousSnapshots = new Dictionary<string, JObject>(StringComparer.Ordinal);
        var logs = new List<Log>(orderedRows.Count);

        foreach (var row in orderedRows)
        {
            var recordPk = ParseJsonObject(row.RecordPkJson);
            var storedRowData = ParseJsonObject(row.RowDataJson);
            var oldRowData = ParseJsonObject(row.OldRowDataJson);
            var newRowData = ParseJsonObject(row.NewRowDataJson);
            var rowData = BuildEffectiveRowData(row.Operation, storedRowData, oldRowData, newRowData);
            var recordKey = BuildRecordKey(row.SourceTable, recordPk);

            previousSnapshots.TryGetValue(recordKey, out var previousRowData);
            var effectivePreviousRowData = string.Equals(row.Operation, "UPDATE", StringComparison.OrdinalIgnoreCase)
                ? oldRowData ?? (reconstructPreviousSnapshots ? previousRowData : null)
                : null;

            logs.Add(MapAuditLog(row, recordPk, rowData, effectivePreviousRowData, oldRowData, newRowData, sourceColumnOrders));

            if (!reconstructPreviousSnapshots || string.IsNullOrWhiteSpace(recordKey))
            {
                continue;
            }

            if (IsDeleteOperation(row.Operation))
            {
                previousSnapshots.Remove(recordKey);
                continue;
            }

            if (rowData != null)
            {
                previousSnapshots[recordKey] = (JObject)rowData.DeepClone();
            }
        }

        return GroupRelatedLogs(DeduplicateLogs(logs));
    }

    private static void StripLogDetails(IEnumerable<Log> logs)
    {
        foreach (var log in logs)
        {
            log.ExtraData = BuildLookupDetails(log.ExtraData as JObject);
        }
    }

    private static JObject? BuildLookupDetails(JObject? details)
    {
        if (details == null)
        {
            return null;
        }

        var lookup = new JObject
        {
            ["source_table"] = ExtractValue(details, "source_table"),
            ["target_id"] = ExtractValue(details, "target_id"),
            ["is_chain"] = details["is_chain"] ?? new JValue(false)
        };

        var sourceItem = details["items"] is JArray items
            ? items
                .OfType<JObject>()
                .OrderBy(GetItemSourceOrder)
                .ThenBy(GetItemAuditId)
                .FirstOrDefault()
            : details;

        lookup["audit_lookup_source_table"] = ExtractValue(sourceItem, "source_table");
        lookup["audit_lookup_id"] = ExtractValue(sourceItem, "audit_id") ?? ExtractValue(details, "audit_id");

        return lookup;
    }

    public string GenerateLogText(IEnumerable<Log> logs)
    {
        var orderedLogs = logs
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.IdLog)
            .ToList();
        var sb = new StringBuilder();

        sb.AppendLine("АИС Анкетирование. Журнал событий");
        sb.AppendLine($"Количество событий: {orderedLogs.Count}");
        sb.AppendLine();

        for (var index = 0; index < orderedLogs.Count; index++)
        {
            if (index > 0)
            {
                sb.AppendLine();
            }

            AppendExportBlock(sb, orderedLogs[index], index + 1);
        }

        return sb.ToString();
    }

    private static List<Log> SortLogsForPage(IReadOnlyList<Log> logs, string sortBy, string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.Ordinal);
        IOrderedEnumerable<Log> orderedLogs = sortBy switch
        {
            AuditLogSortFields.User => descending
                ? logs.OrderByDescending(log => GetUserValue(log), AuditLogStringComparer)
                : logs.OrderBy(log => GetUserValue(log), AuditLogStringComparer),
            AuditLogSortFields.Event => descending
                ? logs.OrderByDescending(log => GetEventValue(log), AuditLogStringComparer)
                : logs.OrderBy(log => GetEventValue(log), AuditLogStringComparer),
            AuditLogSortFields.Description => descending
                ? logs.OrderByDescending(log => GetDescriptionValue(log), AuditLogStringComparer)
                : logs.OrderBy(log => GetDescriptionValue(log), AuditLogStringComparer),
            _ => descending
                ? logs.OrderByDescending(log => log.Date)
                : logs.OrderBy(log => log.Date)
        };

        orderedLogs = descending
            ? orderedLogs.ThenByDescending(log => log.IdLog)
            : orderedLogs.ThenBy(log => log.IdLog);

        if (!string.Equals(sortBy, AuditLogSortFields.Date, StringComparison.Ordinal))
        {
            orderedLogs = orderedLogs.ThenByDescending(log => log.Date).ThenByDescending(log => log.IdLog);
        }

        return orderedLogs.ToList();
    }

    private static string NormalizeSortField(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            AuditLogSortFields.User => AuditLogSortFields.User,
            AuditLogSortFields.Event => AuditLogSortFields.Event,
            AuditLogSortFields.Description => AuditLogSortFields.Description,
            _ => AuditLogSortFields.Date
        };
    }

    private static string NormalizeSortDirection(string? sortDirection, string sortBy)
    {
        if (string.Equals(sortDirection?.Trim(), "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        if (string.Equals(sortDirection?.Trim(), "desc", StringComparison.OrdinalIgnoreCase))
        {
            return "desc";
        }

        return string.Equals(sortBy, AuditLogSortFields.Date, StringComparison.Ordinal)
            ? "desc"
            : "asc";
    }

    private static string GetUserValue(Log log) => string.IsNullOrWhiteSpace(log.NameUser) ? "Система" : log.NameUser!;

    private static string GetEventValue(Log log) => string.IsNullOrWhiteSpace(log.EventType) ? "—" : log.EventType!;

    private static string GetDescriptionValue(Log log) => string.IsNullOrWhiteSpace(log.Description) ? "—" : log.Description!;

    private static AuditMetadata LoadAuditMetadata(IDbConnection connection)
    {
        var availableAuditTables = connection.Query<string>(
                ExistingAuditTablesSql,
                new { TableNames = AuditSources.Select(source => source.AuditTableName).ToArray() })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceColumnOrders = connection.Query<SourceColumnOrderRow>(
                SourceColumnOrderSql,
                new { TableNames = AuditSources.Select(source => source.SourceTable).ToArray() })
            .GroupBy(row => row.TableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .OrderBy(row => row.OrdinalPosition)
                    .Select(row => row.ColumnName)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return new AuditMetadata(availableAuditTables, sourceColumnOrders);
    }

    private static int GetAuditEventCount(IDbConnection connection, IReadOnlyCollection<string> availableAuditTables)
    {
        var countSql = BuildAuditEventCountSql(availableAuditTables);
        if (string.IsNullOrWhiteSpace(countSql))
        {
            return 0;
        }

        var count = connection.QuerySingle<long>(countSql);
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    private static IReadOnlyList<AuditLogRow> GetAuditPageRows(
        IDbConnection connection,
        IReadOnlyCollection<string> availableAuditTables,
        int currentPage,
        int pageSize,
        string sortBy,
        string sortDirection,
        bool includeDetails)
    {
        var pageSql = BuildAuditPageSql(availableAuditTables, sortBy, sortDirection, includeDetails);
        if (string.IsNullOrWhiteSpace(pageSql))
        {
            return [];
        }

        var offset = Math.Max(0, (currentPage - 1) * pageSize);
        // A single event can contain several audit rows, so fetch a wider per-table window
        // before grouping date-sorted entries into the requested page.
        var candidateLimit = Math.Max(offset + pageSize, pageSize * 25);
        return connection.Query<AuditLogRow>(
                pageSql,
                new
                {
                    Offset = offset,
                    Limit = pageSize,
                    CandidateLimit = candidateLimit
                })
            .ToList();
    }

    private static IReadOnlyList<AuditLogRow> GetAuditDetailRows(
        IDbConnection connection,
        IReadOnlyCollection<string> availableAuditTables,
        long idAudit,
        string? sourceTable)
    {
        var detailSql = BuildAuditDetailSql(availableAuditTables, sourceTable);
        if (string.IsNullOrWhiteSpace(detailSql))
        {
            return [];
        }

        var directRows = connection.Query<AuditLogRow>(detailSql, new { IdAudit = idAudit }).ToList();
        var primaryRow = directRows.FirstOrDefault();
        if (primaryRow == null
            || string.IsNullOrWhiteSpace(primaryRow.RelatedKind)
            || string.IsNullOrWhiteSpace(primaryRow.RelatedId))
        {
            return directRows;
        }

        var relatedSql = BuildAuditRelatedRowsSql(availableAuditTables);
        if (string.IsNullOrWhiteSpace(relatedSql))
        {
            return directRows;
        }

        var relatedRows = connection.Query<AuditLogRow>(
                relatedSql,
                new
                {
                    primaryRow.ChangedAt,
                    primaryRow.ChangedByUserId,
                    primaryRow.RelatedKind,
                    primaryRow.RelatedId
                })
            .ToList();

        return relatedRows.Count > 0 ? relatedRows : directRows;
    }

    private static void EnrichAnswerContext(IDbConnection connection, Log log)
    {
        if (log.ExtraData is not JObject details)
        {
            return;
        }

        if (!ContainsSourceTable(details, "answer") && !ContainsSourceTable(details, "answer_item"))
        {
            return;
        }

        var idOrganizationSurvey = FindDetailValue(details, "id_organization_survey");
        var idAnswer = FindDetailValue(details, "id_answer");
        AnswerContextRow? context = null;

        if (int.TryParse(idOrganizationSurvey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var organizationSurveyId))
        {
            context = connection.QuerySingleOrDefault<AnswerContextRow>(
                AnswerContextByOrganizationSurveySql,
                new { IdOrganizationSurvey = organizationSurveyId });
        }

        if (context == null
            && int.TryParse(idAnswer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var answerId))
        {
            context = connection.QuerySingleOrDefault<AnswerContextRow>(
                AnswerContextByAnswerSql,
                new { IdAnswer = answerId });
        }

        if (context == null)
        {
            return;
        }

        details["semantic_context"] = new JObject
        {
            ["id_survey"] = context.IdSurvey,
            ["name_survey"] = context.SurveyName,
            ["completed_by"] = string.IsNullOrWhiteSpace(log.NameUser) ? "Система" : log.NameUser,
            ["id_organization"] = context.IdOrganization,
            ["organization_name"] = context.OrganizationName
        };
    }

    private static string? FindDetailValue(JObject details, string propertyName)
    {
        var value = ExtractValue(details, propertyName)
            ?? ExtractValue(details["record_pk"] as JObject, propertyName)
            ?? ExtractValue(details["row_data"] as JObject, propertyName)
            ?? ExtractValue(details["new_row_data"] as JObject, propertyName)
            ?? ExtractValue(details["old_row_data"] as JObject, propertyName);

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (details["items"] is not JArray items)
        {
            return null;
        }

        foreach (var item in items.OfType<JObject>())
        {
            value = FindDetailValue(item, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool ContainsSourceTable(JObject details, string sourceTable)
    {
        if (string.Equals(ExtractValue(details, "source_table"), sourceTable, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return details["items"] is JArray items
               && items
                   .OfType<JObject>()
                   .Any(item => string.Equals(ExtractValue(item, "source_table"), sourceTable, StringComparison.OrdinalIgnoreCase));
    }

    private static void AppendExportBlock(StringBuilder sb, Log log, int ordinal)
    {
        if (log.ExtraData is not JObject details)
        {
            sb.AppendLine($"Событие {ordinal}");
            sb.AppendLine(new string('=', 72));
            AppendExportField(sb, "Дата", log.Date.ToString("dd.MM.yyyy HH:mm:ss"));
            AppendExportField(sb, "Пользователь", BuildActorLabel(log));
            AppendExportField(sb, "Событие", string.IsNullOrWhiteSpace(log.EventType) ? "—" : log.EventType!);
            AppendExportField(sb, "Описание", string.IsNullOrWhiteSpace(log.Description) ? "Событие без описания." : log.Description!);
            return;
        }

        var targetTable = ExtractValue(details, "source_table_name") ?? log.TargetType ?? "Объект";
        var sourceTable = ExtractValue(details, "source_table") ?? targetTable;
        var targetId = ExtractValue(details, "target_id") ?? "—";
        var targetName = string.IsNullOrWhiteSpace(log.TargetName) ? targetTable : log.TargetName!;
        var operationName = ExtractValue(details, "operation_name") ?? log.EventType ?? "Изменение";
        var description = !string.IsNullOrWhiteSpace(log.Description)
            ? log.Description!
            : BuildDescription(ExtractValue(details, "operation") ?? operationName, sourceTable, targetName, new JArray(), null);

        sb.AppendLine($"Событие {ordinal}");
        sb.AppendLine(new string('=', 72));
        AppendExportField(sb, "Дата", log.Date.ToString("dd.MM.yyyy HH:mm:ss"));
        AppendExportField(sb, "Пользователь", BuildActorLabel(log));
        AppendExportField(sb, "Событие", operationName);
        AppendExportField(sb, "Описание", description);

        if (details.Value<bool?>("is_chain") == true)
        {
            var chainTables = ExtractChainTables(details);
            AppendExportField(sb, chainTables.Count <= 1 ? "Таблица" : "Таблицы", string.Join(", ", chainTables));
            AppendExportField(sb, "Запись", targetName);
            AppendExportField(sb, "ID записи", targetId);
            AppendChainItems(sb, details);
            return;
        }

        AppendExportField(sb, "Таблица", sourceTable);
        AppendExportField(sb, "Запись", targetName);
        AppendExportField(sb, "ID записи", targetId);

        if (string.Equals(ExtractValue(details, "operation"), "UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            AppendChangedFields(sb, details);
        }
    }

    private static void AppendExportField(StringBuilder sb, string label, string value)
    {
        sb.Append(label);
        sb.Append(": ");
        sb.AppendLine(string.IsNullOrWhiteSpace(value) ? "—" : value);
    }

    private static string BuildActorLabel(Log log)
    {
        if (!log.IdUser.HasValue)
        {
            return "Система";
        }

        var actorName = string.IsNullOrWhiteSpace(log.NameUser) ? "Неизвестно" : log.NameUser!;
        return $"{actorName} (ID = {log.IdUser.Value.ToString(CultureInfo.InvariantCulture)})";
    }

    private static List<string> ExtractChainTables(JObject details)
    {
        var tables = (details["items"] as JArray)?
            .OfType<JObject>()
            .Select(item => ExtractValue(item, "source_table"))
            .Where(table => !string.IsNullOrWhiteSpace(table))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(table => GetAuditSourceOrder(table))
            .ThenBy(table => table, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tables is { Count: > 0 })
        {
            return tables!;
        }

        var sourceTable = ExtractValue(details, "source_table");
        return [string.IsNullOrWhiteSpace(sourceTable) ? "неопределённая таблица" : sourceTable];
    }

    private static void AppendChainItems(StringBuilder sb, JObject details)
    {
        if (details["items"] is not JArray items || items.Count == 0)
        {
            return;
        }

        sb.AppendLine("Связанные записи:");

        var relatedItems = items
            .OfType<JObject>()
            .Select(item => new
            {
                SourceTable = ExtractValue(item, "source_table") ?? "неопределённая таблица",
                TargetName = ExtractValue(item, "target_name") ?? "Нет данных",
                TargetId = ExtractValue(item, "target_id") ?? BuildRecordIdentifier(item["record_pk"] as JObject)
            })
            .GroupBy(
                item => string.Join("\u001f", item.SourceTable, item.TargetName, item.TargetId),
                StringComparer.Ordinal)
            .Select(group => group.First());

        foreach (var item in relatedItems)
        {
            sb.Append("- ");
            sb.Append(item.SourceTable);
            sb.Append(": ");
            sb.Append(item.TargetName);

            if (!string.IsNullOrWhiteSpace(item.TargetId) && !string.Equals(item.TargetId, "—", StringComparison.Ordinal))
            {
                sb.Append(" (ID: ");
                sb.Append(item.TargetId);
                sb.Append(')');
            }

            sb.AppendLine();
        }
    }

    private static void AppendChangedFields(StringBuilder sb, JObject details)
    {
        sb.AppendLine("Изменения:");

        if (details["changed_fields"] is JArray changedFields && changedFields.Count > 0)
        {
            foreach (var change in changedFields.OfType<JObject>())
            {
                var fieldName = ExtractValue(change, "field") ?? "unknown";
                var newValue = FormatTokenValue(change["new_value"]);
                var oldValue = FormatTokenValue(change["old_value"]);
                sb.Append("- ");
                sb.Append(fieldName);
                sb.Append(": ");
                sb.Append(oldValue);
                sb.Append(" -> ");
                sb.Append(newValue);
                sb.AppendLine();
            }

            return;
        }

        var changeReason = ExtractValue(details, "change_reason");
        sb.Append("- ");
        sb.Append(string.IsNullOrWhiteSpace(changeReason)
            ? "не определены."
            : $"не определены: {changeReason}.");
        sb.AppendLine();
    }

    private static string BuildUpdateDetailsText(JObject details)
    {
        if (details["changed_fields"] is JArray changedFields && changedFields.Count > 0)
        {
            return $"Изменил атрибуты: {FormatChangedFields(changedFields)}.";
        }

        var changeReason = ExtractValue(details, "change_reason");
        if (!string.IsNullOrWhiteSpace(changeReason))
        {
            return $"Изменённые атрибуты не определены: {changeReason}.";
        }

        return "Изменённые атрибуты не определены.";
    }

    private static string FormatChangedFields(JArray changedFields)
    {
        return string.Join(
            "; ",
            changedFields
                .OfType<JObject>()
                .Select(change =>
                {
                    var fieldName = ExtractValue(change, "field") ?? "unknown";
                    var newValue = FormatTokenValue(change["new_value"]);
                    var oldValue = FormatTokenValue(change["old_value"]);
                    return $"{fieldName} = {newValue} (старое значение: {oldValue})";
                }));
    }

    private static Log MapAuditLog(
        AuditLogRow row,
        JObject? recordPk,
        JObject? rowData,
        JObject? previousRowData,
        JObject? oldRowData,
        JObject? newRowData,
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourceColumnOrders)
    {
        var entityName = GetEntityName(row.SourceTable);
        var operationName = GetOperationName(row.Operation);
        var operationVerb = GetOperationVerb(row.Operation);
        var targetName = !string.IsNullOrWhiteSpace(row.TargetName)
            ? row.TargetName!
            : BuildTargetName(row.SourceTable, recordPk, rowData);
        var changedFields = BuildChangedFields(rowData, previousRowData);
        var changeReason = BuildChangeReason(row.Operation, previousRowData, changedFields);

        return new Log
        {
            IdLog = row.IdAudit,
            IdUser = row.ChangedByUserId,
            TargetType = entityName,
            EventType = operationName,
            Date = row.ChangedAt,
            Description = BuildDescription(row.Operation, row.SourceTable, targetName, changedFields, changeReason),
            ExtraData = BuildDetails(row, entityName, operationVerb, targetName, recordPk, rowData, previousRowData, oldRowData, newRowData, changedFields, changeReason, sourceColumnOrders),
            NameUser = !string.IsNullOrWhiteSpace(row.ActorName)
                ? row.ActorName
                : row.ChangedByUserId.HasValue
                    ? $"ID {row.ChangedByUserId}"
                    : "Система",
            TargetName = targetName
        };
    }

    private static List<Log> GroupRelatedLogs(IReadOnlyList<Log> logs)
    {
        if (logs.Count == 0)
        {
            return [];
        }

        var candidates = logs
            .Select((log, index) => new AuditLogGroupCandidate(index, log, BuildRelatedGroupKey(log)))
            .ToList();
        var groupedIndexes = new HashSet<int>();
        var result = new List<Log>(logs.Count);

        foreach (var group in candidates
                     .Where(candidate => candidate.GroupKey != null)
                     .GroupBy(candidate => candidate.GroupKey!))
        {
            var relatedCandidates = group
                .OrderBy(candidate => candidate.Index)
                .ToList();

            if (relatedCandidates.Count <= 1)
            {
                continue;
            }

            foreach (var candidate in relatedCandidates)
            {
                groupedIndexes.Add(candidate.Index);
            }

            result.Add(BuildRelatedLog(
                relatedCandidates.Select(candidate => candidate.Log).ToList(),
                group.Key));
        }

        foreach (var candidate in candidates)
        {
            if (!groupedIndexes.Contains(candidate.Index))
            {
                result.Add(candidate.Log);
            }
        }

        return SortLogs(result);
    }

    private static List<Log> DeduplicateLogs(IReadOnlyList<Log> logs)
    {
        if (logs.Count <= 1)
        {
            return logs.ToList();
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Log>(logs.Count);

        foreach (var log in logs)
        {
            var key = BuildLogDeduplicationKey(log);
            if (string.IsNullOrWhiteSpace(key) || seenKeys.Add(key))
            {
                result.Add(log);
            }
        }

        return result;
    }

    private static string BuildLogDeduplicationKey(Log log)
    {
        if (log.ExtraData is not JObject details)
        {
            return string.Empty;
        }

        return string.Join(
            "\u001f",
            log.Date.Ticks.ToString(CultureInfo.InvariantCulture),
            log.IdUser?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ExtractValue(details, "source_table") ?? string.Empty,
            ExtractValue(details, "operation") ?? string.Empty,
            NormalizeTokenForKey(details["record_pk"] ?? new JObject()).ToString(Newtonsoft.Json.Formatting.None),
            NormalizeTokenForKey(details["row_data"] ?? new JObject()).ToString(Newtonsoft.Json.Formatting.None),
            NormalizeTokenForKey(details["old_row_data"] ?? new JObject()).ToString(Newtonsoft.Json.Formatting.None),
            NormalizeTokenForKey(details["new_row_data"] ?? new JObject()).ToString(Newtonsoft.Json.Formatting.None));
    }

    private static Log BuildRelatedLog(IReadOnlyList<Log> relatedLogs, AuditLogGroupKey groupKey)
    {
        var orderedLogs = SortLogs(relatedLogs);
        var operation = DetermineGroupOperation(orderedLogs);
        var targetType = GetGroupTargetType(groupKey.Kind);
        var primaryLog = ChoosePrimaryLog(orderedLogs, groupKey.Kind);
        var targetName = BuildGroupTargetName(primaryLog, groupKey);
        var eventDate = orderedLogs[0].Date;

        return new Log
        {
            IdLog = orderedLogs[0].IdLog,
            IdUser = primaryLog.IdUser,
            TargetType = targetType,
            EventType = GetOperationName(operation),
            Date = eventDate,
            Description = BuildRelatedDescription(orderedLogs, groupKey.Kind, targetName),
            ExtraData = BuildRelatedDetails(orderedLogs, groupKey, operation, targetType, targetName),
            NameUser = primaryLog.NameUser,
            TargetName = targetName
        };
    }

    private static JObject BuildRelatedDetails(
        IReadOnlyList<Log> orderedLogs,
        AuditLogGroupKey groupKey,
        string operation,
        string targetType,
        string targetName)
    {
        var items = new JArray();

        foreach (var log in orderedLogs)
        {
            if (log.ExtraData is not JObject details)
            {
                continue;
            }

            var item = (JObject)details.DeepClone();
            item["log_description"] = log.Description;
            items.Add(item);
        }

        return new JObject
        {
            ["is_chain"] = true,
            ["operation"] = operation,
            ["operation_name"] = GetOperationName(operation),
            ["operation_verb"] = GetOperationVerb(operation),
            ["source_table"] = groupKey.Kind,
            ["source_table_name"] = targetType,
            ["target_name"] = targetName,
            ["target_id"] = groupKey.RelatedId,
            ["items"] = items
        };
    }

    private static string BuildRelatedDescription(IReadOnlyList<Log> orderedLogs, string groupKind, string targetName)
    {
        var tables = orderedLogs
            .Select(log => ExtractValue(log.ExtraData as JObject, "source_table"))
            .Where(table => !string.IsNullOrWhiteSpace(table))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(table => GetAuditSourceOrder(table))
            .ThenBy(table => table, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tables.Count == 0)
        {
            return "записей из неопределённых таблиц";
        }

        return tables.Count == 1
            ? $"записей из таблицы {tables[0]}"
            : $"записей из таблиц {string.Join(", ", tables)}";
    }

    private static AuditLogGroupKey? BuildRelatedGroupKey(Log log)
    {
        if (log.ExtraData is not JObject details)
        {
            return null;
        }

        var sourceTable = ExtractValue(details, "source_table");
        if (string.IsNullOrWhiteSpace(sourceTable))
        {
            return null;
        }

        var directRelatedKind = ExtractValue(details, "related_kind");
        var directRelatedId = ExtractValue(details, "related_id");
        if (!string.IsNullOrWhiteSpace(directRelatedKind) && !string.IsNullOrWhiteSpace(directRelatedId))
        {
            return new AuditLogGroupKey(directRelatedKind!, directRelatedId!, log.IdUser, log.Date.Ticks);
        }

        var recordPk = details["record_pk"] as JObject;
        var rowData = details["row_data"] as JObject;

        if (AutoCreationConfigChainTables.Contains(sourceTable))
        {
            var configId = ExtractConfigId(recordPk, rowData);
            return string.IsNullOrWhiteSpace(configId)
                ? null
                : new AuditLogGroupKey("auto_creation_config", configId, log.IdUser, log.Date.Ticks);
        }

        if (AnswerChainTables.Contains(sourceTable))
        {
            var answerId = ExtractAnswerId(sourceTable, recordPk, rowData);
            return string.IsNullOrWhiteSpace(answerId)
                ? null
                : new AuditLogGroupKey("answer", answerId, log.IdUser, log.Date.Ticks);
        }

        if (SurveyChainTables.Contains(sourceTable))
        {
            var surveyId = ExtractSurveyId(sourceTable, recordPk, rowData);
            return string.IsNullOrWhiteSpace(surveyId)
                ? null
                : new AuditLogGroupKey("survey", surveyId, log.IdUser, log.Date.Ticks);
        }

        if (string.Equals(sourceTable, "email_config", StringComparison.OrdinalIgnoreCase))
        {
            var configId = ExtractConfigId(recordPk, rowData);
            return string.IsNullOrWhiteSpace(configId)
                ? null
                : new AuditLogGroupKey("email_config", configId, log.IdUser, log.Date.Ticks);
        }

        if (string.Equals(sourceTable, "theme_config", StringComparison.OrdinalIgnoreCase))
        {
            var configId = ExtractConfigId(recordPk, rowData);
            return string.IsNullOrWhiteSpace(configId)
                ? null
                : new AuditLogGroupKey("theme_config", configId, log.IdUser, log.Date.Ticks);
        }

        return null;
    }

    private static string? ExtractSurveyId(string sourceTable, JObject? recordPk, JObject? rowData)
    {
        return sourceTable switch
        {
            "survey" => ExtractValue(rowData, "id_survey") ?? ExtractValue(recordPk, "id_survey"),
            "survey_question" => ExtractValue(rowData, "id_survey") ?? ExtractValue(recordPk, "id_survey"),
            "organization_survey" => ExtractValue(rowData, "id_survey") ?? ExtractValue(recordPk, "id_survey"),
            _ => null
        };
    }

    private static string? ExtractAnswerId(string sourceTable, JObject? recordPk, JObject? rowData)
    {
        return sourceTable switch
        {
            "answer" => ExtractValue(rowData, "id_answer") ?? ExtractValue(recordPk, "id_answer"),
            "answer_item" => ExtractValue(rowData, "id_answer") ?? ExtractValue(recordPk, "id_answer"),
            _ => null
        };
    }

    private static string? ExtractConfigId(JObject? recordPk, JObject? rowData)
    {
        return ExtractValue(rowData, "id_config") ?? ExtractValue(recordPk, "id_config");
    }

    private static string DetermineGroupOperation(IReadOnlyList<Log> orderedLogs)
    {
        var operations = orderedLogs
            .Select(log => ExtractValue(log.ExtraData as JObject, "operation")?.ToUpperInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return operations.Length == 1 ? operations[0]! : "UPDATE";
    }

    private static Log ChoosePrimaryLog(IReadOnlyList<Log> orderedLogs, string groupKind)
    {
        var preferredSourceTable = groupKind switch
        {
            "survey" => "survey",
            "answer" => "answer",
            "auto_creation_config" => "auto_creation_config",
            "email_config" => "email_config",
            "theme_config" => "theme_config",
            _ => string.Empty
        };

        return orderedLogs.FirstOrDefault(log =>
            string.Equals(ExtractValue(log.ExtraData as JObject, "source_table"), preferredSourceTable, StringComparison.OrdinalIgnoreCase))
            ?? orderedLogs[0];
    }

    private static string BuildGroupTargetName(Log primaryLog, AuditLogGroupKey groupKey)
    {
        if (!string.IsNullOrWhiteSpace(primaryLog.TargetName)
            && !string.Equals(primaryLog.TargetName, "Нет данных", StringComparison.OrdinalIgnoreCase))
        {
            return primaryLog.TargetName!;
        }

        return groupKey.Kind switch
        {
            "survey" => $"Анкета {groupKey.RelatedId}",
            "answer" => $"Ответ {groupKey.RelatedId}",
            "auto_creation_config" => $"Конфигурация {groupKey.RelatedId}",
            "email_config" => $"Почтовая конфигурация {groupKey.RelatedId}",
            "theme_config" => $"Конфигурация темы {groupKey.RelatedId}",
            _ => groupKey.RelatedId
        };
    }

    private static string GetGroupTargetType(string groupKind)
    {
        return groupKind switch
        {
            "survey" => "Анкета",
            "answer" => "Ответ",
            "auto_creation_config" => "Настройка автосоздания",
            "email_config" => "Почтовая настройка",
            "theme_config" => "Настройка темы",
            _ => "Запись"
        };
    }

    private static List<Log> SortLogs(IEnumerable<Log> logs)
    {
        return logs
            .OrderBy(log => log.Date)
            .ThenBy(GetAuditSourceOrder)
            .ThenBy(GetAuditId)
            .ToList();
    }

    private static int GetAuditSourceOrder(Log log)
    {
        if (log.ExtraData is not JObject details)
        {
            return int.MaxValue;
        }

        var sourceOrder = ExtractValue(details, "audit_source_order");
        if (int.TryParse(sourceOrder, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        var sourceTable = ExtractValue(details, "source_table");
        return sourceTable != null && AuditSourceOrder.TryGetValue(sourceTable, out var tableOrder)
            ? tableOrder
            : int.MaxValue;
    }

    private static int GetAuditSourceOrder(string? sourceTable)
    {
        return !string.IsNullOrWhiteSpace(sourceTable)
               && AuditSourceOrder.TryGetValue(sourceTable, out var tableOrder)
            ? tableOrder
            : int.MaxValue;
    }

    private static int GetItemSourceOrder(JObject? item)
    {
        var sourceOrder = ExtractValue(item, "audit_source_order");
        if (int.TryParse(sourceOrder, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return GetAuditSourceOrder(ExtractValue(item, "source_table"));
    }

    private static long GetItemAuditId(JObject? item)
    {
        var auditId = ExtractValue(item, "audit_id");
        return long.TryParse(auditId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : long.MaxValue;
    }

    private static long GetAuditId(Log log)
    {
        if (log.ExtraData is not JObject details)
        {
            return log.IdLog;
        }

        var auditId = ExtractValue(details, "audit_id");
        return long.TryParse(auditId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : log.IdLog;
    }

    private static void AssignDisplayLogIds(IReadOnlyList<Log> logs)
    {
        var nextId = 1L;
        foreach (var log in SortLogs(logs))
        {
            log.IdLog = nextId++;
        }
    }

    private static string BuildDescription(
        string operation,
        string sourceTable,
        string targetName,
        JArray changedFields,
        string? changeReason)
    {
        return string.IsNullOrWhiteSpace(sourceTable)
            ? "записи из неопределённой таблицы"
            : $"записи из таблицы {sourceTable}";
    }

    private static JObject BuildDetails(
        AuditLogRow row,
        string entityName,
        string operationVerb,
        string targetName,
        JObject? recordPk,
        JObject? rowData,
        JObject? previousRowData,
        JObject? oldRowData,
        JObject? newRowData,
        JArray changedFields,
        string? changeReason,
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourceColumnOrders)
    {
        return new JObject
        {
            ["audit_id"] = row.IdAudit,
            ["audit_source_order"] = row.SourceOrder,
            ["operation"] = row.Operation,
            ["operation_name"] = GetOperationName(row.Operation),
            ["operation_verb"] = operationVerb,
            ["source_table"] = row.SourceTable,
            ["source_table_name"] = entityName,
            ["target_name"] = targetName,
            ["target_id"] = string.IsNullOrWhiteSpace(row.TargetId) ? BuildRecordIdentifier(recordPk) : row.TargetId,
            ["parent_audit_id"] = row.ParentAuditId.HasValue ? row.ParentAuditId.Value : JValue.CreateNull(),
            ["related_kind"] = row.RelatedKind,
            ["related_id"] = row.RelatedId,
            ["column_order"] = BuildColumnOrder(row.SourceTable, recordPk, rowData, previousRowData, oldRowData, newRowData, sourceColumnOrders),
            ["record_pk"] = SanitizeToken(recordPk) ?? new JObject(),
            ["row_data"] = SanitizeToken(rowData) ?? new JObject(),
            ["previous_row_data"] = SanitizeToken(previousRowData),
            ["old_row_data"] = SanitizeToken(oldRowData),
            ["new_row_data"] = SanitizeToken(newRowData),
            ["changed_fields"] = changedFields,
            ["change_reason"] = changeReason
        };
    }

    private static JArray BuildColumnOrder(
        string sourceTable,
        JObject? recordPk,
        JObject? rowData,
        JObject? previousRowData,
        JObject? oldRowData,
        JObject? newRowData,
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourceColumnOrders)
    {
        var presentColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddPropertyNames(presentColumns, recordPk);
        AddPropertyNames(presentColumns, rowData);
        AddPropertyNames(presentColumns, previousRowData);
        AddPropertyNames(presentColumns, oldRowData);
        AddPropertyNames(presentColumns, newRowData);

        if (presentColumns.Count == 0)
        {
            return new JArray();
        }

        var orderedColumns = new List<string>();
        if (sourceColumnOrders.TryGetValue(sourceTable, out var databaseColumns))
        {
            orderedColumns.AddRange(databaseColumns.Where(presentColumns.Contains));
        }

        orderedColumns.AddRange(
            presentColumns
                .Where(column => !orderedColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                .OrderBy(column => column, StringComparer.OrdinalIgnoreCase));

        return new JArray(orderedColumns);
    }

    private static void AddPropertyNames(ISet<string> target, JObject? source)
    {
        if (source == null)
        {
            return;
        }

        foreach (var property in source.Properties())
        {
            if (!string.IsNullOrWhiteSpace(property.Name))
            {
                target.Add(property.Name);
            }
        }
    }

    private static JArray BuildChangedFields(JObject? currentRowData, JObject? previousRowData)
    {
        var changedFields = new JArray();

        if (currentRowData == null || previousRowData == null)
        {
            return changedFields;
        }

        var propertyNames = currentRowData.Properties()
            .Select(property => property.Name)
            .Concat(previousRowData.Properties().Select(property => property.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        foreach (var propertyName in propertyNames)
        {
            if (IgnoredChangedFieldNames.Contains(propertyName))
            {
                continue;
            }

            currentRowData.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var currentValue);
            previousRowData.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var previousValue);

            if (JToken.DeepEquals(currentValue, previousValue))
            {
                continue;
            }

            changedFields.Add(new JObject
            {
                ["field"] = propertyName,
                ["new_value"] = SanitizeToken(currentValue) ?? JValue.CreateNull(),
                ["old_value"] = SanitizeToken(previousValue) ?? JValue.CreateNull()
            });
        }

        return changedFields;
    }

    private static string? BuildChangeReason(string operation, JObject? previousRowData, JArray changedFields)
    {
        if (!string.Equals(operation, "UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (previousRowData == null)
        {
            return "предыдущая версия записи не найдена в журнале";
        }

        if (changedFields.Count == 0)
        {
            return "отличия от предыдущего снимка записи не найдены";
        }

        return null;
    }

    private static string BuildRecordKey(string sourceTable, JObject? recordPk)
    {
        if (recordPk == null || !recordPk.Properties().Any())
        {
            return string.Empty;
        }

        var normalizedPk = NormalizeObjectForKey(recordPk);
        return $"{sourceTable}:{normalizedPk.ToString(Newtonsoft.Json.Formatting.None)}";
    }

    private static JObject? BuildEffectiveRowData(
        string operation,
        JObject? storedRowData,
        JObject? oldRowData,
        JObject? newRowData)
    {
        if (string.Equals(operation, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return oldRowData ?? storedRowData;
        }

        return newRowData ?? storedRowData;
    }

    private static JObject NormalizeObjectForKey(JObject source)
    {
        var normalized = new JObject();

        foreach (var property in source.Properties().OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase))
        {
            normalized[property.Name] = NormalizeTokenForKey(property.Value);
        }

        return normalized;
    }

    private static JToken NormalizeTokenForKey(JToken token)
    {
        return token switch
        {
            JObject obj => NormalizeObjectForKey(obj),
            JArray array => new JArray(array.Select(NormalizeTokenForKey)),
            _ => token.DeepClone()
        };
    }

    private static bool IsDeleteOperation(string operation)
    {
        return string.Equals(operation, "DELETE", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRecordIdentifier(JObject? recordPk)
    {
        if (recordPk == null || !recordPk.Properties().Any())
        {
            return "—";
        }

        var properties = recordPk.Properties().ToList();
        if (properties.Count == 1)
        {
            return FormatTokenValue(properties[0].Value);
        }

        return string.Join(
            ", ",
            properties.Select(property => $"{NormalizeAuditPropertyName(property.Name)}={FormatTokenValue(property.Value)}"));
    }

    private static string FormatTokenValue(JToken? token)
    {
        if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
        {
            return "пусто";
        }

        return token.Type switch
        {
            JTokenType.String => FormatStringValue(token.Value<string>()),
            JTokenType.Boolean => token.Value<bool>() ? "true" : "false",
            JTokenType.Integer => token.Value<long>().ToString(CultureInfo.InvariantCulture),
            JTokenType.Float => token.Value<double>().ToString(CultureInfo.InvariantCulture),
            JTokenType.Date => token.Value<DateTime>().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            JTokenType.Object or JTokenType.Array => token.ToString(Newtonsoft.Json.Formatting.None),
            _ => token.ToString(Newtonsoft.Json.Formatting.None)
        };
    }

    private static string FormatStringValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "пусто";
        }

        if (string.Equals(value, RedactedValue, StringComparison.Ordinal))
        {
            return value;
        }

        if (TryFormatDateValue(value, out var formattedDateValue))
        {
            return formattedDateValue;
        }

        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static bool TryFormatDateValue(string value, out string formattedValue)
    {
        formattedValue = string.Empty;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            formattedValue = dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        if (!value.Contains('T', StringComparison.Ordinal) && !value.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedValue))
        {
            return false;
        }

        formattedValue = parsedValue.TimeOfDay == TimeSpan.Zero
            ? parsedValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : parsedValue.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return true;
    }

    private static JToken? SanitizeToken(JToken? token)
    {
        if (token == null)
        {
            return null;
        }

        return token switch
        {
            JObject obj => SanitizeObject(obj),
            JArray array => new JArray(array.Select(item => SanitizeToken(item) ?? JValue.CreateNull())),
            _ => token.DeepClone()
        };
    }

    private static JObject SanitizeObject(JObject source)
    {
        var sanitized = new JObject();

        foreach (var property in source.Properties())
        {
            sanitized[property.Name] = SensitiveFieldNames.Contains(property.Name)
                ? RedactedValue
                : SanitizeToken(property.Value) ?? JValue.CreateNull();
        }

        return sanitized;
    }

    private static string BuildTargetName(string sourceTable, JObject? recordPk, JObject? rowData)
    {
        return sourceTable switch
        {
            "app_user" => FirstNonEmpty(rowData?["full_name"], rowData?["login"], rowData?["name_user"]) ?? BuildIdLabel(recordPk, "id_user", "ID"),
            "organization" => FirstNonEmpty(rowData?["organization_name"]) ?? BuildIdLabel(recordPk, "id_organization", "ID"),
            "survey" => FirstNonEmpty(rowData?["name_survey"]) ?? BuildIdLabel(recordPk, "id_survey", "ID"),
            "survey_question" => BuildSurveyQuestionTarget(recordPk, rowData),
            "answer" => BuildAnswerTarget(recordPk, rowData),
            "answer_item" => BuildAnswerItemTarget(recordPk, rowData),
            "organization_survey" => BuildAssignmentTarget(recordPk, rowData),
            "auto_creation_config" => BuildIdLabel(recordPk, "id_config", "Конфигурация"),
            "survey_auto_creation_config" => BuildSurveyAutoCreationTarget(recordPk, rowData),
            "email_config" => BuildIdLabel(recordPk, "id_config", "Почтовая конфигурация"),
            "theme_config" => BuildIdLabel(recordPk, "id_config", "Конфигурация темы"),
            _ => BuildGenericTarget(recordPk)
        };
    }

    private static string BuildSurveyQuestionTarget(JObject? recordPk, JObject? rowData)
    {
        var questionText = FirstNonEmpty(rowData?["question_text"]);
        if (!string.IsNullOrWhiteSpace(questionText))
        {
            return questionText;
        }

        var questionOrder = ExtractValue(rowData, "question_order") ?? ExtractValue(recordPk, "question_order");
        var surveyId = ExtractValue(rowData, "id_survey") ?? ExtractValue(recordPk, "id_survey");

        if (!string.IsNullOrWhiteSpace(questionOrder) && !string.IsNullOrWhiteSpace(surveyId))
        {
            return $"Вопрос {questionOrder} анкеты {surveyId}";
        }

        return BuildIdLabel(recordPk, "id_question", "Вопрос");
    }

    private static string BuildAnswerTarget(JObject? recordPk, JObject? rowData)
    {
        var answerId = ExtractValue(recordPk, "id_answer");
        var assignmentId = ExtractValue(rowData, "id_organization_survey") ?? ExtractValue(recordPk, "id_organization_survey");
        var organizationId = ExtractOrganizationId(rowData, recordPk);
        var surveyId = ExtractValue(rowData, "id_survey") ?? ExtractValue(recordPk, "id_survey");

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(answerId))
        {
            parts.Add($"Ответ {answerId}");
        }

        if (!string.IsNullOrWhiteSpace(assignmentId))
        {
            parts.Add($"назначение {assignmentId}");
        }

        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            parts.Add($"организация {organizationId}");
        }

        if (!string.IsNullOrWhiteSpace(surveyId))
        {
            parts.Add($"анкета {surveyId}");
        }

        return parts.Count == 0 ? "Ответ" : string.Join(", ", parts);
    }

    private static string BuildAnswerItemTarget(JObject? recordPk, JObject? rowData)
    {
        var answerId = ExtractValue(rowData, "id_answer") ?? ExtractValue(recordPk, "id_answer");
        var questionOrder = ExtractValue(rowData, "question_order") ?? ExtractValue(recordPk, "question_order");

        if (!string.IsNullOrWhiteSpace(answerId) && !string.IsNullOrWhiteSpace(questionOrder))
        {
            return $"Вопрос {questionOrder} ответа {answerId}";
        }

        return BuildIdLabel(recordPk, "id_item", "Строка ответа");
    }

    private static string BuildAssignmentTarget(JObject? recordPk, JObject? rowData)
    {
        var assignmentId = ExtractValue(recordPk, "id_organization_survey") ?? ExtractValue(rowData, "id_organization_survey");
        var organizationId = ExtractOrganizationId(recordPk, rowData);
        var surveyId = ExtractValue(recordPk, "id_survey") ?? ExtractValue(rowData, "id_survey");

        if (!string.IsNullOrWhiteSpace(organizationId) && !string.IsNullOrWhiteSpace(surveyId))
        {
            return $"Организация {organizationId} / анкета {surveyId}";
        }

        if (!string.IsNullOrWhiteSpace(assignmentId))
        {
            return $"Назначение {assignmentId}";
        }

        return BuildGenericTarget(recordPk);
    }

    private static string BuildSurveyAutoCreationTarget(JObject? recordPk, JObject? rowData)
    {
        var configId = ExtractValue(recordPk, "id_config") ?? ExtractValue(rowData, "id_config");
        var surveyId = ExtractValue(recordPk, "id_survey") ?? ExtractValue(rowData, "id_survey");

        if (!string.IsNullOrWhiteSpace(configId) && !string.IsNullOrWhiteSpace(surveyId))
        {
            return $"Конфигурация {configId} / анкета {surveyId}";
        }

        return BuildGenericTarget(recordPk);
    }

    private static string BuildGenericTarget(JObject? recordPk)
    {
        if (recordPk == null || !recordPk.Properties().Any())
        {
            return "Нет данных";
        }

        return string.Join(
            ", ",
            recordPk.Properties().Select(property => $"{NormalizeAuditPropertyName(property.Name)}={FormatTokenValue(property.Value)}"));
    }

    private static string? ExtractOrganizationId(JObject? primarySource, JObject? secondarySource)
    {
        return ExtractValue(primarySource, "id_organization")
            ?? ExtractValue(primarySource, "id_omsu")
            ?? ExtractValue(secondarySource, "id_organization")
            ?? ExtractValue(secondarySource, "id_omsu");
    }

    private static string GetEntityName(string sourceTable)
    {
        return sourceTable switch
        {
            "app_user" => "Пользователь",
            "organization" => "Организация",
            "survey" => "Анкета",
            "survey_question" => "Критерий анкеты",
            "answer" => "Ответ",
            "answer_item" => "Строка ответа",
            "organization_survey" => "Назначение анкеты",
            "auto_creation_config" => "Настройка автосоздания",
            "survey_auto_creation_config" => "Связь автосоздания и анкеты",
            "email_config" => "Почтовая настройка",
            "theme_config" => "Настройка темы",
            _ => sourceTable
        };
    }

    private static string GetOperationName(string operation)
    {
        return operation switch
        {
            "INSERT" => "Добавление",
            "UPDATE" => "Изменение",
            "DELETE" => "Удаление",
            _ => operation
        };
    }

    private static string GetOperationVerb(string operation)
    {
        return operation switch
        {
            "INSERT" => "Добавил",
            "UPDATE" => "Изменил",
            "DELETE" => "Удалил",
            _ => operation
        };
    }

    private static JObject? ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JObject.Parse(json);
        }
        catch
        {
            return new JObject { ["raw"] = json };
        }
    }

    private static string? FirstNonEmpty(params JToken?[] tokens)
    {
        return tokens
            .Select(token => token?.ToString())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string BuildIdLabel(JObject? obj, string propertyName, string prefix)
    {
        var value = ExtractValue(obj, propertyName);
        return string.IsNullOrWhiteSpace(value) ? "Нет данных" : $"{prefix} {value}";
    }

    private static string? ExtractValue(JObject? obj, string propertyName)
    {
        if (obj == null)
        {
            return null;
        }

        return obj.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
            ? token?.ToString()
            : null;
    }

    private static string NormalizeAuditPropertyName(string propertyName)
    {
        return propertyName switch
        {
            "id_omsu" => "id_organization",
            _ => propertyName
        };
    }

    private static string? BuildAuditSql(IReadOnlyCollection<string> availableAuditTables)
    {
        var unionParts = AuditSources
            .Where(source => availableAuditTables.Contains(source.AuditTableName))
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

    private static string? BuildAuditDetailSql(IReadOnlyCollection<string> availableAuditTables, string? sourceTable)
    {
        var sourceTableName = sourceTable?.Trim();
        var sources = AuditSources
            .Where(source => availableAuditTables.Contains(source.AuditTableName))
            .Where(source => string.IsNullOrWhiteSpace(sourceTableName)
                             || string.Equals(source.SourceTable, sourceTableName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (sources.Length == 0 && !string.IsNullOrWhiteSpace(sourceTableName))
        {
            sources = AuditSources
                .Where(source => availableAuditTables.Contains(source.AuditTableName))
                .ToArray();
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

    private static string? BuildAuditRelatedRowsSql(IReadOnlyCollection<string> availableAuditTables)
    {
        var unionParts = AuditSources
            .Where(source => availableAuditTables.Contains(source.AuditTableName))
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

    private static string? BuildAuditEventCountSql(IReadOnlyCollection<string> availableAuditTables)
    {
        var unionParts = AuditSources
            .Where(source => availableAuditTables.Contains(source.AuditTableName))
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

    private static string BuildStructuredAuditSelect(
        AuditSourceDefinition source,
        bool includeDetails,
        string? dateDirection,
        bool limitPerSource)
    {
        var sourceOrder = AuditSourceOrder[source.SourceTable];
        var sourceTableExpression = $"'{source.SourceTable}'::text";
        var recordPkExpression = BuildRecordPkJsonExpression(source);
        var currentRowDataExpression = BuildAuditRowDataJsonExpression("current_row");
        var parentRowDataExpression = BuildAuditRowDataJsonExpression("parent_row");
        var detailsProjection = includeDetails
            ? $$"""
                {{recordPkExpression}}::text AS record_pk_json,
                {{currentRowDataExpression}}::text AS row_data_json,
                CASE
                    WHEN current_row.operation = 'DELETE' THEN {{currentRowDataExpression}}
                    WHEN current_row.operation = 'UPDATE' AND parent_row.id_audit IS NOT NULL THEN {{parentRowDataExpression}}
                    ELSE NULL::jsonb
                END::text AS old_row_data_json,
                CASE
                    WHEN current_row.operation IN ('INSERT', 'UPDATE') THEN {{currentRowDataExpression}}
                    ELSE NULL::jsonb
                END::text AS new_row_data_json
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
                {{sourceTableExpression}} AS source_table,
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
                {{detailsProjection}}
            FROM {{sourceFrom}}
            LEFT JOIN public.{{source.AuditTableName}} parent_row
                ON parent_row.id_audit = current_row.parent_audit_id
            """;
    }

    private static string BuildRecordPkJsonExpression(AuditSourceDefinition source)
    {
        var parts = source.PrimaryKeyColumns
            .Select(column => $"'{column}', current_row.{column}")
            .ToArray();

        return parts.Length == 0
            ? "'{}'::jsonb"
            : $"jsonb_build_object({string.Join(", ", parts)})";
    }

    private static string BuildAuditRowDataJsonExpression(string tableAlias)
    {
        return $"(to_jsonb({tableAlias}) {AuditRowJsonExclusions})";
    }

    private static string? BuildAuditPageSql(
        IReadOnlyCollection<string> availableAuditTables,
        string sortBy,
        string sortDirection,
        bool includeDetails)
    {
        var isDateSort = string.Equals(sortBy, AuditLogSortFields.Date, StringComparison.Ordinal);
        var dateDirection = string.Equals(sortDirection, "asc", StringComparison.Ordinal) ? "ASC" : "DESC";
        var unionParts = AuditSources
            .Where(source => availableAuditTables.Contains(source.AuditTableName))
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
                ELSE CONCAT('audit|', {tableAlias}.source_table, '|', {tableAlias}.id_audit::text)
            END
            """;
    }

    private static string BuildAuditEventPageOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "asc", StringComparison.Ordinal) ? "ASC" : "DESC";

        return sortBy switch
        {
            AuditLogSortFields.User => $"""
                COALESCE(actor.full_name, actor.login, '') {direction},
                event_groups.changed_at DESC,
                event_groups.source_order DESC,
                event_groups.id_audit DESC
                """,
            AuditLogSortFields.Event => $"""
                CASE event_groups.operation
                    WHEN 'INSERT' THEN 'Добавление'
                    WHEN 'UPDATE' THEN 'Изменение'
                    WHEN 'DELETE' THEN 'Удаление'
                    ELSE event_groups.operation
                END {direction},
                event_groups.changed_at DESC,
                event_groups.source_order DESC,
                event_groups.id_audit DESC
                """,
            AuditLogSortFields.Description => $"""
                event_groups.source_table {direction},
                event_groups.changed_at DESC,
                event_groups.source_order DESC,
                event_groups.id_audit DESC
                """,
            _ => $"""
                event_groups.changed_at {direction},
                event_groups.source_order {direction},
                event_groups.id_audit {direction}
                """
        };
    }

    private static string BuildAuditPageOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "asc", StringComparison.Ordinal) ? "ASC" : "DESC";

        return sortBy switch
        {
            AuditLogSortFields.User => $"""
                COALESCE(actor.full_name, actor.login, '') {direction},
                audit_entries.changed_at DESC,
                audit_entries.source_order DESC,
                audit_entries.id_audit DESC
                """,
            AuditLogSortFields.Event => $"""
                CASE audit_entries.operation
                    WHEN 'INSERT' THEN 'Добавление'
                    WHEN 'UPDATE' THEN 'Изменение'
                    WHEN 'DELETE' THEN 'Удаление'
                    ELSE audit_entries.operation
                END {direction},
                audit_entries.changed_at DESC,
                audit_entries.source_order DESC,
                audit_entries.id_audit DESC
                """,
            AuditLogSortFields.Description => $"""
                audit_entries.source_table {direction},
                audit_entries.changed_at DESC,
                audit_entries.source_order DESC,
                audit_entries.id_audit DESC
                """,
            _ => $"""
                audit_entries.changed_at {direction},
                audit_entries.source_order {direction},
                audit_entries.id_audit {direction}
                """
        };
    }

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

    private const string AnswerContextByOrganizationSurveySql = """
        SELECT
            os.id_survey AS IdSurvey,
            s.name_survey AS SurveyName,
            os.id_organization AS IdOrganization,
            o.organization_name AS OrganizationName
        FROM public.organization_survey os
        LEFT JOIN public.survey s
            ON s.id_survey = os.id_survey
        LEFT JOIN public.organization o
            ON o.id_organization = os.id_organization
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
        INNER JOIN public.organization_survey os
            ON os.id_organization_survey = a.id_organization_survey
        LEFT JOIN public.survey s
            ON s.id_survey = os.id_survey
        LEFT JOIN public.organization o
            ON o.id_organization = os.id_organization
        WHERE a.id_answer = @IdAnswer
        LIMIT 1;
        """;

    private sealed class AuditLogRow
    {
        public string SourceTable { get; init; } = string.Empty;
        public int SourceOrder { get; init; }
        public long IdAudit { get; init; }
        public long? ParentAuditId { get; init; }
        public string Operation { get; init; } = string.Empty;
        public DateTime ChangedAt { get; init; }
        public int? ChangedByUserId { get; init; }
        public string? ActorName { get; init; }
        public string? TargetName { get; init; }
        public string? TargetId { get; init; }
        public string? RelatedKind { get; init; }
        public string? RelatedId { get; init; }
        public string? RecordPkJson { get; init; }
        public string? RowDataJson { get; init; }
        public string? OldRowDataJson { get; init; }
        public string? NewRowDataJson { get; init; }
    }

    private sealed record AuditMetadata(
        HashSet<string> AvailableAuditTables,
        IReadOnlyDictionary<string, IReadOnlyList<string>> SourceColumnOrders);

    private sealed class SourceColumnOrderRow
    {
        public string TableName { get; init; } = string.Empty;
        public string ColumnName { get; init; } = string.Empty;
        public int OrdinalPosition { get; init; }
    }

    private sealed class AnswerContextRow
    {
        public int IdSurvey { get; init; }
        public string? SurveyName { get; init; }
        public int IdOrganization { get; init; }
        public string? OrganizationName { get; init; }
    }

    private sealed record AuditSourceDefinition(
        string SourceTable,
        string AuditTableName,
        IReadOnlyList<string> PrimaryKeyColumns,
        string TargetNameSql,
        string TargetIdSql,
        string RelatedKindSql,
        string RelatedIdSql);

    private sealed record AuditLogGroupCandidate(int Index, Log Log, AuditLogGroupKey? GroupKey);

    private sealed record AuditLogGroupKey(string Kind, string RelatedId, int? UserId, long ChangedAtTicks);

    private sealed record AuditLogSourceSummaryKey(string SourceTable, string Operation);
}
