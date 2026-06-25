using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Theme;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MainProject.Web.Infrastructure;

namespace MainProject.Tests.Integration.Authorization;

[Collection(MainProject.Tests.Integration.Http.WebApplicationFactoryCollection.Name)]
public sealed class FallbackAuthorizationTests : IClassFixture<FallbackAuthorizationTests.TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public FallbackAuthorizationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminRoute_ReturnsUnauthorized_ForAnonymousRequest()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.SendAsync(CreateApiRequest("/users"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminRoute_ReturnsForbidden_ForNonAdministrator()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = CreateApiRequest("/users");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, AppRoles.User);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void LoginPage_ExplicitlyAllowsAnonymousAccess()
    {
        var action = typeof(AuthController).GetMethod(
            nameof(AuthController.DisplayAuth),
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(action);
        Assert.Contains(action.GetCustomAttributes<AllowAnonymousAttribute>(), _ => true);
    }

    [Fact]
    public async Task HealthEndpoints_AreAnonymousAndExposeLivenessSeparately()
    {
        using var client = _factory.CreateClient();

        using var livenessResponse = await client.GetAsync("/health/live");
        var livenessPayload = JsonDocument.Parse(await livenessResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal("Healthy", livenessPayload.RootElement.GetProperty("status").GetString());
        Assert.Empty(livenessPayload.RootElement.GetProperty("checks").EnumerateObject());

        using var readinessResponse = await client.GetAsync("/health/ready");
        var readinessPayload = JsonDocument.Parse(await readinessResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, readinessResponse.StatusCode);
        Assert.Equal("Unhealthy", readinessPayload.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "Unhealthy",
            readinessPayload.RootElement.GetProperty("checks").GetProperty("postgresql").GetProperty("status").GetString());
    }

    [Fact]
    public async Task UnhandledApiError_ReturnsSafePayloadWithTraceId()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IThemeSettingsService>();
                services.AddScoped<IThemeSettingsService, ThrowingThemeSettingsService>();
            });
        });
        using var client = factory.CreateClient();
        using var request = CreateApiRequest("/settings/theme/data");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, AppRoles.Admin);

        using var response = await client.SendAsync(request);
        var payload = JsonSerializer.Deserialize<ApiErrorResponse>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("Произошла внутренняя ошибка сервера.", payload.Error);
        Assert.False(string.IsNullOrWhiteSpace(payload.TraceId));
        Assert.DoesNotContain("database secret", payload.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpRequestMessage CreateApiRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    public sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _keyRingPath = Path.Combine(Path.GetTempPath(), $"ais-anketirovanie-tests-{Guid.NewGuid():N}");
        private readonly string? _originalKeyRingPath;
        private readonly string? _originalConnectionString;

        public TestApplicationFactory()
        {
            _originalKeyRingPath = Environment.GetEnvironmentVariable("DataProtection__KeyRingPath");
            _originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

            Environment.SetEnvironmentVariable("DataProtection__KeyRingPath", _keyRingPath);
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                "Host=127.0.0.1;Port=1;Database=authorization_test;Username=test");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (Directory.Exists(_keyRingPath))
            {
                Directory.Delete(_keyRingPath, recursive: true);
            }

            Environment.SetEnvironmentVariable("DataProtection__KeyRingPath", _originalKeyRingPath);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _originalConnectionString);
        }
    }

    public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RoleHeaderName = "X-Test-Role";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeaderName, out var role) || string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "test-user"),
                    new Claim(ClaimTypes.Name, "Test user"),
                    new Claim(ClaimTypes.Role, role.ToString())
                ],
                SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class ThrowingThemeSettingsService : IThemeSettingsService
    {
        public Task<ThemeSettings> GetAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database secret must never be sent to the client");

        public Task SaveAsync(ThemeSettings settings, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database secret must never be sent to the client");
    }
}
