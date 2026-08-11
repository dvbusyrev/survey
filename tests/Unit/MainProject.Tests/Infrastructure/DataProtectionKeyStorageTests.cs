using MainProject.Infrastructure.Security;

namespace MainProject.Tests.Infrastructure;

public sealed class DataProtectionKeyStorageTests
{
    [Fact]
    public void ResolveKeysPath_UsesExternalApplicationDataDirectory_InProduction()
    {
        var result = DataProtectionKeyStorage.ResolveKeysPath(
            configuredPath: null,
            contentRootPath: Path.Combine(Path.GetTempPath(), "published-app"),
            environmentName: "Production",
            localApplicationDataPath: Path.Combine(Path.GetTempPath(), "service-data"));

        Assert.Equal(
            Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "service-data",
                "AIS.Anketirovanie",
                "DataProtection-Keys")),
            result);
    }

    [Fact]
    public void ResolveKeysPath_RejectsRelativeProductionOverride()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionKeyStorage.ResolveKeysPath(
                configuredPath: "relative/key-path",
                contentRootPath: Path.GetTempPath(),
                environmentName: "Production",
                localApplicationDataPath: Path.GetTempPath()));

        Assert.Contains("абсолютным путём", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Prepare_CopiesLegacyKeysToConfiguredDirectory()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"survey-key-storage-{Guid.NewGuid():N}");
        var legacyDirectory = Path.Combine(testRoot, "App_Data", "DataProtection-Keys");
        var targetDirectory = Path.Combine(testRoot, "persistent-keys");
        var legacyKeyPath = Path.Combine(legacyDirectory, "key-existing.xml");

        try
        {
            Directory.CreateDirectory(legacyDirectory);
            File.WriteAllText(legacyKeyPath, "<key />");

            var result = DataProtectionKeyStorage.Prepare(
                targetDirectory,
                testRoot,
                "Production");

            Assert.Equal(Path.GetFullPath(targetDirectory), result.FullName);
            Assert.Equal("<key />", File.ReadAllText(Path.Combine(targetDirectory, "key-existing.xml")));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
