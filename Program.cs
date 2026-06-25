using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MainProject.Application.Contracts;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.Health;
using MainProject.Infrastructure.External.Email;
using MainProject.Infrastructure.Time;
using MainProject.Application.UseCases;
using MainProject.Application.UseCases.Admin;
using MainProject.Application.UseCases.Answers;
using MainProject.Web.Infrastructure;
using MainProject.Application.UseCases.Surveys;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Web/wwwroot",
    EnvironmentName = ResolveEnvironmentName()
});

var configuredUrls = builder.Configuration["urls"];
builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(configuredUrls)
    ? "http://0.0.0.0:8080"
    : configuredUrls);

DefaultTypeMap.MatchNamesWithUnderscores = true;

var dataProtectionOptions = builder.Configuration.GetSection("DataProtection");
var dataProtectionKeyRingPath = dataProtectionOptions["KeyRingPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeyRingPath))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Для production требуется DataProtection:KeyRingPath в постоянном хранилище.");
    }

    dataProtectionKeyRingPath = "data-protection-keys";
}

var resolvedKeyRingPath = Path.IsPathRooted(dataProtectionKeyRingPath)
    ? dataProtectionKeyRingPath
    : Path.Combine(builder.Environment.ContentRootPath, dataProtectionKeyRingPath);
Directory.CreateDirectory(resolvedKeyRingPath);
var dataProtectionApplicationName = dataProtectionOptions["ApplicationName"] ?? "AIS.Anketirovanie";
var trustedProxyAddresses = builder.Configuration
    .GetSection("ReverseProxy:KnownProxies")
    .Get<string[]>() ?? [];
ProductionConfigurationValidator.EnsureAllowedHosts(
    builder.Environment,
    builder.Configuration["AllowedHosts"]);

// Настройка логирования
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
    options.JsonWriterOptions = new JsonWriterOptions
    {
        Indented = false
    };
});

if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
})
.AddRazorOptions(options =>
{
    options.ViewLocationFormats.Clear();
    options.ViewLocationFormats.Add("/Web/Views/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Web/Views/Shared/{0}.cshtml");
    options.ViewLocationFormats.Add("/Web/Views/{0}.cshtml");
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    ForwardedHeadersConfiguration.Configure(options, trustedProxyAddresses);
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString();
        }

        const string message = "Слишком много попыток входа. Попробуйте снова через минуту.";
        context.HttpContext.Response.ContentType = IsApiRequest(context.HttpContext.Request)
            ? "application/json; charset=utf-8"
            : "text/plain; charset=utf-8";

        if (context.HttpContext.Response.ContentType.StartsWith("application/json", StringComparison.Ordinal))
        {
            await context.HttpContext.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = message }),
                cancellationToken);
            return;
        }

        await context.HttpContext.Response.WriteAsync(message, cancellationToken);
    };
    options.AddPolicy("login-attempts", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetLoginRateLimitPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Name = ".AIS.Anketirovanie.Auth";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = async context =>
            {
                if (IsApiRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Требуется авторизация. Выполните вход снова." }));
                    return;
                }

                context.Response.Redirect("/");
            },
            OnRedirectToAccessDenied = async context =>
            {
                if (IsApiRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Доступ запрещён." }));
                    return;
                }

                context.Response.Redirect("/");
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddHostedService<SurveyAutoCreationHostedService>();
builder.Services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<LogController>();
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(resolvedKeyRingPath))
    .SetApplicationName(dataProtectionApplicationName);
builder.Services.AddHealthChecks()
    .AddCheck<PostgreSqlHealthCheck>("postgresql", tags: ["ready"]);
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IUserChromeContextService, UserChromeContextService>();
builder.Services.AddScoped<IUserChromeRepository, UserChromeRepository>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IThemeSettingsService, ThemeSettingsService>();
builder.Services.AddScoped<IEmailConfigRepository, EmailConfigRepository>();
builder.Services.AddScoped<IThemeConfigRepository, ThemeConfigRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<ISurveyDefinitionRepository, SurveyDefinitionRepository>();
builder.Services.AddScoped<IAutoCreationConfigRepository, AutoCreationConfigRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISurveyAutoCreationService, SurveyAutoCreationService>();
builder.Services.AddScoped<ISurveyAssignmentRepository, SurveyAssignmentRepository>();
builder.Services.AddScoped<IAnswerRepository, AnswerRepository>();
builder.Services.AddScoped<IAnswerReadRepository, AnswerReadRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IOrganizationManagementService, OrganizationManagementService>();
builder.Services.AddScoped<ISurveyExtensionService, SurveyExtensionService>();
builder.Services.AddScoped<ISurveyAdminService, SurveyAdminService>();
builder.Services.AddScoped<ISurveyUserService, SurveyUserService>();
builder.Services.AddScoped<ISurveyArchiveService, SurveyArchiveService>();
builder.Services.AddScoped<ISurveyAnswersService, SurveyAnswersService>();
builder.Services.AddScoped<ISurveyReportRepository, SurveyReportRepository>();
builder.Services.AddScoped<ISurveyReportService, SurveyReportService>();
builder.Services.AddScoped<IAnswerAdminService, AnswerAdminService>();
builder.Services.AddScoped<AnswerDataService>();
builder.Services.AddScoped<IAnswerAccessService, AnswerAccessService>();
builder.Services.AddScoped<IAnswerWorkflowService, AnswerWorkflowService>();
builder.Services.AddScoped<IAnswerSigningService, AnswerSigningService>();
builder.Services.AddScoped<IAnswerExportService, AnswerExportService>();
builder.Services.AddScoped<SmtpEmailSender>();

// Сжатие ответов для ускорения загрузки
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/javascript",
        "text/javascript"
    });
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("ru-RU") };

    options.DefaultRequestCulture = new RequestCulture("ru-RU");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
});

var app = builder.Build();

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    var startedAt = Stopwatch.GetTimestamp();
    var traceId = context.TraceIdentifier;
    context.Response.Headers.TryAdd("X-Trace-Id", traceId);

    using (app.Logger.BeginScope(new Dictionary<string, object?>
    {
        ["TraceId"] = traceId,
        ["RequestMethod"] = context.Request.Method,
        ["RequestPath"] = context.Request.Path.Value ?? "/",
        ["ClientIp"] = ClientIpAddressResolver.Resolve(context)
    }))
    {
        try
        {
            await next();
        }
        finally
        {
            app.Logger.LogInformation(
                "HTTP {RequestMethod} {RequestPath} from {ClientIp} completed with {StatusCode} in {ElapsedMilliseconds} ms. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                ClientIpAddressResolver.Resolve(context),
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                traceId);
        }
    }
});

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var payload = ApiErrorResponse.Create(context, "Произошла внутренняя ошибка сервера.");
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("UnhandledRequest");

        if (exception != null)
        {
            logger.LogError(exception, "Необработанная ошибка запроса. TraceId: {TraceId}", payload.TraceId);
        }

        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain; charset=utf-8";

        if (IsApiRequest(context.Request))
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            return;
        }

        await context.Response.WriteAsync($"Произошла внутренняя ошибка сервера. Trace ID: {payload.TraceId}");
    });
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
app.UseCookiePolicy();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    if (response.HasStarted)
    {
        return;
    }

    var message = response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized => "Требуется авторизация. Выполните вход снова.",
        StatusCodes.Status403Forbidden => "Доступ запрещён.",
        StatusCodes.Status404NotFound => "Страница не найдена.",
        StatusCodes.Status500InternalServerError => "Произошла внутренняя ошибка сервера.",
        _ => "Произошла ошибка при обработке запроса."
    };

    response.ContentType = IsApiRequest(statusCodeContext.HttpContext.Request)
        ? "application/json; charset=utf-8"
        : "text/plain; charset=utf-8";

    if (response.ContentType.StartsWith("application/json", StringComparison.Ordinal))
    {
        await response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
        return;
    }

    await response.WriteAsync(message);
});

app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
}).AllowAnonymous();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
}).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=DisplayAuth}/{id?}"
);


app.Run();

static string? ResolveEnvironmentName()
{
    var aspNetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (!string.IsNullOrWhiteSpace(aspNetEnvironment))
    {
        return aspNetEnvironment;
    }

    var dotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    if (!string.IsNullOrWhiteSpace(dotNetEnvironment))
    {
        return dotNetEnvironment;
    }

#if DEBUG
    return "Development";
#else
    return null;
#endif
}

static bool IsApiRequest(HttpRequest request)
{
    if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (request.Headers.TryGetValue("X-Requested-With", out var requestedWith) &&
        string.Equals(requestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return false;
}

static string GetLoginRateLimitPartitionKey(HttpContext context)
{
    return ClientIpAddressResolver.Resolve(context);
}

public partial class Program
{
}
