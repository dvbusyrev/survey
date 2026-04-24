using MainProject.Application.Contracts;
using MainProject.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace MainProject.Tests.Controllers;

public sealed class LogControllerTests
{
    [Fact]
    public void GetLogs_ReturnsLogsView_WhenServiceSucceeds()
    {
        var expectedLogs = new[] { new Log { IdLog = 1 } };
        var controller = new LogController(new StubAuditLogService(expectedLogs));

        var result = controller.GetLogs();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("get_logs", viewResult.ViewName);
        Assert.Same(expectedLogs, viewResult.Model);
    }

    [Fact]
    public void GetLogs_ReturnsEmptyLogsView_WhenServiceThrows()
    {
        var controller = new LogController(new ThrowingAuditLogService());

        var result = controller.GetLogs();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("get_logs", viewResult.ViewName);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Log>>(viewResult.Model));
        Assert.Contains("Не удалось загрузить журнал событий", controller.ViewData["LogLoadErrorMessage"] as string);
    }

    private sealed class StubAuditLogService : IAuditLogService
    {
        private readonly IReadOnlyList<Log> _logs;

        public StubAuditLogService(IReadOnlyList<Log> logs)
        {
            _logs = logs;
        }

        public IReadOnlyList<Log> GetLogs()
            => _logs;

        public string GenerateLogText(IEnumerable<Log> logs)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingAuditLogService : IAuditLogService
    {
        public IReadOnlyList<Log> GetLogs()
            => throw new InvalidOperationException("audit tables are unavailable");

        public string GenerateLogText(IEnumerable<Log> logs)
            => throw new NotSupportedException();
    }
}
