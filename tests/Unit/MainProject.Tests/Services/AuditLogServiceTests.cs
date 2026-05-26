using MainProject.Application.UseCases.Admin;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;
using Newtonsoft.Json.Linq;
using Npgsql;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MainProject.Tests.Services;

public sealed class AuditLogServiceTests
{
    [Fact]
    public void BuildAuditSql_ReturnsNull_WhenNoAuditTablesExist()
    {
        var result = InvokeBuildAuditSql(Array.Empty<string>());

        Assert.Null(result);
    }

    [Fact]
    public void BuildAuditSql_UsesOnlyExistingAuditTables()
    {
        var result = InvokeBuildAuditSql(["survey_l", "organization_survey_l"]);

        Assert.NotNull(result);
        Assert.Contains("FROM public.survey_l", result);
        Assert.Contains("FROM public.organization_survey_l", result);
        Assert.DoesNotContain("FROM public.app_user_l", result);
        Assert.DoesNotContain("FROM public.organization_l", result);
        Assert.DoesNotContain("FROM public.answer_l", result);
        Assert.DoesNotContain("FROM public.survey_question_l", result);
        Assert.DoesNotContain("FROM public.answer_item_l", result);
        Assert.DoesNotContain("FROM public.auto_creation_config_l", result);
    }

    [Fact]
    public void BuildAuditSql_UsesCurrentAuditTables_WhenTheyExist()
    {
        var result = InvokeBuildAuditSql([
            "survey_question_l",
            "answer_item_l",
            "auto_creation_config_l",
            "survey_auto_creation_config_l",
            "email_config_l"
        ]);

        Assert.NotNull(result);
        Assert.Contains("FROM public.survey_question_l", result);
        Assert.Contains("FROM public.answer_item_l", result);
        Assert.Contains("FROM public.auto_creation_config_l", result);
        Assert.Contains("FROM public.survey_auto_creation_config_l", result);
        Assert.Contains("FROM public.email_config_l", result);
    }

    [Fact]
    public void GroupRelatedLogs_MergesSurveyChainIntoSingleEvent()
    {
        var changedAt = new DateTime(2026, 4, 20, 14, 30, 15);
        var logs = new List<Log>
        {
            BuildAuditLog(1, "survey", "INSERT", changedAt, "7", new JObject
            {
                ["id_survey"] = 7,
                ["name_survey"] = "Новая анкета"
            }),
            BuildAuditLog(2, "survey_question", "INSERT", changedAt, "12", new JObject
            {
                ["id_question"] = 12,
                ["id_survey"] = 7,
                ["question_order"] = 1,
                ["question_text"] = "Качество услуг"
            }),
            BuildAuditLog(3, "organization_survey", "INSERT", changedAt, "20", new JObject
            {
                ["id_organization_survey"] = 20,
                ["id_survey"] = 7,
                ["id_organization"] = 3
            })
        };

        var result = InvokeGroupRelatedLogs(logs);

        var groupedLog = Assert.Single(result);
        Assert.Equal("Добавление", groupedLog.EventType);
        Assert.Equal("записей из таблиц survey, survey_question, organization_survey", groupedLog.Description);
        var details = Assert.IsType<JObject>(groupedLog.ExtraData);
        Assert.True(details.Value<bool>("is_chain"));
        Assert.Equal(3, Assert.IsType<JArray>(details["items"]).Count);
    }

    [Fact]
    public void GenerateLogText_FormatsUpdateEntryWithChangedAttributes()
    {
        var service = new AuditLogService(new ThrowingDbConnectionFactory());
        var log = new Log
        {
            IdLog = 12,
            IdUser = 2,
            NameUser = "Администратор",
            TargetType = "Анкета",
            TargetName = "Новая анкета",
            Date = new DateTime(2026, 4, 20, 14, 30, 15),
            ExtraData = new JObject
            {
                ["operation"] = "UPDATE",
                ["operation_verb"] = "Изменил",
                ["source_table"] = "survey",
                ["source_table_name"] = "Анкета",
                ["target_id"] = "7",
                ["changed_fields"] = new JArray
                {
                    new JObject
                    {
                        ["field"] = "name_survey",
                        ["new_value"] = "Новая анкета 2",
                        ["old_value"] = "Новая анкета"
                    }
                }
            }
        };

        var result = service.GenerateLogText(new[] { log });

        Assert.Contains("АИС Анкетирование. Журнал событий", result);
        Assert.Contains("Количество событий: 1", result);
        Assert.Contains("Событие 1", result);
        Assert.Contains("Дата: 20.04.2026 14:30:15", result);
        Assert.Contains("Пользователь: Администратор (ID = 2)", result);
        Assert.Contains("Событие: Изменение", result);
        Assert.Contains("Описание: записи из таблицы survey", result);
        Assert.Contains("Таблица: survey", result);
        Assert.Contains("Запись: Новая анкета", result);
        Assert.Contains("ID записи: 7", result);
        Assert.Contains("Изменения:", result);
        Assert.Contains("- name_survey: \"Новая анкета\" -> \"Новая анкета 2\"", result);
    }

    [Fact]
    public void GenerateLogText_ExplainsWhyChangedAttributesAreUnavailable()
    {
        var service = new AuditLogService(new ThrowingDbConnectionFactory());
        var log = new Log
        {
            IdLog = 13,
            IdUser = 2,
            NameUser = "Администратор",
            TargetType = "Анкета",
            TargetName = "Новая анкета",
            Date = new DateTime(2026, 4, 20, 14, 31, 00),
            ExtraData = new JObject
            {
                ["operation"] = "UPDATE",
                ["operation_verb"] = "Изменил",
                ["source_table"] = "survey",
                ["source_table_name"] = "Анкета",
                ["target_id"] = "7",
                ["changed_fields"] = new JArray(),
                ["change_reason"] = "предыдущая версия записи не найдена в журнале"
            }
        };

        var result = service.GenerateLogText(new[] { log });

        Assert.Contains("Изменения:", result);
        Assert.Contains("- не определены: предыдущая версия записи не найдена в журнале.", result);
    }

    [Fact]
    public void GenerateLogText_FormatsChainEntryAsStructuredBlock()
    {
        var service = new AuditLogService(new ThrowingDbConnectionFactory());
        var logs = new List<Log>
        {
            BuildAuditLog(1, "survey", "INSERT", new DateTime(2026, 4, 20, 14, 30, 15), "7", new JObject
            {
                ["id_survey"] = 7,
                ["name_survey"] = "Новая анкета"
            }),
            BuildAuditLog(2, "survey_question", "INSERT", new DateTime(2026, 4, 20, 14, 30, 15), "12", new JObject
            {
                ["id_question"] = 12,
                ["id_survey"] = 7,
                ["question_order"] = 1,
                ["question_text"] = "Качество услуг"
            })
        };

        var groupedLog = Assert.Single(InvokeGroupRelatedLogs(logs));

        var result = service.GenerateLogText(new[] { groupedLog });

        Assert.Contains("Событие: Добавление", result);
        Assert.Contains("Описание: записей из таблиц survey, survey_question", result);
        Assert.Contains("Таблицы: survey, survey_question", result);
        Assert.Contains("Запись: Новая анкета", result);
        Assert.Contains("Связанные записи:", result);
        Assert.Contains("- survey: Новая анкета (ID: 7)", result);
        Assert.Contains("- survey_question: Качество услуг (ID: 12)", result);
    }

    [Fact]
    public void GenerateLogText_NormalizesLegacyOrganizationIdentifiersAndDeduplicatesChainItems()
    {
        var changedAt = new DateTime(2026, 4, 20, 14, 30, 15);
        var service = new AuditLogService(new ThrowingDbConnectionFactory());
        var logs = new List<Log>
        {
            BuildAuditLog(1, "survey", "UPDATE", changedAt, "3", new JObject
            {
                ["id_survey"] = 3,
                ["name_survey"] = "Первая анкета"
            }),
            BuildAuditLog(2, "organization_survey", "UPDATE", changedAt, "2", new JObject
            {
                ["id_omsu"] = 2,
                ["id_survey"] = 3
            }, new JObject
            {
                ["id_omsu"] = 2,
                ["id_survey"] = 3
            }),
            BuildAuditLog(3, "organization_survey", "UPDATE", changedAt, "2", new JObject
            {
                ["id_omsu"] = 2,
                ["id_survey"] = 3
            }, new JObject
            {
                ["id_omsu"] = 2,
                ["id_survey"] = 3
            })
        };

        var groupedLog = Assert.Single(InvokeGroupRelatedLogs(logs));

        var result = service.GenerateLogText(new[] { groupedLog });

        Assert.DoesNotContain("id_omsu", result);
        Assert.Contains("ID записи: id_organization=2, id_survey=3", result);
        Assert.Contains("- organization_survey: Организация 2 / анкета 3 (ID: id_organization=2, id_survey=3)", result);
        Assert.Equal(1, Regex.Matches(result, "- organization_survey: Организация 2 / анкета 3 \\(ID: id_organization=2, id_survey=3\\)").Count);
    }

    private sealed class ThrowingDbConnectionFactory : IDbConnectionFactory
    {
        public NpgsqlConnection CreateConnection()
        {
            throw new NotSupportedException("Database access is not used in these tests.");
        }
    }

    private static string? InvokeBuildAuditSql(IReadOnlyCollection<string> availableAuditTables)
    {
        var method = typeof(AuditLogService).GetMethod("BuildAuditSql", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return (string?)method!.Invoke(null, [availableAuditTables]);
    }

    private static List<Log> InvokeGroupRelatedLogs(IReadOnlyList<Log> logs)
    {
        var method = typeof(AuditLogService).GetMethod("GroupRelatedLogs", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return Assert.IsType<List<Log>>(method!.Invoke(null, [logs]));
    }

    private static Log BuildAuditLog(long idLog, string sourceTable, string operation, DateTime changedAt, string targetId, JObject rowData, JObject? recordPk = null)
    {
        var targetName = rowData.Value<string>("name_survey")
            ?? rowData.Value<string>("question_text")
            ?? targetId;

        return new Log
        {
            IdLog = idLog,
            IdUser = 2,
            NameUser = "Администратор",
            TargetType = sourceTable,
            TargetName = targetName,
            EventType = operation,
            Date = changedAt,
            Description = sourceTable,
            ExtraData = new JObject
            {
                ["audit_id"] = idLog,
                ["audit_source_order"] = sourceTable switch
                {
                    "survey" => 2,
                    "survey_question" => 3,
                    "organization_survey" => 4,
                    _ => 99
                },
                ["operation"] = operation,
                ["operation_name"] = operation switch
                {
                    "INSERT" => "Добавление",
                    "UPDATE" => "Изменение",
                    "DELETE" => "Удаление",
                    _ => operation
                },
                ["source_table"] = sourceTable,
                ["target_name"] = targetName,
                ["target_id"] = targetId,
                ["record_pk"] = recordPk ?? new JObject { ["id"] = targetId },
                ["row_data"] = rowData
            }
        };
    }
}
