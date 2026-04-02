using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using main_project.Data;
using main_project.Models;
using main_project.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка логирования
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// НИЖЕ ПРЕДСТАВЛЕН СПИСОК ИСПОЛЬЗУЮЩИХСЯ СЕРВИСОВ
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth";
        options.LogoutPath = "/Auth/logout_account";
        options.AccessDeniedPath = "/Auth";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

builder.Services.AddHostedService<SurveyExpirationService>(); // Регистрируем фоновую службу
builder.Services.AddScoped<DatabaseController>();
builder.Services.AddSingleton<DatabaseConnection>();
builder.Services.AddScoped<LogController>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();

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
    var dbConnection = serviceScope.ServiceProvider.GetRequiredService<DatabaseConnection>();
    dbConnection.CreateConnection();
}

// НАСТРОЙКА HTTP ПЕРЕАДРЕСАЦИИ
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// НИЖЕ ПРЕДСТАВЛЕН СПИСОК МАРШРУТОВ(РОУТОВ) ДЛЯ МЕТОДОВ КОНТРОЛЛЕРОВ
app.MapControllerRoute(
    name: "get_surveys",
    pattern: "get_surveys",
    defaults: new { controller = "Survey", action = "get_surveys" }
);

app.MapControllerRoute(
    name: "page_help",
    pattern: "page_help",
    defaults: new { controller = "Help", action = "page_help" }
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
    name: "archive_list_omsus",
    pattern: "archive_list_omsus",
    defaults: new { controller = "OMSU", action = "archive_list_omsus" }
);

app.MapControllerRoute(
    name: "copy_archive_survey",
    pattern: "copy_archive_survey",
    defaults: new { controller = "Survey", action = "copy_archive_survey" }
);

app.MapControllerRoute(
    name: "get_file",
    pattern: "get_file/{type}",
    defaults: new { controller = "Help", action = "get_file" }
);

app.MapControllerRoute(
    name: "list_answers_users",
    pattern: "list_answers_users",
    defaults: new { controller = "Survey", action = "list_answers_users" }
);

app.MapControllerRoute(
    name: "download_signed_archive",
    pattern: "download_signed_archive/{idSurvey}/{idOmsu}",
    defaults: new { controller = "Answer", action = "DownloadSignedArchive" }
);

app.MapControllerRoute(
    name: "get_signing_data",
    pattern: "get_signing_data/{id}/{idOmsu}",
    defaults: new { controller = "Answer", action = "GetSigningData" }
);

app.MapControllerRoute(
    name: "archiv_surveys",
    pattern: "archiv_surveys",
    defaults: new { controller = "Survey", action = "archiv_surveys" }
);

app.MapControllerRoute(
    name: "add_survey",
    pattern: "add_survey",
    defaults: new { controller = "Survey", action = "add_survey" }
);


app.MapControllerRoute(
    name: "logout_account",
    pattern: "Auth/logout_account",
    defaults: new { controller = "Auth", action = "logout_account" }
);

app.MapControllerRoute(
    name: "survey_list_user",
    pattern: "survey_list_user/{id}",
    defaults: new { controller = "Survey", action = "survey_list_user" }
);

app.MapControllerRoute(
    name: "open_statistic",
    pattern: "open_statistic",
    defaults: new { controller = "Answer", action = "open_statistic" }
);

app.MapControllerRoute(
    name: "add_survey_bd",
    pattern: "add_survey_bd",
    defaults: new { controller = "Survey", action = "add_survey_bd" }
);

app.MapControllerRoute(
    name: "get_list_archive",
    pattern: "get_list_archive/{id}",
    defaults: new { controller = "Survey", action = "get_list_archive" }
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
    name: "rassilka_page",
    pattern: "rassilka_page",
    defaults: new { controller = "Email", action = "rassilka_page" }
);

app.MapControllerRoute(
    name: "get_data_statistic",
    pattern: "get_data_statistic",
    defaults: new { controller = "Answer", action = "get_data_statistic" }
);

app.MapControllerRoute(
    name: "zapolnenie_anketi",
    pattern: "zapolnenie_anketi/{id:int}/{omsuId:int}",
    defaults: new { controller = "Survey", action = "zapolnenie_anketi" }
);

app.MapControllerRoute(
    name: "csp",
    pattern: "csp/{id:int}/{idOmsu:int}/{signature}",
    defaults: new { controller = "Answer", action = "CSP_answer" }
);

app.MapControllerRoute(
    name: "create_otchet_month",
    pattern: "create_otchet_month/{id:int}",
    defaults: new { controller = "Survey", action = "create_otchet_month" }
);

app.MapControllerRoute(
    name: "create_otchet_for_me",
    pattern: "create_otchet_for_me/{idSurvey}/{idOmsu}/{type}",
    defaults: new { controller = "Answer", action = "create_otchet_for_me" }
);

app.MapControllerRoute(
    name: "update_answer",
    pattern: "update_answer/{idSurvey}/{idOmsu}",
    defaults: new { controller = "Answer", action = "update_answer" }
);


app.MapControllerRoute(
    name: "answers",
    pattern: "answers/{idSurvey}/{idOmsu}/{type}",
    defaults: new { controller = "Answer", action = "answers" }
);


app.MapControllerRoute(
    name: "create_archiv_for_me",
    pattern: "create_archiv_for_me/{idSurvey}/{idOmsu}",
    defaults: new { controller = "Answer", action = "create_archiv_for_me" }
);

app.MapControllerRoute(
    name: "create_otchetAll_month",
    pattern: "create_otchetAll_month",
    defaults: new { controller = "Survey", action = "create_otchetAll_month" }
);

app.MapControllerRoute(
    name: "create_otchet_kvartal",
    pattern: "create_otchet_kvartal/{kvartal}/{year}",
    defaults: new { controller = "Survey", action = "create_otchet_kvartal" }
);

app.MapControllerRoute(
    name: "send_message",
    pattern: "send_message",
    defaults: new { controller = "Email", action = "send_message" }
);

app.MapControllerRoute(
    name: "view_otchets",
    pattern: "view_otchets",
    defaults: new { controller = "Survey", action = "view_otchets" }
);

app.MapControllerRoute(
    name: "update_email",
    pattern: "update_email",
    defaults: new { controller = "Email", action = "update_settings" }
);

app.MapControllerRoute(
    name: "update_survey",
    pattern: "update_survey/{id}",
    defaults: new { controller = "Survey", action = "update_survey" }
);

app.MapControllerRoute(
    name: "update_survey_bd",
    pattern: "update_survey_bd/{id}",
    defaults: new { controller = "Survey", action = "update_survey_bd" }
);
app.MapControllerRoute(
    name: "copy_survey",
    pattern: "copy_survey/{id}",
    defaults: new { controller = "Survey", action = "copy_survey" }
);

app.MapControllerRoute(
    name: "copy_survey_bd",
    pattern: "copy_survey_bd/{id}",
    defaults: new { controller = "Survey", action = "copy_survey_bd" }
);

app.MapControllerRoute(
    name: "delete_survey",
    pattern: "surveys/delete/{id}",
    defaults: new { controller = "Survey", action = "delete_survey" }
);

app.MapControllerRoute(
    name: "add_omsu_bd",
    pattern: "add_omsu_bd",
    defaults: new { controller = "OMSU", action = "add_omsu_bd" }
);

app.MapControllerRoute(
    name: "update_omsu",
    pattern: "update_omsu/{id}",
    defaults: new { controller = "OMSU", action = "update_omsu" }
);

app.MapControllerRoute(
    name: "update_omsu_bd",
    pattern: "update_omsu_bd/{id}",
    defaults: new { controller = "OMSU", action = "update_omsu_bd" }
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
    defaults: new { controller = "Survey", action = "GetSurveyAnswers" });

app.MapControllerRoute(
    name: "update_user",
    pattern: "update_user/{id}",
    defaults: new { controller = "User", action = "update_user" });

app.MapControllerRoute(
    name: "update_user_bd",
    pattern: "update_user_bd/{id}",
    defaults: new { controller = "User", action = "update_user_bd" });


app.MapControllerRoute(
    name: "delete_omsu",
    pattern: "delete_omsu/{id}",
    defaults: new { controller = "OMSU", action = "delete_omsu" });

app.MapControllerRoute(
    name: "delete_user",
    pattern: "delete_user/{id}",
    defaults: new { controller = "User", action = "delete_user" });

app.MapControllerRoute(
    name: "insert_answer",
    pattern: "api/insert_answer", 
    defaults: new { controller = "Answer", action = "insert_answer" });

app.MapControllerRoute(
    name: "update_answer_bd",
    pattern: "update_answer_bd", 
    defaults: new { controller = "Answer", action = "update_answer_bd" });

app.MapControllerRoute(
    name: "get_list_answers",
    pattern: "get_list_answers",
    defaults: new { controller = "Answer", action = "get_list_answers"}
);
app.MapControllerRoute(
    name: "get_list_csp",
    pattern: "get_list_csp/{id}",
    defaults: new { controller = "Answer", action = "get_list_csp" });

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
    name: "get_file",
    pattern: "get_file/{type}",
    defaults: new { controller = "Help", action = "get_file"});

app.MapControllerRoute(
    name: "add_user",
    pattern: "add_user",
    defaults: new { controller = "User", action = "add_user"});

app.MapControllerRoute(
    name: "add_omsu",
    pattern: "add_omsu",
    defaults: new { controller = "OMSU", action = "add_omsu"});


app.MapControllerRoute(
    name: "get_omsu",
    pattern: "get_omsu",
    defaults: new { controller = "OMSU", action = "get_omsu" });

app.MapControllerRoute(
    name: "get_omsu",
    pattern: "get_omsu/{variantType}",
    defaults: new { controller = "OMSU", action = "get_omsu" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=display_auth}/{id?}"
);

app.MapControllerRoute(
    name: "prodlenie_omsus",
    pattern: "prodlenie_omsus",
    defaults: new { controller = "Ae", action = "prodlenie_omsus"});

app.MapControllerRoute(
    name: "get_organizations",
    pattern: "get_organizations",
    defaults: new { controller = "Ae", action = "GetOrganizations" });
    
app.MapControllerRoute(
    name: "display_auth",
    pattern: "display_auth",
    defaults: new { controller = "Auth", action = "display_auth"});

app.MapControllerRoute(
    name: "auth",
    pattern: "",
    defaults: new { controller = "Auth", action = "display_auth" });

app.MapControllerRoute(
    name: "login",
    pattern: "Auth/login",
    defaults: new { controller = "Auth", action = "login"});


app.Run();