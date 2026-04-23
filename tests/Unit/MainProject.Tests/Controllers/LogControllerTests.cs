using MainProject.Application.Contracts;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace MainProject.Tests.Controllers;

public sealed class LogControllerTests
{
    [Fact]
    public void GetLogs_ReturnsViewModelWithBootstrapJson()
    {
        var service = new StubAuditLogService(
            new[]
            {
                new Log
                {
                    IdLog = 10,
                    IdUser = 5,
                    NameUser = "ivanov",
                    EventType = "Изменение",
                    TargetType = "Анкета",
                    TargetName = "Анкета 2026",
                    Description = "Изменил запись объекта Анкета 2026.",
                    Date = new DateTime(2026, 4, 23, 12, 30, 0),
                    ExtraData = new JObject
                    {
                        ["operation"] = "UPDATE"
                    }
                }
            });

        var controller = new LogController(service);

        var result = controller.GetLogs();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("get_logs", viewResult.ViewName);

        var model = Assert.IsType<AuditLogPageViewModel>(viewResult.Model);
        Assert.Single(model.Logs);
        Assert.Contains("\"Id\":10", model.LogsBootstrapJson);
        Assert.Contains("\\\"operation\\\": \\\"UPDATE\\\"", model.LogsBootstrapJson);
    }

    [Fact]
    public void GetLogs_ReturnsErrorView_WhenServiceThrows()
    {
        var controller = new LogController(new ThrowingAuditLogService());

        var result = controller.GetLogs();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Error", viewResult.ViewName);

        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Contains("Ошибка при получении списка логов", model.Message);
    }

    private sealed class StubAuditLogService : IAuditLogService
    {
        private readonly IReadOnlyList<Log> _logs;

        public StubAuditLogService(IReadOnlyList<Log> logs)
        {
            _logs = logs;
        }

        public IReadOnlyList<Log> GetLogs() => _logs;

        public string GenerateLogText(IEnumerable<Log> logs) => string.Empty;
    }

    private sealed class ThrowingAuditLogService : IAuditLogService
    {
        public IReadOnlyList<Log> GetLogs() => throw new InvalidOperationException("boom");

        public string GenerateLogText(IEnumerable<Log> logs) => throw new NotSupportedException();
    }
}
