using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using MainProject.Infrastructure.Database;
using MainProject.Services.Email;
using MainProject.Services;
using MainProject.Services.Admin;
using MainProject.Services.Answers;
using MainProject.Services.Surveys;
using System.Text.Json;

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

// Настройки сессии
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // время ожидания сессии
    options.Cookie.HttpOnly = true; // защита от доступа к куки через JavaScript (ЛУЧШЕ ОСТАВИТЬ "true")
    options.Cookie.IsEssential = true; // обязательно для работы сессий (СНОВА "true")
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
app.UseSession();
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
