using System.Collections.Concurrent;

namespace MainProject.Infrastructure.External.Calendar;

public sealed class ProductionCalendarService
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<int, Lazy<Task<string>>> _yearCache = new();

    public ProductionCalendarService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
        var response = await _httpClient.GetStringAsync($"api/getdata?year={year}", cancellationToken);
        var calendar = string.Concat(response.Where(static symbol => !char.IsWhiteSpace(symbol)));
        var expectedLength = DateTime.IsLeapYear(year) ? 366 : 365;
        if (calendar.Length != expectedLength
            || calendar.Any(static code => code is not ('0' or '1' or '2' or '4' or '8')))
        {
            throw new InvalidOperationException($"Производственный календарь за {year} год получен в некорректном формате.");
        }

        return calendar;
    }
}
