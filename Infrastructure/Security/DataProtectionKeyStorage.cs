namespace MainProject.Infrastructure.Security;

public static class DataProtectionKeyStorage
{
    private const string ApplicationDirectoryName = "AIS.Anketirovanie";
    private const string KeysDirectoryName = "DataProtection-Keys";
    private static readonly string LegacyRelativePath = Path.Combine("App_Data", KeysDirectoryName);

    public static DirectoryInfo Prepare(
        string? configuredPath,
        string contentRootPath,
        string environmentName)
    {
        var keysPath = ResolveKeysPath(
            configuredPath,
            contentRootPath,
            environmentName,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        Directory.CreateDirectory(keysPath);
        RestrictDirectoryAccess(keysPath);
        CopyLegacyKeys(contentRootPath, keysPath);

        return new DirectoryInfo(keysPath);
    }

    public static string ResolveKeysPath(
        string? configuredPath,
        string contentRootPath,
        string environmentName,
        string localApplicationDataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (Path.IsPathRooted(configuredPath))
            {
                return Path.GetFullPath(configuredPath);
            }

            if (IsLocalEnvironment(environmentName))
            {
                return Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
            }

            throw new InvalidOperationException(
                "DataProtection:KeysPath должен быть абсолютным путём в рабочем окружении.");
        }

        if (IsLocalEnvironment(environmentName))
        {
            return Path.GetFullPath(Path.Combine(contentRootPath, LegacyRelativePath));
        }

        if (string.IsNullOrWhiteSpace(localApplicationDataPath))
        {
            throw new InvalidOperationException(
                "Не удалось определить постоянную директорию для ключей Data Protection. " +
                "Укажите абсолютный путь в DataProtection:KeysPath.");
        }

        return Path.GetFullPath(Path.Combine(
            localApplicationDataPath,
            ApplicationDirectoryName,
            KeysDirectoryName));
    }

    private static bool IsLocalEnvironment(string environmentName) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
        || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    private static void CopyLegacyKeys(string contentRootPath, string keysPath)
    {
        var legacyKeysPath = Path.GetFullPath(Path.Combine(contentRootPath, LegacyRelativePath));
        if (string.Equals(legacyKeysPath, keysPath, StringComparison.Ordinal)
            || !Directory.Exists(legacyKeysPath))
        {
            return;
        }

        foreach (var sourcePath in Directory.EnumerateFiles(legacyKeysPath, "key-*.xml"))
        {
            var destinationPath = Path.Combine(keysPath, Path.GetFileName(sourcePath));
            try
            {
                File.Copy(sourcePath, destinationPath, overwrite: false);
                RestrictFileAccess(destinationPath);
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
                // Another application instance copied the same key during startup.
            }
        }
    }

    private static void RestrictDirectoryAccess(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void RestrictFileAccess(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
