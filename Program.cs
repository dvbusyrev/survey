using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using MainProject.Infrastructure.Database;
using MainProject.Services.Email;
using MainProject.Services;
using MainProject.Services.Admin;
using MainProject.Services.Answers;
using MainProject.Services.Surveys;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

DefaultTypeMap.MatchNamesWithUnderscores = true;

// Настройка логирования
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
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
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<OrganizationManagementService>();
builder.Services.AddScoped<SurveyExtensionService>();
builder.Services.AddScoped<SurveyAdminService>();
builder.Services.AddScoped<SurveyUserService>();
builder.Services.AddScoped<SurveyArchiveService>();
builder.Services.AddScoped<SurveyAnswersService>();
builder.Services.AddScoped<SurveyReportService>();
builder.Services.AddScoped<AnswerAdminService>();
builder.Services.AddScoped<AnswerDataService>();
builder.Services.AddScoped<AnswerAccessService>();
builder.Services.AddScoped<AnswerWorkflowService>();
builder.Services.AddScoped<AnswerSigningService>();
builder.Services.AddScoped<AnswerExportService>();
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
