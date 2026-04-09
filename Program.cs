using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using MainProject.Application.Contracts;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.External.Email;
using MainProject.Application.UseCases;
using MainProject.Application.UseCases.Admin;
using MainProject.Application.UseCases.Answers;
using MainProject.Application.UseCases.Surveys;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Web/wwwroot"
});

DefaultTypeMap.MatchNamesWithUnderscores = true;

// Настройка логирования
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

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

builder.Services.AddHostedService<SurveyExpirationService>(); // Регистрируем фоновую службу
builder.Services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<LogController>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IOrganizationManagementService, OrganizationManagementService>();
builder.Services.AddScoped<ISurveyExtensionService, SurveyExtensionService>();
builder.Services.AddScoped<ISurveyAdminService, SurveyAdminService>();
builder.Services.AddScoped<ISurveyUserService, SurveyUserService>();
builder.Services.AddScoped<ISurveyArchiveService, SurveyArchiveService>();
builder.Services.AddScoped<ISurveyAnswersService, SurveyAnswersService>();
builder.Services.AddScoped<ISurveyReportService, SurveyReportService>();
builder.Services.AddScoped<IAnswerAdminService, AnswerAdminService>();
builder.Services.AddScoped<AnswerDataService>();
builder.Services.AddScoped<IAnswerAccessService, AnswerAccessService>();
builder.Services.AddScoped<IAnswerWorkflowService, AnswerWorkflowService>();
builder.Services.AddScoped<IAnswerSigningService, AnswerSigningService>();
builder.Services.AddScoped<IAnswerExportService, AnswerExportService>();
builder.Services.AddScoped<EmailSettingsStore>();
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.Configure<SmtpEmailOptions>(builder.Configuration.GetSection(SmtpEmailOptions.SectionName));

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

var app = builder.Build();

// НАСТРОЙКА HTTP ПЕРЕАДРЕСАЦИИ
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("Произошла внутренняя ошибка сервера.");
        });
    });
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=DisplayAuth}/{id?}"
);


app.Run();

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
    if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedForHeader))
    {
        var forwardedFor = forwardedForHeader
            .ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor;
        }
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
