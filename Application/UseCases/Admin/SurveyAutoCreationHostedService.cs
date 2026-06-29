using Microsoft.Extensions.Hosting;
using MainProject.Application.UseCases.Surveys;

namespace MainProject.Application.UseCases.Admin;

public sealed class SurveyAutoCreationHostedService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(6);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SurveyAutoCreationHostedService> _logger;

    public SurveyAutoCreationHostedService(IServiceScopeFactory scopeFactory, ILogger<SurveyAutoCreationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Фоновая служба автосоздания анкет запущена");

        try
        {
            await RunPendingAsync(stoppingToken);

            using var timer = new PeriodicTimer(RunInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunPendingAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            _logger.LogInformation("Фоновая служба автосоздания анкет остановлена");
        }
    }

    private async Task RunPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<SurveyService>();
            var result = await service.RunPendingAsync(cancellationToken);

            if (result.Processed)
            {
                _logger.LogInformation(
                    "Автосоздание анкет выполнилось. Создано копий: {Count}, дата периода: {ScheduleDate}",
                    result.CreatedSurveyCount,
                    result.ScheduleDate);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка фоновой службы автосоздания анкет");
        }
    }
}
