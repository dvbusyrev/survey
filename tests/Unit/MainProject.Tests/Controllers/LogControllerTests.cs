using MainProject.Application.Contracts;
using MainProject.Application.UseCases.Admin;
using MainProject.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using MainProject.Web.ViewModels;

namespace MainProject.Tests.Controllers;

public sealed class LogControllerTests
{
    [Fact]
    public async Task GetLogs_ReturnsLogsView_WhenServiceSucceeds()
    {
        var expectedLogs = new[] { new Log { IdLog = 1 } };
        var controller = new LogController(new StubAuditLogService(expectedLogs), new FixedClock());

        var result = await controller.GetLogs();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("get_logs", viewResult.ViewName);
        var model = Assert.IsType<AuditLogPageViewModel>(viewResult.Model);
        Assert.Same(expectedLogs, model.Logs);
    }

    [Fact]
    public async Task GetLogs_ReturnsSafeErrorView_WhenServiceThrows()
    {
        var controller = new LogController(new ThrowingAuditLogService(), new FixedClock());

        var result = await controller.GetLogs();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Error", viewResult.ViewName);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal("Не удалось загрузить журнал событий.", model.Message);
        Assert.False(string.IsNullOrWhiteSpace(model.RequestId));
    }

    [Fact]
    public async Task GetDumpLogs_UsesClockForExportFileName()
    {
        var controller = new LogController(
            new StubAuditLogService(Array.Empty<Log>()),
            new FixedClock());

        var result = await controller.GetDumpLogs();

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("АИС Анкетирование. Журнал событий 2030-05-10 12-30-00.txt", fileResult.FileDownloadName);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime Today => new(2030, 5, 10);
        public DateTime Now => new(2030, 5, 10, 12, 30, 0);
    }

    private sealed class StubAuditLogService : AuditLogService
    {
        private readonly IReadOnlyList<Log> _logs;

        public StubAuditLogService(IReadOnlyList<Log> logs)
        {
            _logs = logs;
        }

        public override Task<IReadOnlyList<Log>> GetLogsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_logs);

        public override Task<AuditLogPageViewModel> GetLogsPageAsync(
            int currentPage, int pageSize, string? sortBy, string? sortDirection, CancellationToken cancellationToken = default)
            => Task.FromResult(new AuditLogPageViewModel
            {
                Logs = _logs,
                CurrentPage = currentPage,
                TotalPages = 1,
                TotalCount = _logs.Count,
                PageSize = pageSize,
                SortBy = sortBy ?? string.Empty,
                SortDirection = sortDirection ?? string.Empty
            });

        public override Task<Log?> GetLogDetailsAsync(
            long idLog, string? sourceTable, int currentPage, int pageSize, string? sortBy, string? sortDirection,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_logs.FirstOrDefault(log => log.IdLog == idLog));

        public override string GenerateLogText(IEnumerable<Log> logs) => "Журнал";
    }

    private sealed class ThrowingAuditLogService : AuditLogService
    {
        public override Task<AuditLogPageViewModel> GetLogsPageAsync(
            int currentPage, int pageSize, string? sortBy, string? sortDirection, CancellationToken cancellationToken = default)
            => Task.FromException<AuditLogPageViewModel>(new InvalidOperationException("audit tables are unavailable"));

        public override Task<Log?> GetLogDetailsAsync(
            long idLog, string? sourceTable, int currentPage, int pageSize, string? sortBy, string? sortDirection,
            CancellationToken cancellationToken = default)
            => Task.FromException<Log?>(new InvalidOperationException("audit tables are unavailable"));

        public override Task<IReadOnlyList<Log>> GetLogsAsync(CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<Log>>(new InvalidOperationException("audit tables are unavailable"));

        public override string GenerateLogText(IEnumerable<Log> logs)
            => throw new NotSupportedException();
    }
}
