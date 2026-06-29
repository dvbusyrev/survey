using MainProject.Application.DTO;
using MainProject.Application.UseCases.Admin;
using MainProject.Application.UseCases.Surveys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MainProject.Tests.Services;

public sealed class SurveyAutoCreationHostedServiceTests
{
    [Fact]
    public async Task StartAsync_RunsPendingWorkOnce_AndStopsWithCancellation()
    {
        var autoCreationService = new RecordingAutoCreationService();
        var services = new ServiceCollection();
        services.AddScoped<SurveyService>(_ => autoCreationService);
        await using var provider = services.BuildServiceProvider();

        var hostedService = new SurveyAutoCreationHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SurveyAutoCreationHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);
        var runToken = await autoCreationService.RunToken.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await hostedService.StopAsync(CancellationToken.None);

        Assert.True(runToken.CanBeCanceled);
        Assert.Equal(1, autoCreationService.RunCount);
    }

    private sealed class RecordingAutoCreationService : SurveyService
    {
        public TaskCompletionSource<CancellationToken> RunToken { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RunCount { get; private set; }

        public override Task<SurveyAutoCreationRunResult> RunPendingAsync(CancellationToken cancellationToken = default)
        {
            RunCount++;
            RunToken.TrySetResult(cancellationToken);
            return Task.FromResult(new SurveyAutoCreationRunResult());
        }
    }
}
