using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Options;

namespace MainProject.Infrastructure.External.Calendar;

public sealed class ProductionCalendarService
{
    private readonly HttpClient _httpClient;
    private readonly string? _dataPath;
    private readonly bool _remoteDownloadEnabled;
    private readonly ILogger<ProductionCalendarService> _logger;
    private readonly ConcurrentDictionary<int, Lazy<Task<string>>> _yearCache = new();

    public ProductionCalendarService(
        HttpClient httpClient,
        IOptions<ProductionCalendarOptions> options,
        ILogger<ProductionCalendarService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _remoteDownloadEnabled = options.Value.RemoteDownloadEnabled;
        _dataPath = ResolveDataPath(options.Value.DataPath);
    }

    public async Task<bool> IsBusinessDayAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var calendar = await GetYearAsync(date.Year, cancellationToken);
        var code = calendar[date.DayOfYear - 1];
        return code is '0' or '2' or '4';
    }

    private async Task<string> GetYearAsync(int year, CancellationToken cancellationToken)
    {
        var lazy = _yearCache.GetOrAdd(
            year,
            requestedYear => new Lazy<Task<string>>(
                () => LoadYearAsync(requestedYear, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazy.Value;
        }
        catch
        {
            _yearCache.TryRemove(new KeyValuePair<int, Lazy<Task<string>>>(year, lazy));
            throw;
        }
    }

    private async Task<string> LoadYearAsync(int year, CancellationToken cancellationToken)
    {
        var localCalendar = await TryLoadLocalYearAsync(year, cancellationToken);
        if (localCalendar != null)
        {
            return localCalendar;
        }

        if (!_remoteDownloadEnabled)
        {
            throw new ProductionCalendarUnavailableException(
                $"Локальный производственный календарь за {year} год не найден: {GetExpectedFilePath(year) ?? "каталог не настроен"}.");
        }

        string calendar;
        try
        {
            var response = await _httpClient.GetStringAsync($"api/getdata?year={year}", cancellationToken);
            calendar = NormalizeAndValidate(response, year);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProductionCalendarUnavailableException(
                $"Не удалось получить производственный календарь за {year} год из внешнего сервиса. " +
                $"Ожидаемый локальный файл: {GetExpectedFilePath(year) ?? "каталог не настроен"}.",
                ex);
        }

        await TryPersistLocalYearAsync(year, calendar, cancellationToken);
        return calendar;
    }

    private async Task<string?> TryLoadLocalYearAsync(int year, CancellationToken cancellationToken)
    {
        var filePath = GetExpectedFilePath(year);
        if (filePath == null || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return NormalizeAndValidate(content, year);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProductionCalendarUnavailableException(
                $"Локальный производственный календарь за {year} год имеет некорректный формат: {filePath}.",
                ex);
        }
    }

    private async Task TryPersistLocalYearAsync(
        int year,
        string calendar,
        CancellationToken cancellationToken)
    {
        var filePath = GetExpectedFilePath(year);
        if (filePath == null)
        {
            return;
        }

        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(_dataPath!);
            await File.WriteAllTextAsync(temporaryPath, calendar, Encoding.ASCII, cancellationToken);
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не удалось сохранить производственный календарь за {Year} год в {FilePath}",
                year,
                filePath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private string? GetExpectedFilePath(int year)
        => _dataPath == null ? null : Path.Combine(_dataPath, $"{year}.txt");

    private static string? ResolveDataPath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        if (!Path.IsPathRooted(configuredPath))
        {
            throw new InvalidOperationException(
                "ProductionCalendar:DataPath должен содержать абсолютный путь.");
        }

        return Path.GetFullPath(configuredPath);
    }

    private static string NormalizeAndValidate(string value, int year)
    {
        var calendar = string.Concat(value.Where(static symbol => !char.IsWhiteSpace(symbol)));
        var expectedLength = DateTime.IsLeapYear(year) ? 366 : 365;
        if (calendar.Length != expectedLength
            || calendar.Any(static code => code is not ('0' or '1' or '2' or '4' or '8')))
        {
            throw new InvalidOperationException($"Производственный календарь за {year} год получен в некорректном формате.");
        }

        return calendar;
    }
}
