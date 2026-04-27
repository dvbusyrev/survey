using MainProject.Application.Contracts;

namespace MainProject.Application.UseCases.Admin;

public sealed class SurveyAutoCreationHostedService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SurveyAutoCreationHostedService> _logger;
    private Timer? _timer;

    public SurveyAutoCreationHostedService(IServiceScopeFactory scopeFactory, ILogger<SurveyAutoCreationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Фоновая служба автосоздания анкет запущена");
        _timer = new Timer(Execute, null, TimeSpan.Zero, TimeSpan.FromHours(6));
        return Task.CompletedTask;
    }

    private async void Execute(object? state)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISurveyAutoCreationService>();
            var result = await service.RunPendingAsync();

            if (result.Processed)
            {
                _logger.LogInformation(
                    "Автосоздание анкет выполнилось. Создано копий: {Count}, дата периода: {ScheduleDate}",
                    result.CreatedSurveyCount,
                    result.ScheduleDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка фоновой службы автосоздания анкет");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Фоновая служба автосоздания анкет остановлена");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
