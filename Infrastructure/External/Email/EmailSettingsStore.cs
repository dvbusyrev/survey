using System.Text.Json;

namespace MainProject.Infrastructure.External.Email;

public sealed class EmailSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public EmailSettingsStore(IWebHostEnvironment environment)
    {
        _settingsFilePath = Path.Combine(environment.ContentRootPath, "App_Data", "email-settings.json");
    }

    public async Task<EmailTemplateSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new EmailTemplateSettings();
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<EmailTemplateSettings>(stream, cancellationToken: cancellationToken);
        return settings ?? new EmailTemplateSettings();
    }

    public async Task SaveAsync(EmailTemplateSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);

        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }
}
