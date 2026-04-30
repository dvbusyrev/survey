using System.Globalization;
using System.Text;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;
using Newtonsoft.Json.Linq;

namespace MainProject.Application.UseCases.Admin;

public sealed class AuditLogService : IAuditLogService
{
    private const string RedactedValue = "[REDACTED]";
    private static readonly AuditSourceDefinition[] AuditSources =
    [
        new("app_user", "app_user_l"),
        new("organization", "organization_l"),
        new("survey", "survey_l"),
        new("answer", "answer_l"),
        new("organization_survey", "organization_survey_l")
    ];

    private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "hash_password",
        "password",
        "csp",
        "key_csp",
        "signature",
        "email"
    };

    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IReadOnlyList<Log> GetLogs()
    {
        using var connection = _connectionFactory.CreateConnection();
        var availableAuditTables = connection.Query<string>(
                ExistingAuditTablesSql,
                new { TableNames = AuditSources.Select(source => source.AuditTableName).ToArray() })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var auditSql = BuildAuditSql(availableAuditTables);
        if (string.IsNullOrWhiteSpace(auditSql))
        {
            return Array.Empty<Log>();
        }

        var rows = connection.Query<AuditLogRow>(auditSql).ToList();
        var orderedRows = rows
            .OrderBy(row => row.ChangedAt)
            .ThenBy(row => row.IdAudit)
            .ToList();

        var previousSnapshots = new Dictionary<string, JObject>(StringComparer.Ordinal);
        var logs = new List<Log>(orderedRows.Count);

        foreach (var row in orderedRows)
        {
            var recordPk = ParseJsonObject(row.RecordPkJson);
            var rowData = ParseJsonObject(row.RowDataJson);
            var recordKey = BuildRecordKey(row.SourceTable, recordPk);

            previousSnapshots.TryGetValue(recordKey, out var previousRowData);

            logs.Add(MapAuditLog(row, recordPk, rowData, previousRowData));

            if (string.IsNullOrWhiteSpace(recordKey))
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

        return logs
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.IdLog)
            .ToList();
    }

    public string GenerateLogText(IEnumerable<Log> logs)
    {
        var sb = new StringBuilder();

        foreach (var log in logs.OrderByDescending(item => item.Date).ThenByDescending(item => item.IdLog))
        {
            sb.AppendLine(BuildExportLine(log));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildExportLine(Log log)
    {
        if (log.ExtraData is not JObject details)
        {
            return $"{log.Date:dd.MM.yyyy HH:mm:ss} {log.Description ?? "Событие без описания."}";
        }

        var actorPrefix = log.IdUser.HasValue ? "Пользователь" : "Система";
        var actorName = string.IsNullOrWhiteSpace(log.NameUser) ? "Неизвестно" : log.NameUser!;
        var actorId = log.IdUser?.ToString(CultureInfo.InvariantCulture) ?? "—";
        var targetTable = ExtractValue(details, "source_table_name") ?? log.TargetType ?? "Объект";
        var targetId = ExtractValue(details, "target_id") ?? "—";
        var targetName = string.IsNullOrWhiteSpace(log.TargetName) ? targetTable : log.TargetName!;
        var operationVerb = ExtractValue(details, "operation_verb") ?? log.EventType ?? "Изменил";

        var line = $"{log.Date:dd.MM.yyyy HH:mm:ss} {actorPrefix} {actorName} (таблица {actorPrefix}, id = {actorId}) {operationVerb} запись объекта {targetName} (таблица {targetTable}, id = {targetId})";

        if (string.Equals(ExtractValue(details, "operation"), "UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            line += ". " + BuildUpdateDetailsText(details);
        }
        else
        {
            line += ".";
        }

        return line;
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
        JObject? previousRowData)
    {
        var entityName = GetEntityName(row.SourceTable);
        var operationName = GetOperationName(row.Operation);
        var operationVerb = GetOperationVerb(row.Operation);
        var targetName = BuildTargetName(row.SourceTable, recordPk, rowData);
        var changedFields = BuildChangedFields(rowData, previousRowData);
        var changeReason = BuildChangeReason(row.Operation, previousRowData, changedFields);

        return new Log
        {
            IdLog = row.IdAudit,
            IdUser = row.ChangedByUserId,
            TargetType = entityName,
            EventType = operationName,
            Date = row.ChangedAt,
            Description = BuildDescription(operationVerb, targetName, changedFields, changeReason),
            ExtraData = BuildDetails(row, entityName, operationVerb, targetName, recordPk, rowData, previousRowData, changedFields, changeReason),
            NameUser = !string.IsNullOrWhiteSpace(row.ActorName)
                ? row.ActorName
                : row.ChangedByUserId.HasValue
                    ? $"ID {row.ChangedByUserId}"
                    : "Система",
            TargetName = targetName
        };
    }

    private static string BuildDescription(
        string operationVerb,
        string targetName,
        JArray changedFields,
        string? changeReason)
    {
        if (changedFields.Count > 0)
        {
            var changedFieldList = string.Join(
                ", ",
                changedFields
                    .OfType<JObject>()
                    .Select(change => ExtractValue(change, "field"))
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

            return $"{operationVerb} запись объекта {targetName}. Изменены поля: {changedFieldList}.";
        }

        if (!string.IsNullOrWhiteSpace(changeReason))
        {
            return $"{operationVerb} запись объекта {targetName}. {changeReason}.";
        }

        return $"{operationVerb} запись объекта {targetName}.";
    }

    private static JObject BuildDetails(
        AuditLogRow row,
        string entityName,
        string operationVerb,
        string targetName,
        JObject? recordPk,
        JObject? rowData,
        JObject? previousRowData,
        JArray changedFields,
        string? changeReason)
    {
        return new JObject
        {
            ["operation"] = row.Operation,
            ["operation_name"] = GetOperationName(row.Operation),
            ["operation_verb"] = operationVerb,
            ["source_table"] = row.SourceTable,
            ["source_table_name"] = entityName,
            ["target_name"] = targetName,
            ["target_id"] = BuildRecordIdentifier(recordPk),
            ["record_pk"] = SanitizeToken(recordPk) ?? new JObject(),
            ["row_data"] = SanitizeToken(rowData) ?? new JObject(),
            ["previous_row_data"] = SanitizeToken(previousRowData),
            ["changed_fields"] = changedFields,
            ["change_reason"] = changeReason
        };
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
            properties.Select(property => $"{property.Name}={FormatTokenValue(property.Value)}"));
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
            "answer" => BuildAnswerTarget(recordPk, rowData),
            "organization_survey" => BuildAssignmentTarget(recordPk, rowData),
            _ => BuildGenericTarget(recordPk)
        };
    }

    private static string BuildAnswerTarget(JObject? recordPk, JObject? rowData)
    {
        var answerId = ExtractValue(recordPk, "id_answer");
        var assignmentId = ExtractValue(rowData, "id_organization_survey") ?? ExtractValue(recordPk, "id_organization_survey");
        var organizationId = ExtractValue(rowData, "id_organization") ?? ExtractValue(recordPk, "id_organization");
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

    private static string BuildAssignmentTarget(JObject? recordPk, JObject? rowData)
    {
        var assignmentId = ExtractValue(recordPk, "id_organization_survey") ?? ExtractValue(rowData, "id_organization_survey");
        var organizationId = ExtractValue(recordPk, "id_organization") ?? ExtractValue(rowData, "id_organization");
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

    private static string BuildGenericTarget(JObject? recordPk)
    {
        if (recordPk == null || !recordPk.Properties().Any())
        {
            return "Нет данных";
        }

        return string.Join(", ", recordPk.Properties().Select(property => $"{property.Name}={property.Value}"));
    }

    private static string GetEntityName(string sourceTable)
    {
        return sourceTable switch
        {
            "app_user" => "Пользователь",
            "organization" => "Организация",
            "survey" => "Анкета",
            "answer" => "Ответ",
            "organization_survey" => "Назначение анкеты",
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

    private static string? BuildAuditSql(IReadOnlyCollection<string> availableAuditTables)
    {
        var unionParts = AuditSources
            .Where(source => availableAuditTables.Contains(source.AuditTableName))
            .Select(source =>
                $$"""
                    SELECT '{{source.SourceTable}}'::text AS source_table, id_audit, operation, changed_at, changed_by_user_id, record_pk, row_data
                    FROM public.{{source.AuditTableName}}
                """)
            .ToArray();

        if (unionParts.Length == 0)
        {
            return null;
        }

        return $$"""
            SELECT
                audit_entries.source_table AS SourceTable,
                audit_entries.id_audit AS IdAudit,
                audit_entries.operation AS Operation,
                audit_entries.changed_at AS ChangedAt,
                audit_entries.changed_by_user_id AS ChangedByUserId,
                COALESCE(actor.full_name, actor.login) AS ActorName,
                audit_entries.record_pk::text AS RecordPkJson,
                audit_entries.row_data::text AS RowDataJson
            FROM (
                {{string.Join("\n                UNION ALL\n", unionParts)}}
            ) audit_entries
            LEFT JOIN public.app_user actor
                ON actor.id_user = audit_entries.changed_by_user_id
            ORDER BY audit_entries.changed_at DESC, audit_entries.id_audit DESC;
            """;
    }

    private const string ExistingAuditTablesSql = """
        SELECT table_name
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = ANY(@TableNames);
        """;

    private sealed class AuditLogRow
    {
        public string SourceTable { get; init; } = string.Empty;
        public long IdAudit { get; init; }
        public string Operation { get; init; } = string.Empty;
        public DateTime ChangedAt { get; init; }
        public int? ChangedByUserId { get; init; }
        public string? ActorName { get; init; }
        public string? RecordPkJson { get; init; }
        public string? RowDataJson { get; init; }
    }

    private sealed record AuditSourceDefinition(string SourceTable, string AuditTableName);
}
