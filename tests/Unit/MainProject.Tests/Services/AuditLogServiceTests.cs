using MainProject.Application.UseCases.Admin;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace MainProject.Tests.Services;

public sealed class AuditLogServiceTests
{
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

        Assert.Contains(
            "20.04.2026 14:30:15 Пользователь Администратор (таблица Пользователь, id = 2) Изменил запись объекта Новая анкета (таблица Анкета, id = 7). Изменил атрибуты: name_survey = \"Новая анкета 2\" (старое значение: \"Новая анкета\").",
            result);
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
                ["source_table_name"] = "Анкета",
                ["target_id"] = "7",
                ["changed_fields"] = new JArray(),
                ["change_reason"] = "предыдущая версия записи не найдена в журнале"
            }
        };

        var result = service.GenerateLogText(new[] { log });

        Assert.Contains(
            "Изменённые атрибуты не определены: предыдущая версия записи не найдена в журнале.",
            result);
    }

    [Fact]
    public void BuildAuditSql_ReturnsEmptyString_WhenNoAuditTablesExist()
    {
        var result = AuditLogService.BuildAuditSql(Array.Empty<AuditLogService.AuditSourceDescriptor>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildAuditSql_UsesOnlyExistingAuditTables()
    {
        var result = AuditLogService.BuildAuditSql(
            new[]
            {
                new AuditLogService.AuditSourceDescriptor("app_user", "app_user_l"),
                new AuditLogService.AuditSourceDescriptor("survey", "survey_l")
            });

        Assert.Contains("FROM public.app_user_l", result);
        Assert.Contains("FROM public.survey_l", result);
        Assert.DoesNotContain("FROM public.answer_l", result);
        Assert.DoesNotContain("FROM public.organization_survey_l", result);
    }

    private sealed class ThrowingDbConnectionFactory : IDbConnectionFactory
    {
        public NpgsqlConnection CreateConnection()
        {
            throw new NotSupportedException("Database access is not used in these tests.");
        }
    }
}
