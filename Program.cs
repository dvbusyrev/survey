using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MainProject.Application.Contracts;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.External.Email;
using MainProject.Infrastructure.External.Calendar;
using MainProject.Infrastructure.Security;
using MainProject.Infrastructure.Time;
using MainProject.Application.UseCases;
using MainProject.Application.UseCases.Admin;
using MainProject.Application.UseCases.Answers;
using MainProject.Web.Infrastructure;
using MainProject.Application.UseCases.Surveys;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = ResolveEnvironmentName()
});

var configuredUrls = builder.Configuration["urls"];
builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(configuredUrls)
    ? "http://0.0.0.0:8080"
    : configuredUrls);

DefaultTypeMap.MatchNamesWithUnderscores = true;

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
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
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
        options.EventsType = typeof(ApplicationCookieAuthenticationEvents);
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
var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName("AIS.Anketirovanie");
var configuredDataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(configuredDataProtectionKeysPath))
{
    var dataProtectionKeysPath = Path.IsPathRooted(configuredDataProtectionKeysPath)
        ? configuredDataProtectionKeysPath
        : Path.Combine(builder.Environment.ContentRootPath, configuredDataProtectionKeysPath);
    Directory.CreateDirectory(dataProtectionKeysPath);
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(
            dataProtectionKeysPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddSingleton<SmtpPasswordProtector>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IUserAccessStatusService, UserAccessStatusService>();
builder.Services.AddScoped<ApplicationCookieAuthenticationEvents>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<UserChromeContextService>();
builder.Services.AddScoped<EmailTemplateService>();
builder.Services.AddHostedService<SmtpPasswordMigrationHostedService>();
builder.Services.AddScoped<ThemeSettingsService>();
builder.Services.AddScoped<AuditLogRepository>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<SurveyRepository>();
builder.Services.AddScoped<AnswerRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<OrganizationManagementService>();
builder.Services.AddScoped<SurveyService>();
builder.Services.AddScoped<AnswerService>();
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddHttpClient<ProductionCalendarService>(client =>
{
    var baseUrl = builder.Configuration["ProductionCalendar:BaseUrl"] ?? "https://isdayoff.ru";
    client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

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

public partial class Program
{
}
