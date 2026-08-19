using Microsoft.Extensions.Configuration;

namespace MainProject.Infrastructure.Configuration;

public static class ExternalConfigurationLoader
{
    public const string PathEnvironmentVariable = "SURVEY_CONFIG_PATH";

    public static string? Add(
        ConfigurationManager configuration,
        string environmentName,
        string? configuredPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            if (string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Для рабочего окружения задайте переменную {PathEnvironmentVariable} " +
                    "с абсолютным путём к внешнему JSON-файлу конфигурации.");
            }

            return null;
        }

        if (!Path.IsPathRooted(configuredPath))
        {
            throw new InvalidOperationException(
                $"Переменная {PathEnvironmentVariable} должна содержать абсолютный путь.");
        }

        var fullPath = Path.GetFullPath(configuredPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Внешний файл конфигурации не найден: {fullPath}",
                fullPath);
        }

        configuration.AddJsonFile(fullPath, optional: false, reloadOnChange: false);

        // Server environment variables remain the highest-priority source.
        configuration.AddEnvironmentVariables();
        return fullPath;
    }
}
