using System.Globalization;
using MainProject.Application.DTO.Audit;
using MainProject.Application.Support;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;
using Newtonsoft.Json.Linq;

namespace MainProject.Application.UseCases.Admin;

public partial class AuditLogService
{
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
            "survey_template" => FirstNonEmpty(rowData?["name_survey_template"]) ?? BuildIdLabel(recordPk, "id_survey_template", "Шаблон"),
            "survey_question" => BuildSurveyQuestionTarget(recordPk, rowData),
            "survey_template_question" => BuildSurveyTemplateQuestionTarget(recordPk, rowData),
            "answer" => BuildAnswerTarget(recordPk, rowData),
            "answer_item" => BuildAnswerItemTarget(recordPk, rowData),
            "organization_survey" => BuildAssignmentTarget(recordPk, rowData),
            "organization_survey_template" => BuildSurveyTemplateAssignmentTarget(recordPk, rowData),
            "auto_creation_config" => BuildIdLabel(recordPk, "id_config", "Конфигурация"),
            "survey_template_auto_creation_config" => BuildSurveyTemplateAutoCreationTarget(recordPk, rowData),
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

    private static string BuildSurveyTemplateQuestionTarget(JObject? recordPk, JObject? rowData)
    {
        var questionText = FirstNonEmpty(rowData?["question_text"]);
        if (!string.IsNullOrWhiteSpace(questionText))
        {
            return questionText;
        }

        var questionOrder = ExtractValue(rowData, "question_order") ?? ExtractValue(recordPk, "question_order");
        var templateId = ExtractValue(rowData, "id_survey_template") ?? ExtractValue(recordPk, "id_survey_template");
        return !string.IsNullOrWhiteSpace(questionOrder) && !string.IsNullOrWhiteSpace(templateId)
            ? $"Вопрос {questionOrder} шаблона {templateId}"
            : BuildIdLabel(recordPk, "id_survey_template_question", "Вопрос");
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

    private static string BuildSurveyTemplateAssignmentTarget(JObject? recordPk, JObject? rowData)
    {
        var assignmentId = ExtractValue(recordPk, "id_organization_survey_template")
            ?? ExtractValue(rowData, "id_organization_survey_template");
        var organizationId = ExtractOrganizationId(recordPk, rowData);
        var templateId = ExtractValue(recordPk, "id_survey_template")
            ?? ExtractValue(rowData, "id_survey_template");

        if (!string.IsNullOrWhiteSpace(organizationId) && !string.IsNullOrWhiteSpace(templateId))
        {
            return $"Организация {organizationId} / шаблон {templateId}";
        }

        return !string.IsNullOrWhiteSpace(assignmentId)
            ? $"Назначение шаблона {assignmentId}"
            : BuildGenericTarget(recordPk);
    }

    private static string BuildSurveyTemplateAutoCreationTarget(JObject? recordPk, JObject? rowData)
    {
        var configId = ExtractValue(recordPk, "id_config") ?? ExtractValue(rowData, "id_config");
        var templateId = ExtractValue(recordPk, "id_survey_template") ?? ExtractValue(rowData, "id_survey_template");

        if (!string.IsNullOrWhiteSpace(configId) && !string.IsNullOrWhiteSpace(templateId))
        {
            return $"Конфигурация {configId} / шаблон {templateId}";
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
}
