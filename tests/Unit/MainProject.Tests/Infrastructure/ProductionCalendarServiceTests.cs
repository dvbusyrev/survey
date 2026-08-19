using System.Net;
using MainProject.Infrastructure.External.Calendar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MainProject.Tests.Unit.Infrastructure;

public sealed class ProductionCalendarServiceTests
{
    [Fact]
    public void DependencyInjection_ResolvesTypedHttpClient()
    {
        var services = new ServiceCollection();
        services.Configure<ProductionCalendarOptions>(_ => { });
        services.AddLogging();
        services.AddHttpClient<ProductionCalendarService>(client =>
        {
            client.BaseAddress = new Uri("https://calendar.test/");
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ProductionCalendarService>());
    }

    [Fact]
    public async Task IsBusinessDayAsync_ReadsLocalCalendarWithoutNetworkRequest()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var calendar = new string('0', 365).ToCharArray();
            calendar[0] = '8';
            await File.WriteAllTextAsync(Path.Combine(directory, "2026.txt"), new string(calendar));
            var handler = new CalendarHandler(_ => throw new InvalidOperationException("Network must not be used."));
            var service = CreateService(handler, directory, remoteDownloadEnabled: false);

            Assert.False(await service.IsBusinessDayAsync(new DateTime(2026, 1, 1)));
            Assert.True(await service.IsBusinessDayAsync(new DateTime(2026, 1, 2)));
            Assert.Equal(0, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task IsBusinessDayAsync_PersistsDownloadedCalendarForOfflineUse()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var handler = new CalendarHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new string('0', 365))
            });
            var onlineService = CreateService(handler, directory, remoteDownloadEnabled: true);

            Assert.True(await onlineService.IsBusinessDayAsync(new DateTime(2026, 1, 1)));
            Assert.Equal(1, handler.RequestCount);

            var filePath = Path.Combine(directory, "2026.txt");
            Assert.True(File.Exists(filePath));
            Assert.Equal(365, (await File.ReadAllTextAsync(filePath)).Length);

            var offlineHandler = new CalendarHandler(_ => throw new InvalidOperationException("Network must not be used."));
            var offlineService = CreateService(offlineHandler, directory, remoteDownloadEnabled: false);

            Assert.True(await offlineService.IsBusinessDayAsync(new DateTime(2026, 1, 1)));
            Assert.Equal(0, offlineHandler.RequestCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task IsBusinessDayAsync_ReportsMissingOfflineCalendar()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var handler = new CalendarHandler(_ => throw new InvalidOperationException("Network must not be used."));
            var service = CreateService(handler, directory, remoteDownloadEnabled: false);

            var exception = await Assert.ThrowsAsync<ProductionCalendarUnavailableException>(() =>
                service.IsBusinessDayAsync(new DateTime(2026, 1, 1)));

            Assert.Contains("2026.txt", exception.Message);
            Assert.Equal(0, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task IsBusinessDayAsync_RejectsInvalidLocalCalendar()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "2026.txt"), "invalid");
            var service = CreateService(
                new CalendarHandler(_ => throw new InvalidOperationException("Network must not be used.")),
                directory,
                remoteDownloadEnabled: false);

            var exception = await Assert.ThrowsAsync<ProductionCalendarUnavailableException>(() =>
                service.IsBusinessDayAsync(new DateTime(2026, 1, 1)));

            Assert.Contains("некорректный формат", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ProductionCalendarService CreateService(
        HttpMessageHandler handler,
        string dataPath,
        bool remoteDownloadEnabled)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://calendar.test/")
        };
        return new ProductionCalendarService(
            httpClient,
            Options.Create(new ProductionCalendarOptions
            {
                DataPath = dataPath,
                RemoteDownloadEnabled = remoteDownloadEnabled
            }),
            NullLogger<ProductionCalendarService>.Instance);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"survey-calendar-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class CalendarHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
