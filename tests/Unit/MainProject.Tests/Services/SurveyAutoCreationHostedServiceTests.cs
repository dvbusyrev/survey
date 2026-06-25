using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Admin;
using MainProject.Web.ViewModels;
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
        services.AddScoped<ISurveyAutoCreationService>(_ => autoCreationService);
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

    private sealed class RecordingAutoCreationService : ISurveyAutoCreationService
    {
        public TaskCompletionSource<CancellationToken> RunToken { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RunCount { get; private set; }

        public Task<SurveyAutoCreationRunResult> RunPendingAsync(CancellationToken cancellationToken = default)
        {
            RunCount++;
            RunToken.TrySetResult(cancellationToken);
            return Task.FromResult(new SurveyAutoCreationRunResult());
        }

        public Task<SurveyAutoCreationPageViewModel> GetPageModelAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SurveySelectionItem>> GetSurveyOptionsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SurveyAutoCreationCommandResult> SaveAsync(
            SurveyAutoCreationSettingsRequest? request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SurveyAutoCreationCommandResult> StartAsync(
            SurveyAutoCreationSettingsRequest? request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SurveyAutoCreationCommandResult> StopAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
