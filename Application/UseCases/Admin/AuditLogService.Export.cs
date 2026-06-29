using System.Globalization;
using System.Text;
using MainProject.Domain.Entities;
using Newtonsoft.Json.Linq;

namespace MainProject.Application.UseCases.Admin;

public partial class AuditLogService
{
    public virtual string GenerateLogText(IEnumerable<Log> logs)
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

}
