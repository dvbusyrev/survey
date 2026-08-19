using MainProject.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace MainProject.Tests.Unit.Infrastructure;

public sealed class ExternalConfigurationLoaderTests
{
    [Fact]
    public void Add_RequiresExternalConfigurationInProduction()
    {
        var configuration = new ConfigurationManager();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExternalConfigurationLoader.Add(configuration, "Production", null));

        Assert.Contains(ExternalConfigurationLoader.PathEnvironmentVariable, exception.Message);
    }

    [Fact]
    public void Add_LoadsConfigurationFromAbsolutePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"survey-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var configurationPath = Path.Combine(directory, "server-config.json");

        try
        {
            File.WriteAllText(
                configurationPath,
                """
                {
                  "ExternalConfigProbe": {
                    "Value": "loaded"
                  }
                }
                """);
            var configuration = new ConfigurationManager();

            var result = ExternalConfigurationLoader.Add(
                configuration,
                "Production",
                configurationPath);

            Assert.Equal(Path.GetFullPath(configurationPath), result);
            Assert.Equal("loaded", configuration["ExternalConfigProbe:Value"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Add_RejectsRelativePath()
    {
        var configuration = new ConfigurationManager();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExternalConfigurationLoader.Add(configuration, "Production", "server-config.json"));

        Assert.Contains("абсолютный путь", exception.Message);
    }
}
