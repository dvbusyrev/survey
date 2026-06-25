using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Theme;
using MainProject.Application.UseCases.Answers;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MainProject.Tests.Integration.Http;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class WorkflowHttpTests
{
    private const string WorkflowIdentityRole = "csrf-test-user";

    [Fact]
    public async Task Login_WithAntiforgeryToken_SetsCookieAndRedirectsAuthenticatedVisitor()
    {
        await using var factory = new LoginApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var antiforgery = await GetAntiforgeryTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new[] { "smoke-admin", "SmokePass1!" })
        };
        AddAntiforgeryHeaders(request, antiforgery);

        using var response = await client.SendAsync(request);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AppRoles.Admin, payload.RootElement.GetProperty("role").GetString());
        var authCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(".AIS.Anketirovanie.Auth=", StringComparison.Ordinal))
            .Split(';', 2)[0];

        using var authenticatedRequest = new HttpRequestMessage(HttpMethod.Get, "/");
        authenticatedRequest.Headers.TryAddWithoutValidation("Cookie", authCookie);
        using var authenticatedVisit = await client.SendAsync(authenticatedRequest);
        Assert.Equal(HttpStatusCode.Redirect, authenticatedVisit.StatusCode);
        Assert.Equal("/survey", authenticatedVisit.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ProtectedWorkflowRoute_ReturnsUnauthorizedOrForbiddenBeforeServiceExecution()
    {
        await using var factory = new WorkflowApplicationFactory();

        using (var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        }))
        using (var anonymousRequest = CreateJsonRequest(HttpMethod.Post, "/answers/draft", CreateAnswerRecord()))
        {
            using var anonymousResponse = await anonymousClient.SendAsync(anonymousRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var antiforgery = await GetAntiforgeryTokenAsync(client, WorkflowIdentityRole);
        using var forbiddenRequest = CreateJsonRequest(HttpMethod.Post, "/answers/draft", CreateAnswerRecord());
        forbiddenRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, WorkflowIdentityRole);
        AddAntiforgeryHeaders(forbiddenRequest, antiforgery);
        factory.AnswerAccess.AllowSubmission = false;

        using var forbiddenResponse = await client.SendAsync(forbiddenRequest);

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(0, factory.Workflow.SaveDraftCalls);
    }

    [Fact]
    public async Task WorkflowPosts_RequireAntiforgeryToken()
    {
        await using var factory = new WorkflowApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        _ = await GetAntiforgeryTokenAsync(client);
        using var request = CreateJsonRequest(HttpMethod.Post, "/answers/draft", CreateAnswerRecord());
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, AppRoles.User);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Workflow.SaveDraftCalls);
    }

    [Fact]
    public async Task ClientAnswerDraftAndSignature_WithAntiforgeryToken_UseWorkflowServices()
    {
        await using var factory = new WorkflowApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var antiforgery = await GetAntiforgeryTokenAsync(client, WorkflowIdentityRole);

        using var draftRequest = CreateJsonRequest(HttpMethod.Post, "/answers/draft", CreateAnswerRecord());
        AddUserAndAntiforgeryHeaders(draftRequest, antiforgery);
        using var draftResponse = await client.SendAsync(draftRequest);

        using var answerRequest = CreateJsonRequest(HttpMethod.Post, "/answers/create", CreateAnswerRecord());
        AddUserAndAntiforgeryHeaders(answerRequest, antiforgery);
        answerRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var answerResponse = await client.SendAsync(answerRequest);

        using var signatureRequest = CreateJsonRequest(
            HttpMethod.Post,
            "/draft-signatures/7/10",
            new AnswerSignatureSaveRequest { Signature = Convert.ToBase64String("signature"u8.ToArray()) });
        AddUserAndAntiforgeryHeaders(signatureRequest, antiforgery);
        using var signatureResponse = await client.SendAsync(signatureRequest);

        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, answerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signatureResponse.StatusCode);
        Assert.Equal(1, factory.Workflow.SaveDraftCalls);
        Assert.Equal(1, factory.Workflow.InsertAnswerCalls);
        Assert.Equal(1, factory.Signing.SaveDraftSignatureCalls);
    }

    [Fact]
    public async Task WorkflowException_UsesSafeApiErrorWithoutInternalDetails()
    {
        await using var factory = new WorkflowApplicationFactory();
        factory.Workflow.ThrowOnSaveDraft = true;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var antiforgery = await GetAntiforgeryTokenAsync(client, WorkflowIdentityRole);
        using var request = CreateJsonRequest(HttpMethod.Post, "/answers/draft", CreateAnswerRecord());
        AddUserAndAntiforgeryHeaders(request, antiforgery);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Не удалось сохранить черновик.", body);
        Assert.DoesNotContain("database password", body, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddUserAndAntiforgeryHeaders(HttpRequestMessage request, AntiforgeryToken antiforgery)
    {
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, WorkflowIdentityRole);
        AddAntiforgeryHeaders(request, antiforgery);
    }

    private static void AddAntiforgeryHeaders(HttpRequestMessage request, AntiforgeryToken antiforgery)
    {
        request.Headers.Add("RequestVerificationToken", antiforgery.Value);
        request.Headers.TryAddWithoutValidation("Cookie", antiforgery.Cookie);
    }

    private static HttpRequestMessage CreateJsonRequest<T>(HttpMethod method, string path, T body) =>
        new(method, path)
        {
            Content = JsonContent.Create(body)
        };

    private static AnswerRecord CreateAnswerRecord() => new()
    {
        IdSurvey = 7,
        OrganizationId = 10,
        Answers =
        [
            new AnswerPayloadItem
            {
                QuestionId = "1",
                QuestionText = "Вопрос",
                Rating = 5
            }
        ]
    };

    private static async Task<AntiforgeryToken> GetAntiforgeryTokenAsync(HttpClient client, string? role = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        if (!string.IsNullOrWhiteSpace(role))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        }

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, "The authentication page must render an antiforgery token.");
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookieValues));
        var cookie = Assert.Single(setCookieValues).Split(';', 2)[0];
        return new AntiforgeryToken(WebUtility.HtmlDecode(match.Groups["token"].Value), cookie);
    }

    private sealed record AntiforgeryToken(string Value, string Cookie);

    private sealed class LoginApplicationFactory : ApplicationFactoryBase
    {
        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.RemoveAll<IAuthService>();
            services.AddSingleton<IAuthService, SuccessfulAuthService>();
        }
    }

    private sealed class WorkflowApplicationFactory : ApplicationFactoryBase
    {
        public FakeAnswerAccessService AnswerAccess { get; } = new();
        public FakeAnswerWorkflowService Workflow { get; } = new();
        public FakeAnswerSigningService Signing { get; } = new();

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.RemoveAll<IAnswerAccessService>();
            services.RemoveAll<IAnswerWorkflowService>();
            services.RemoveAll<IAnswerSigningService>();
            services.AddSingleton<IAnswerAccessService>(AnswerAccess);
            services.AddSingleton<IAnswerWorkflowService>(Workflow);
            services.AddSingleton<IAnswerSigningService>(Signing);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ => { });
        }
    }

    private abstract class ApplicationFactoryBase : WebApplicationFactory<Program>
    {
        private readonly string _keyRingPath = Path.Combine(Path.GetTempPath(), $"ais-anketirovanie-http-tests-{Guid.NewGuid():N}");
        private readonly string? _originalKeyRingPath;
        private readonly string? _originalConnectionString;

        protected ApplicationFactoryBase()
        {
            _originalKeyRingPath = Environment.GetEnvironmentVariable("DataProtection__KeyRingPath");
            _originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            Environment.SetEnvironmentVariable("DataProtection__KeyRingPath", _keyRingPath);
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                "Host=127.0.0.1;Port=1;Database=http_test;Username=test");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IThemeSettingsService>();
                services.AddSingleton<IThemeSettingsService, FixedThemeSettingsService>();
                ConfigureTestServices(services);
            });
        }

        protected abstract void ConfigureTestServices(IServiceCollection services);

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

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "WorkflowTest";
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
                new Claim(ClaimTypes.NameIdentifier, "42"),
                new Claim(ClaimTypes.Name, "HTTP test user"),
                new Claim(ClaimTypes.Role, role.ToString())
            ], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class SuccessfulAuthService : IAuthService
    {
        public Task<LoginResult> AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LoginResult
            {
                Success = username == "smoke-admin" && password == "SmokePass1!",
                StatusCode = username == "smoke-admin" && password == "SmokePass1!" ? 200 : 401,
                ErrorMessage = "Неверное имя пользователя или пароль",
                UserId = 1,
                Role = AppRoles.Admin,
                UserName = "Smoke admin",
                OrganizationName = "Тестовая организация"
            });
    }

    private sealed class FixedThemeSettingsService : IThemeSettingsService
    {
        public Task<ThemeSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ThemeSettings());

        public Task SaveAsync(ThemeSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAnswerAccessService : IAnswerAccessService
    {
        public bool AllowSubmission { get; set; } = true;
        public bool IsAuthenticated => true;
        public bool IsAdmin => false;
        public int? UserId => 42;

        public Task<int?> GetCurrentUserOrganizationIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(10);
        public Task<bool> CanAccessOrganizationAsync(int requestedOrganizationId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> CanSubmitAnswerAsync(int surveyId, int requestedOrganizationId, CancellationToken cancellationToken = default) => Task.FromResult(AllowSubmission);
        public Task<bool> CanAccessAnswerRecordAsync(int surveyId, int requestedOrganizationId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeAnswerWorkflowService : IAnswerWorkflowService
    {
        public int InsertAnswerCalls { get; private set; }
        public int SaveDraftCalls { get; private set; }
        public bool ThrowOnSaveDraft { get; set; }

        public Task<AnswerMutationResult> InsertAnswerAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default)
        {
            InsertAnswerCalls++;
            return Task.FromResult(new AnswerMutationResult { Success = true });
        }

        public Task<AnswerMutationResult> UpdateAnswerAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AnswerMutationResult { Success = true });

        public Task<AnswerMutationResult> SaveDraftAnswerAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default)
        {
            SaveDraftCalls++;
            if (ThrowOnSaveDraft)
            {
                throw new InvalidOperationException("database password must never leave the server");
            }

            return Task.FromResult(new AnswerMutationResult { Success = true });
        }

        public Task<AnswerRecord?> GetDraftAnswerAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AnswerRecord?>(null);

        public Task<MainProject.Web.ViewModels.UpdateAnswerPageViewModel?> GetUpdateAnswerPageAsync(
            int surveyId,
            int organizationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MainProject.Web.ViewModels.UpdateAnswerPageViewModel?>(null);

        public Task<SurveyAnswersResponse> GetAnswersResponseAsync(
            int surveyId,
            int organizationId,
            string? type,
            bool includeAllOrganizationAnswers,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SurveyAnswersResponse { Success = true });
    }

    private sealed class FakeAnswerSigningService : IAnswerSigningService
    {
        public int SaveDraftSignatureCalls { get; private set; }

        public Task<AnswerSigningPayload> GetSigningDataAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AnswerSigningPayload());

        public Task<bool> SaveSignatureAsync(int surveyId, int organizationId, AnswerSignatureSaveRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<AnswerSigningPayload> GetDraftSigningDataAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AnswerSigningPayload());

        public Task<bool> SaveDraftSignatureAsync(int surveyId, int organizationId, AnswerSignatureSaveRequest request, CancellationToken cancellationToken = default)
        {
            SaveDraftSignatureCalls++;
            return Task.FromResult(true);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WebApplicationFactoryCollection
{
    public const string Name = "Web application factory";
}
