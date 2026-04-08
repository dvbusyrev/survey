using System.Text;
using Dapper;
using MainProject.Infrastructure.Database;
using MainProject.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MainProject.Services.Admin;

public sealed class AuditLogService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IReadOnlyList<Log> GetLogs()
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = connection.Query<AuditLogRow>(AuditSql).ToList();
        return rows.Select(MapAuditLog).ToList();
    }

    public string GenerateLogText(IEnumerable<Log> logs)
    {
        var sb = new StringBuilder();

        foreach (var log in logs.OrderByDescending(item => item.Date))
        {
            sb.AppendLine(
                $"{log.Date:dd.MM.yyyy HH:mm:ss} [{log.EventType}] {log.TargetType}: {log.TargetName}. Пользователь: {log.NameUser}. {log.Description}");

            if (log.ExtraData is JToken token)
            {
                sb.AppendLine(token.ToString(Formatting.None));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static Log MapAuditLog(AuditLogRow row)
    {
        var recordPk = ParseJsonObject(row.RecordPkJson);
        var rowData = ParseJsonObject(row.RowDataJson);
        var entityName = GetEntityName(row.SourceTable);
        var operationName = GetOperationName(row.Operation);
        var targetName = BuildTargetName(row.SourceTable, recordPk, rowData);

        return new Log
        {
            IdLog = row.IdAudit,
            IdUser = row.ChangedByUserId,
            TargetType = entityName,
            EventType = operationName,
            Date = row.ChangedAt,
            Description = $"{operationName} сущности \"{entityName}\": {targetName}",
            ExtraData = BuildDetails(recordPk, rowData),
            NameUser = !string.IsNullOrWhiteSpace(row.ActorName)
                ? row.ActorName
                : row.ChangedByUserId.HasValue
                    ? $"ID {row.ChangedByUserId}"
                    : "Система",
            TargetName = targetName
        };
    }

    private static JObject BuildDetails(JObject? recordPk, JObject? rowData)
    {
        return new JObject
        {
            ["record_pk"] = recordPk ?? new JObject(),
            ["row_data"] = rowData ?? new JObject()
        };
    }

    private static string BuildTargetName(string sourceTable, JObject? recordPk, JObject? rowData)
    {
        return sourceTable switch
        {
            "app_user" => FirstNonEmpty(rowData?["full_name"], rowData?["name_user"]) ?? BuildIdLabel(recordPk, "id_user", "ID"),
            "organization" => FirstNonEmpty(rowData?["organization_name"]) ?? BuildIdLabel(recordPk, "organization_id", "ID"),
            "survey" => FirstNonEmpty(rowData?["name_survey"]) ?? BuildIdLabel(recordPk, "id_survey", "ID"),
            "answer" => BuildAnswerTarget(recordPk, rowData),
            "organization_survey" => BuildAssignmentTarget(recordPk, rowData),
            _ => BuildGenericTarget(recordPk)
        };
    }

    private static string BuildAnswerTarget(JObject? recordPk, JObject? rowData)
    {
        var answerId = ExtractValue(recordPk, "id_answer");
        var organizationId = ExtractValue(rowData, "organization_id") ?? ExtractValue(recordPk, "organization_id");
        var surveyId = ExtractValue(rowData, "id_survey") ?? ExtractValue(recordPk, "id_survey");

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(answerId))
        {
            parts.Add($"Ответ {answerId}");
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
        var organizationId = ExtractValue(recordPk, "organization_id") ?? ExtractValue(rowData, "organization_id");
        var surveyId = ExtractValue(recordPk, "id_survey") ?? ExtractValue(rowData, "id_survey");

        if (!string.IsNullOrWhiteSpace(organizationId) && !string.IsNullOrWhiteSpace(surveyId))
        {
            return $"Организация {organizationId} / анкета {surveyId}";
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
            "INSERT" => "Создание",
            "UPDATE" => "Изменение",
            "DELETE" => "Удаление",
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

    private const string AuditSql = """
        SELECT
            audit_entries.source_table AS SourceTable,
            audit_entries.id_audit AS IdAudit,
            audit_entries.operation AS Operation,
            audit_entries.changed_at AS ChangedAt,
            audit_entries.changed_by_user_id AS ChangedByUserId,
            COALESCE(actor.full_name, actor.name_user) AS ActorName,
            audit_entries.record_pk::text AS RecordPkJson,
            audit_entries.row_data::text AS RowDataJson
        FROM (
            SELECT 'app_user'::text AS source_table, id_audit, operation, changed_at, changed_by_user_id, record_pk, row_data
            FROM public.app_user_l
            UNION ALL
            SELECT 'organization'::text AS source_table, id_audit, operation, changed_at, changed_by_user_id, record_pk, row_data
            FROM public.organization_l
            UNION ALL
            SELECT 'survey'::text AS source_table, id_audit, operation, changed_at, changed_by_user_id, record_pk, row_data
            FROM public.survey_l
            UNION ALL
            SELECT 'answer'::text AS source_table, id_audit, operation, changed_at, changed_by_user_id, record_pk, row_data
            FROM public.answer_l
            UNION ALL
            SELECT 'organization_survey'::text AS source_table, id_audit, operation, changed_at, changed_by_user_id, record_pk, row_data
            FROM public.organization_survey_l
        ) audit_entries
        LEFT JOIN public.app_user actor
            ON actor.id_user = audit_entries.changed_by_user_id
        ORDER BY audit_entries.changed_at DESC, audit_entries.id_audit DESC;
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
}
