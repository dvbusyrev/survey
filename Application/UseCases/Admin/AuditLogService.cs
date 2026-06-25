using System.Globalization;
using System.Text;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Audit;
using MainProject.Application.Support;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;
using Newtonsoft.Json.Linq;

namespace MainProject.Application.UseCases.Admin;

public sealed class AuditLogService : IAuditLogService
{
    private const string RedactedValue = "[REDACTED]";
    private static readonly StringComparer AuditLogStringComparer = StringComparer.Create(new CultureInfo("ru-RU"), true);

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

    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public Task<AuditLogPageViewModel> GetLogsPageAsync(
        int currentPage,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default)
    {
        return GetLogsPageInternalAsync(currentPage, pageSize, sortBy, sortDirection, stripDetails: true, cancellationToken);
    }

    public async Task<Log?> GetLogDetailsAsync(
        long idLog,
        string? sourceTable,
        int currentPage,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default)
    {
        if (idLog <= 0)
        {
            return null;
        }

        var result = await _auditLogRepository.GetDetailsAsync(idLog, sourceTable, cancellationToken);
        if (result.Rows.Count == 0)
        {
            return null;
        }

        var logs = MapAuditRows(result.Rows, result.SourceColumnOrders, reconstructPreviousSnapshots: false);
        var log = logs.FirstOrDefault(log => log.IdLog == idLog) ?? logs.FirstOrDefault();
        if (log != null)
        {
            await EnrichAnswerContextAsync(log, cancellationToken);
        }

        return log;
    }

    private async Task<AuditLogPageViewModel> GetLogsPageInternalAsync(
        int currentPage,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        bool stripDetails,
        CancellationToken cancellationToken)
    {
        var normalizedPageSize = pageSize > 0 ? pageSize : AppListPaging.DefaultPageSize;
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeSortDirection(null, normalizedSortBy);
        var totalCount = await _auditLogRepository.GetEventCountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling((double)totalCount / normalizedPageSize);
        var normalizedPage = Math.Clamp(currentPage, 1, totalPages);
        var pageRead = await _auditLogRepository.GetPageAsync(
            normalizedPage,
            normalizedPageSize,
            normalizedSortBy,
            normalizedSortDirection,
            includeDetails: !stripDetails,
            cancellationToken);
        var pageLogs = SortLogsForPage(
                MapAuditRows(pageRead.Rows, pageRead.SourceColumnOrders, reconstructPreviousSnapshots: false),
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

    public async Task<IReadOnlyList<Log>> GetLogsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _auditLogRepository.GetAllAsync(cancellationToken);
        var groupedLogs = MapAuditRows(result.Rows, result.SourceColumnOrders, reconstructPreviousSnapshots: true);
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

    private async Task EnrichAnswerContextAsync(Log log, CancellationToken cancellationToken)
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
        var organizationSurveyId = int.TryParse(idOrganizationSurvey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOrganizationSurveyId)
            ? parsedOrganizationSurveyId
            : (int?)null;
        var answerId = int.TryParse(idAnswer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAnswerId)
            ? parsedAnswerId
            : (int?)null;
        var context = await _auditLogRepository.GetAnswerContextAsync(
            organizationSurveyId,
            answerId,
            cancellationToken);

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

        var source = AuditLogTableRegistry.Find(sourceTable);
        if (source?.ChainKind == null || source.ChainIdentifierColumn == null)
        {
            return null;
        }

        var relatedId = ExtractValue(rowData, source.ChainIdentifierColumn)
            ?? ExtractValue(recordPk, source.ChainIdentifierColumn);
        if (!string.IsNullOrWhiteSpace(relatedId))
        {
            return new AuditLogGroupKey(source.ChainKind, relatedId, log.IdUser, log.Date.Ticks);
        }

        return null;
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
        var preferredSourceTable = AuditLogTableRegistry.FindGroup(groupKind)?.PrimarySourceTable ?? string.Empty;

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

        var group = AuditLogTableRegistry.FindGroup(groupKey.Kind);
        return group == null ? groupKey.RelatedId : $"{group.FallbackTargetPrefix} {groupKey.RelatedId}";
    }

    private static string GetGroupTargetType(string groupKind)
    {
        return AuditLogTableRegistry.FindGroup(groupKind)?.EntityName ?? "Запись";
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
        return AuditLogTableRegistry.GetSourceOrder(sourceTable);
    }

    private static int GetAuditSourceOrder(string? sourceTable)
    {
        return AuditLogTableRegistry.GetSourceOrder(sourceTable);
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
        return AuditLogTableRegistry.Find(sourceTable)?.EntityName ?? sourceTable;
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
        return AuditLogQueryBuilder.BuildAuditSql(availableAuditTables);
    }

    private sealed record AuditLogGroupCandidate(int Index, Log Log, AuditLogGroupKey? GroupKey);

    private sealed record AuditLogGroupKey(string Kind, string RelatedId, int? UserId, long ChangedAtTicks);

    private sealed record AuditLogSourceSummaryKey(string SourceTable, string Operation);
}
