using System.Collections.ObjectModel;

namespace MainProject.Application.Support;

/// <summary>
/// The single catalogue of auditable tables. Query projection, entity names and
/// relationship grouping must be changed here together with a new audit table.
/// </summary>
public static class AuditLogTableRegistry
{
    private static readonly IReadOnlyList<AuditLogTableDefinition> SourceDefinitions =
        new ReadOnlyCollection<AuditLogTableDefinition>(
        [
            new("app_user", "app_user_l", ["id_user"], "Пользователь",
                "COALESCE(NULLIF(current_row.full_name, ''), NULLIF(current_row.login, ''), 'ID ' || current_row.id_user::text)",
                "current_row.id_user::text", "NULL::text", "NULL::text"),
            new("organization", "organization_l", ["id_organization"], "Организация",
                "COALESCE(NULLIF(current_row.organization_name, ''), 'ID ' || current_row.id_organization::text)",
                "current_row.id_organization::text", "NULL::text", "NULL::text"),
            new("survey", "survey_l", ["id_survey"], "Анкета",
                "COALESCE(NULLIF(current_row.name_survey, ''), 'Анкета ' || current_row.id_survey::text)",
                "current_row.id_survey::text", "'survey'::text", "current_row.id_survey::text",
                "survey", "id_survey", IsPrimaryInChain: true),
            new("survey_question", "survey_question_l", ["id_question"], "Критерий анкеты",
                "COALESCE(NULLIF(current_row.question_text, ''), 'Вопрос ' || current_row.question_order::text || ' анкеты ' || current_row.id_survey::text)",
                "current_row.id_question::text", "'survey'::text", "current_row.id_survey::text",
                "survey", "id_survey"),
            new("organization_survey", "organization_survey_l", ["id_organization_survey"], "Назначение анкеты",
                "'Организация ' || current_row.id_organization::text || ' / анкета ' || current_row.id_survey::text",
                "current_row.id_organization_survey::text", "'survey'::text", "current_row.id_survey::text",
                "survey", "id_survey"),
            new("answer", "answer_l", ["id_answer"], "Ответ",
                "'Ответ ' || current_row.id_answer::text || ', назначение ' || current_row.id_organization_survey::text",
                "current_row.id_answer::text", "'answer'::text", "current_row.id_answer::text",
                "answer", "id_answer", IsPrimaryInChain: true),
            new("answer_item", "answer_item_l", ["id_item"], "Строка ответа",
                "'Вопрос ' || current_row.question_order::text || ' ответа ' || current_row.id_answer::text",
                "current_row.id_item::text", "'answer'::text", "current_row.id_answer::text",
                "answer", "id_answer"),
            new("auto_creation_config", "auto_creation_config_l", ["id_config"], "Настройка автосоздания",
                "'Конфигурация ' || current_row.id_config::text",
                "current_row.id_config::text", "'auto_creation_config'::text", "current_row.id_config::text",
                "auto_creation_config", "id_config", IsPrimaryInChain: true),
            new("survey_auto_creation_config", "survey_auto_creation_config_l", ["id_config", "id_survey"], "Связь автосоздания и анкеты",
                "'Конфигурация ' || current_row.id_config::text || ' / анкета ' || current_row.id_survey::text",
                "current_row.id_config::text || ', ' || current_row.id_survey::text", "'auto_creation_config'::text", "current_row.id_config::text",
                "auto_creation_config", "id_config"),
            new("email_config", "email_config_l", ["id_config"], "Почтовая настройка",
                "'Почтовая конфигурация ' || current_row.id_config::text",
                "current_row.id_config::text", "'email_config'::text", "current_row.id_config::text",
                "email_config", "id_config", IsPrimaryInChain: true),
            new("theme_config", "theme_config_l", ["id_config"], "Настройка темы",
                "'Конфигурация темы ' || current_row.id_config::text",
                "current_row.id_config::text", "'theme_config'::text", "current_row.id_config::text",
                "theme_config", "id_config", IsPrimaryInChain: true)
        ]);

    private static readonly IReadOnlyDictionary<string, AuditLogTableDefinition> BySourceTable =
        SourceDefinitions.ToDictionary(item => item.SourceTable, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, AuditLogGroupDefinition> Groups =
        new Dictionary<string, AuditLogGroupDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["survey"] = new("survey", "Анкета", "Анкета"),
            ["answer"] = new("answer", "Ответ", "Ответ"),
            ["auto_creation_config"] = new("auto_creation_config", "Настройка автосоздания", "Конфигурация"),
            ["email_config"] = new("email_config", "Почтовая настройка", "Почтовая конфигурация"),
            ["theme_config"] = new("theme_config", "Настройка темы", "Конфигурация темы")
        };

    public static IReadOnlyList<AuditLogTableDefinition> Sources => SourceDefinitions;

    public static AuditLogTableDefinition? Find(string? sourceTable)
    {
        return !string.IsNullOrWhiteSpace(sourceTable)
               && BySourceTable.TryGetValue(sourceTable, out var source)
            ? source
            : null;
    }

    public static AuditLogGroupDefinition? FindGroup(string? groupKind)
    {
        return !string.IsNullOrWhiteSpace(groupKind)
               && Groups.TryGetValue(groupKind, out var group)
            ? group
            : null;
    }

    public static int GetSourceOrder(string? sourceTable)
    {
        var source = Find(sourceTable);
        if (source == null)
        {
            return int.MaxValue;
        }

        for (var index = 0; index < SourceDefinitions.Count; index++)
        {
            if (ReferenceEquals(SourceDefinitions[index], source))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}

public sealed record AuditLogTableDefinition(
    string SourceTable,
    string AuditTableName,
    IReadOnlyList<string> PrimaryKeyColumns,
    string EntityName,
    string TargetNameSql,
    string TargetIdSql,
    string RelatedKindSql,
    string RelatedIdSql,
    string? ChainKind = null,
    string? ChainIdentifierColumn = null,
    bool IsPrimaryInChain = false);

public sealed record AuditLogGroupDefinition(
    string PrimarySourceTable,
    string EntityName,
    string FallbackTargetPrefix);
