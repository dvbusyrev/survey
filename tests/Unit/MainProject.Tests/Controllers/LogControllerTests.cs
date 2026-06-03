using MainProject.Application.Contracts;
using MainProject.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using MainProject.Web.ViewModels;

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
        var model = Assert.IsType<AuditLogPageViewModel>(viewResult.Model);
        Assert.Same(expectedLogs, model.Logs);
    }

    [Fact]
    public void GetLogs_ReturnsEmptyLogsView_WhenServiceThrows()
    {
        var controller = new LogController(new ThrowingAuditLogService());

        var result = controller.GetLogs();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("get_logs", viewResult.ViewName);
        var model = Assert.IsType<AuditLogPageViewModel>(viewResult.Model);
        Assert.Empty(model.Logs);
        Assert.Contains("Не удалось загрузить журнал событий", controller.ViewData["LogLoadErrorMessage"] as string);
    }

    [Fact]
    public void RedirectLegacyLogs_RedirectsToEventLog()
    {
        var controller = new LogController(new StubAuditLogService(Array.Empty<Log>()));

        var result = controller.RedirectLegacyLogs();

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.True(redirectResult.Permanent);
        Assert.Equal("/event-log", redirectResult.Url);
    }

    [Fact]
    public void RedirectLegacyDumpLogs_RedirectsToEventLogExport()
    {
        var controller = new LogController(new StubAuditLogService(Array.Empty<Log>()));

        var result = controller.RedirectLegacyDumpLogs();

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.True(redirectResult.Permanent);
        Assert.Equal("/event-log/export", redirectResult.Url);
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

        public AuditLogPageViewModel GetLogsPage(int currentPage, int pageSize, string? sortBy, string? sortDirection)
            => new()
            {
                Logs = _logs,
                CurrentPage = currentPage,
                TotalPages = 1,
                TotalCount = _logs.Count,
                PageSize = pageSize,
                SortBy = sortBy ?? string.Empty,
                SortDirection = sortDirection ?? string.Empty
            };

        public string GenerateLogText(IEnumerable<Log> logs)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingAuditLogService : IAuditLogService
    {
        public AuditLogPageViewModel GetLogsPage(int currentPage, int pageSize, string? sortBy, string? sortDirection)
            => throw new InvalidOperationException("audit tables are unavailable");

        public IReadOnlyList<Log> GetLogs()
            => throw new InvalidOperationException("audit tables are unavailable");

        public string GenerateLogText(IEnumerable<Log> logs)
            => throw new NotSupportedException();
    }
}
