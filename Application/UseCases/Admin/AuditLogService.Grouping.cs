using System.Globalization;
using MainProject.Application.Support;
using MainProject.Domain.Entities;
using Newtonsoft.Json.Linq;

namespace MainProject.Application.UseCases.Admin;

public partial class AuditLogService
{
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

    private sealed record AuditLogGroupCandidate(int Index, Log Log, AuditLogGroupKey? GroupKey);

    private sealed record AuditLogGroupKey(string Kind, string RelatedId, int? UserId, long ChangedAtTicks);
}
