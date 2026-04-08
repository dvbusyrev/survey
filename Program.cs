using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.ResponseCompression;
using MainProject.Infrastructure.Database;
using MainProject.Services;
using MainProject.Services.Answers;
using MainProject.Services.Surveys;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

DefaultTypeMap.MatchNamesWithUnderscores = true;

// Настройка логирования
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/display_auth";
        options.LogoutPath = "/Auth/logout_account";
        options.AccessDeniedPath = "/display_auth";
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

                context.Response.Redirect("/display_auth");
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

                context.Response.Redirect("/display_auth");
            }
        };
    });

builder.Services.AddHostedService<SurveyExpirationService>(); // Регистрируем фоновую службу
builder.Services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<LogController>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
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

// СОЗДАНИЕ СОЕДИНЕНИЯ С БД ПРИ СТАРТЕ ПРИЛОЖЕНИЯ
using (var serviceScope = app.Services.CreateScope())
{
    var connectionFactory = serviceScope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    using var _ = connectionFactory.CreateConnection();
}

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

// НИЖЕ ПРЕДСТАВЛЕН СПИСОК МАРШРУТОВ(РОУТОВ) ДЛЯ МЕТОДОВ КОНТРОЛЛЕРОВ
app.MapControllerRoute(
    name: "get_surveys",
    pattern: "get_surveys",
    defaults: new { controller = "SurveyAdmin", action = "get_surveys" }
);

app.MapControllerRoute(
    name: "users_clean",
    pattern: "users",
    defaults: new { controller = "User", action = "get_users" }
);

app.MapControllerRoute(
    name: "users_create_clean",
    pattern: "users/create",
    defaults: new { controller = "User", action = "add_user" }
);

app.MapControllerRoute(
    name: "users_create_post_clean",
    pattern: "users/create/save",
    defaults: new { controller = "User", action = "add_user_bd" }
);

app.MapControllerRoute(
    name: "users_edit_clean",
    pattern: "users/{id}/edit",
    defaults: new { controller = "User", action = "update_user" }
);

app.MapControllerRoute(
    name: "users_update_clean",
    pattern: "users/{id}/update",
    defaults: new { controller = "User", action = "update_user_bd" }
);

app.MapControllerRoute(
    name: "users_delete_clean",
    pattern: "users/{id}/delete",
    defaults: new { controller = "User", action = "delete_user" }
);

app.MapControllerRoute(
    name: "users_archive_clean",
    pattern: "users/archive",
    defaults: new { controller = "User", action = "archive_list_users" }
);

app.MapControllerRoute(
    name: "organizations_clean",
    pattern: "organizations",
    defaults: new { controller = "Organization", action = "get_organization" }
);

app.MapControllerRoute(
    name: "organizations_data_clean",
    pattern: "organizations/data",
    defaults: new { controller = "Organization", action = "get_organization", variantType = "data" }
);

app.MapControllerRoute(
    name: "organizations_create_clean",
    pattern: "organizations/create",
    defaults: new { controller = "Organization", action = "add_organization" }
);

app.MapControllerRoute(
    name: "organizations_create_post_clean",
    pattern: "organizations/create/save",
    defaults: new { controller = "Organization", action = "add_organization_bd" }
);

app.MapControllerRoute(
    name: "organizations_edit_clean",
    pattern: "organizations/{id}/edit",
    defaults: new { controller = "Organization", action = "update_organization" }
);

app.MapControllerRoute(
    name: "organizations_update_clean",
    pattern: "organizations/{id}/update",
    defaults: new { controller = "Organization", action = "update_organization_bd" }
);

app.MapControllerRoute(
    name: "organizations_delete_clean",
    pattern: "organizations/{id}/delete",
    defaults: new { controller = "Organization", action = "delete_organization" }
);

app.MapControllerRoute(
    name: "organizations_archive_clean",
    pattern: "organizations/archive",
    defaults: new { controller = "Organization", action = "archive_list_organizations" }
);

app.MapControllerRoute(
    name: "organizations_variant_clean",
    pattern: "organizations/{variantType}",
    defaults: new { controller = "Organization", action = "get_organization" }
);

app.MapControllerRoute(
    name: "logs_clean",
    pattern: "logs",
    defaults: new { controller = "Log", action = "get_logs" }
);

app.MapControllerRoute(
    name: "logs_export_clean",
    pattern: "logs/export",
    defaults: new { controller = "Log", action = "get_dump_logs" }
);

app.MapControllerRoute(
    name: "mail_settings_clean",
    pattern: "mail-settings",
    defaults: new { controller = "Email", action = "update_settings" }
);

app.MapControllerRoute(
    name: "help_clean",
    pattern: "help",
    defaults: new { controller = "Help", action = "help_page" }
);

app.MapControllerRoute(
    name: "help_page",
    pattern: "help_page",
    defaults: new { controller = "Help", action = "help_page" }
);

app.MapControllerRoute(
    name: "upload-instruction",
    pattern: "upload-instruction",
    defaults: new { controller = "Help", action = "upload_instruction" }
);

app.MapControllerRoute(
    name: "archive_list_users",
    pattern: "archive_list_users",
    defaults: new { controller = "User", action = "archive_list_users" }
);

app.MapControllerRoute(
    name: "archive_list_organizations",
    pattern: "archive_list_organizations",
    defaults: new { controller = "Organization", action = "archive_list_organizations" }
);

app.MapControllerRoute(
    name: "copy_archived_survey",
    pattern: "copy_archived_survey",
    defaults: new { controller = "SurveyArchive", action = "copy_archived_survey" }
);

app.MapControllerRoute(
    name: "help_file_clean",
    pattern: "help/files/{type}",
    defaults: new { controller = "Help", action = "help_file" }
);

app.MapControllerRoute(
    name: "help_file",
    pattern: "help_file/{type}",
    defaults: new { controller = "Help", action = "help_file" }
);

app.MapControllerRoute(
    name: "list_answers_users",
    pattern: "list_answers_users",
    defaults: new { controller = "AnswerAdmin", action = "get_list_answers" }
);

app.MapControllerRoute(
    name: "download_signed_archive",
    pattern: "download_signed_archive/{idSurvey}/{idOrganization}",
    defaults: new { controller = "AnswerExport", action = "DownloadSignedArchive" }
);

app.MapControllerRoute(
    name: "get_signing_data",
    pattern: "get_signing_data/{id}/{idOrganization}",
    defaults: new { controller = "AnswerSigning", action = "GetSigningData" }
);

app.MapControllerRoute(
    name: "archived_surveys",
    pattern: "archived_surveys",
    defaults: new { controller = "SurveyArchive", action = "archived_surveys" }
);

app.MapControllerRoute(
    name: "add_survey",
    pattern: "add_survey",
    defaults: new { controller = "SurveyAdmin", action = "add_survey" }
);


app.MapControllerRoute(
    name: "logout_account",
    pattern: "Auth/logout_account",
    defaults: new { controller = "Auth", action = "logout_account" }
);

app.MapControllerRoute(
    name: "survey_list_user",
    pattern: "survey_list_user/{id}",
    defaults: new { controller = "SurveyUser", action = "survey_list_user" }
);

app.MapControllerRoute(
    name: "open_statistics",
    pattern: "open_statistics",
    defaults: new { controller = "AnswerAdmin", action = "open_statistics" }
);

app.MapControllerRoute(
    name: "add_survey_bd",
    pattern: "add_survey_bd",
    defaults: new { controller = "SurveyAdmin", action = "add_survey_bd" }
);

app.MapControllerRoute(
    name: "get_archived_surveys",
    pattern: "get_archived_surveys/{id}",
    defaults: new { controller = "SurveyArchive", action = "get_archived_surveys" }
);

app.MapControllerRoute(
    name: "get_logs",
    pattern: "get_logs",
    defaults: new { controller = "Log", action = "get_logs" }
);

app.MapControllerRoute(
    name: "get_dump_logs",
    pattern: "get_dump_logs",
    defaults: new { controller = "Log", action = "get_dump_logs" }
);

app.MapControllerRoute(
    name: "get_statistics_data",
    pattern: "get_statistics_data",
    defaults: new { controller = "AnswerAdmin", action = "get_statistics_data" }
);

app.MapControllerRoute(
    name: "get_survey_questions",
    pattern: "get_survey_questions/{id:int}/{organizationId:int}",
    defaults: new { controller = "SurveyUser", action = "get_survey_questions" }
);

app.MapControllerRoute(
    name: "csp",
    pattern: "csp/{id:int}/{idOrganization:int}",
    defaults: new { controller = "AnswerSigning", action = "CSP_answer" }
);

app.MapControllerRoute(
    name: "create_monthly_report",
    pattern: "create_monthly_report/{id:int}",
    defaults: new { controller = "SurveyReports", action = "create_monthly_report" }
);

app.MapControllerRoute(
    name: "create_answer_report",
    pattern: "create_answer_report/{idSurvey}/{idOrganization}/{type}",
    defaults: new { controller = "AnswerExport", action = "create_answer_report" }
);

app.MapControllerRoute(
    name: "update_answer",
    pattern: "update_answer/{idSurvey}/{idOrganization}",
    defaults: new { controller = "AnswerWorkflow", action = "update_answer" }
);


app.MapControllerRoute(
    name: "answers",
    pattern: "answers/{idSurvey}/{idOrganization}/{type}",
    defaults: new { controller = "AnswerWorkflow", action = "answers" }
);


app.MapControllerRoute(
    name: "create_answer_report_archive",
    pattern: "create_answer_report_archive/{idSurvey}/{idOrganization}",
    defaults: new { controller = "AnswerExport", action = "create_answer_report_archive" }
);

app.MapControllerRoute(
    name: "create_monthly_summary_report",
    pattern: "create_monthly_summary_report",
    defaults: new { controller = "SurveyReports", action = "create_monthly_summary_report" }
);

app.MapControllerRoute(
    name: "create_quarterly_report",
    pattern: "create_quarterly_report/{quarter}/{year}",
    defaults: new { controller = "SurveyReports", action = "create_quarterly_report" }
);

app.MapControllerRoute(
    name: "send_message",
    pattern: "send_message",
    defaults: new { controller = "Email", action = "send_message" }
);

app.MapControllerRoute(
    name: "view_reports",
    pattern: "view_reports",
    defaults: new { controller = "SurveyReports", action = "view_reports" }
);

app.MapControllerRoute(
    name: "update_email",
    pattern: "update_email",
    defaults: new { controller = "Email", action = "update_settings" }
);

app.MapControllerRoute(
    name: "update_survey",
    pattern: "update_survey/{id}",
    defaults: new { controller = "SurveyAdmin", action = "update_survey" }
);

app.MapControllerRoute(
    name: "update_survey_bd",
    pattern: "update_survey_bd/{id}",
    defaults: new { controller = "SurveyAdmin", action = "update_survey_bd" }
);
app.MapControllerRoute(
    name: "copy_survey",
    pattern: "copy_survey/{id}",
    defaults: new { controller = "SurveyAdmin", action = "copy_survey" }
);

app.MapControllerRoute(
    name: "copy_survey_bd",
    pattern: "copy_survey_bd/{id}",
    defaults: new { controller = "SurveyAdmin", action = "copy_survey_bd" }
);

app.MapControllerRoute(
    name: "delete_survey",
    pattern: "surveys/delete/{id}",
    defaults: new { controller = "SurveyAdmin", action = "delete_survey" }
);

app.MapControllerRoute(
    name: "add_organization_bd",
    pattern: "add_organization_bd",
    defaults: new { controller = "Organization", action = "add_organization_bd" }
);

app.MapControllerRoute(
    name: "update_organization",
    pattern: "update_organization/{id}",
    defaults: new { controller = "Organization", action = "update_organization" }
);

app.MapControllerRoute(
    name: "update_organization_bd",
    pattern: "update_organization_bd/{id}",
    defaults: new { controller = "Organization", action = "update_organization_bd" }
);

app.MapControllerRoute(
    name: "emailSettings",
    pattern: "save_email_settings",
    defaults: new { controller = "Email", action = "SaveEmailSettings" });

app.MapControllerRoute(
    name: "getEmailSettings",
    pattern: "email_settings.txt",
    defaults: new { controller = "Email", action = "GetEmailSettings" });

app.MapControllerRoute(
    name: "surveyAnswers",
    pattern: "Survey/GetSurveyAnswers",
    defaults: new { controller = "SurveyAnswers", action = "GetSurveyAnswers" });

app.MapControllerRoute(
    name: "update_user",
    pattern: "update_user/{id}",
    defaults: new { controller = "User", action = "update_user" });

app.MapControllerRoute(
    name: "update_user_bd",
    pattern: "update_user_bd/{id}",
    defaults: new { controller = "User", action = "update_user_bd" });


app.MapControllerRoute(
    name: "delete_organization",
    pattern: "delete_organization/{id}",
    defaults: new { controller = "Organization", action = "delete_organization" });

app.MapControllerRoute(
    name: "delete_user",
    pattern: "delete_user/{id}",
    defaults: new { controller = "User", action = "delete_user" });

app.MapControllerRoute(
    name: "insert_answer",
    pattern: "api/insert_answer", 
    defaults: new { controller = "AnswerWorkflow", action = "insert_answer" });

app.MapControllerRoute(
    name: "update_answer_bd",
    pattern: "update_answer_bd", 
    defaults: new { controller = "AnswerWorkflow", action = "update_answer_bd" });

app.MapControllerRoute(
    name: "get_list_answers",
    pattern: "get_list_answers",
    defaults: new { controller = "AnswerAdmin", action = "get_list_answers"}
);
app.MapControllerRoute(
    name: "get_survey_signatures",
    pattern: "get_survey_signatures/{id}",
    defaults: new { controller = "AnswerAdmin", action = "get_survey_signatures" });

app.MapControllerRoute(
    name: "get_users",
    pattern: "get_users",
    defaults: new { controller = "User", action = "get_users"});

app.MapControllerRoute(
    name: "add_user_bd",
    pattern: "add_user_bd",
    defaults: new { controller = "User", action = "add_user_bd"});


app.MapControllerRoute(
    name: "send_message",
    pattern: "send_message",
    defaults: new { controller = "Email", action = "send_message"});

app.MapControllerRoute(
    name: "help_file",
    pattern: "help_file/{type}",
    defaults: new { controller = "Help", action = "help_file"});

app.MapControllerRoute(
    name: "add_user",
    pattern: "add_user",
    defaults: new { controller = "User", action = "add_user"});

app.MapControllerRoute(
    name: "add_organization",
    pattern: "add_organization",
    defaults: new { controller = "Organization", action = "add_organization"});


app.MapControllerRoute(
    name: "get_organization",
    pattern: "get_organization",
    defaults: new { controller = "Organization", action = "get_organization" });

app.MapControllerRoute(
    name: "get_organization",
    pattern: "get_organization/{variantType}",
    defaults: new { controller = "Organization", action = "get_organization" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=display_auth}/{id?}"
);

app.MapControllerRoute(
    name: "save_survey_extensions",
    pattern: "survey-extensions",
    defaults: new { controller = "SurveyExtension", action = "SaveSurveyExtensions" });
    
app.MapControllerRoute(
    name: "display_auth",
    pattern: "display_auth",
    defaults: new { controller = "Auth", action = "display_auth"});

app.MapControllerRoute(
    name: "login",
    pattern: "Auth/login",
    defaults: new { controller = "Auth", action = "login"});


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

    if (request.Headers.Accept.Any(value => value.Contains("application/json", StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    return false;
}
