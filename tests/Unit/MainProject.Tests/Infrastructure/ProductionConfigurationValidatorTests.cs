using MainProject.Web.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace MainProject.Tests.Infrastructure;

public sealed class ProductionConfigurationValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("survey.example.ru;*.example.ru")]
    public void EnsureAllowedHosts_RejectsMissingOrWildcardValuesInProduction(string? allowedHosts)
    {
        var environment = new TestHostEnvironment(Environments.Production);

        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.EnsureAllowedHosts(environment, allowedHosts));
    }

    [Fact]
    public void EnsureAllowedHosts_AllowsConcreteProductionHosts()
    {
        var environment = new TestHostEnvironment(Environments.Production);

        ProductionConfigurationValidator.EnsureAllowedHosts(
            environment,
            "survey.example.ru;survey.internal.example.ru");
    }

    [Fact]
    public void EnsureAllowedHosts_DoesNotRestrictDevelopmentWildcard()
    {
        var environment = new TestHostEnvironment(Environments.Development);

        ProductionConfigurationValidator.EnsureAllowedHosts(environment, "*");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "MainProject.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
