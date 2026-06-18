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

        public Log? GetLogDetails(long idLog, string? sourceTable, int currentPage, int pageSize, string? sortBy, string? sortDirection)
            => _logs.FirstOrDefault(log => log.IdLog == idLog);

        public string GenerateLogText(IEnumerable<Log> logs)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingAuditLogService : IAuditLogService
    {
        public AuditLogPageViewModel GetLogsPage(int currentPage, int pageSize, string? sortBy, string? sortDirection)
            => throw new InvalidOperationException("audit tables are unavailable");

        public Log? GetLogDetails(long idLog, string? sourceTable, int currentPage, int pageSize, string? sortBy, string? sortDirection)
            => throw new InvalidOperationException("audit tables are unavailable");

        public IReadOnlyList<Log> GetLogs()
            => throw new InvalidOperationException("audit tables are unavailable");

        public string GenerateLogText(IEnumerable<Log> logs)
            => throw new NotSupportedException();
    }
}
