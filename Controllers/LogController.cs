using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Infrastructure.Database;
using main_project.Infrastructure.Security;
using main_project.Models;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Text;
using Npgsql;


[Authorize(Roles = AppRoles.Admin)]
public class LogController : Controller
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LogController(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }


// ДОБАВЛЕНИЕ ЛОГА В БАЗУ ДАННЫХ
public IActionResult insert_log(int id_user, int id_target, string target_type, string event_type, DateTime date, object extra_data, string description)
{

    Console.WriteLine("Начинаю добавление лога...");
    try
    {
        string extraDataJson = extra_data != null ? JObject.FromObject(extra_data).ToString() : null;

        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO public.log (id_user, id_target, target_type, event_type, date, extra_data, description)
            VALUES (@id_user, @id_target, @target_type, @event_type, @date, @extra_data, @description);
        ";

        command.Parameters.Add(new NpgsqlParameter("@id_user", id_user));
        command.Parameters.Add(new NpgsqlParameter("@id_target", id_target));
        command.Parameters.Add(new NpgsqlParameter("@target_type", target_type));
        command.Parameters.Add(new NpgsqlParameter("@event_type", event_type));
        command.Parameters.Add(new NpgsqlParameter("@date", date));
        command.Parameters.Add(new NpgsqlParameter("@extra_data", extraDataJson));
        command.Parameters.Add(new NpgsqlParameter("@description", description));

        command.ExecuteNonQuery();

        return Ok();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при вставке лога: {ex.Message}");
        return StatusCode(500, $"Ошибка при вставке лога: {ex.Message}");
    }
}


public IActionResult get_logs()
{
    List<Log> logs = new List<Log>();
    using var connection = _connectionFactory.CreateConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT "+
                            "l.id_log, "+
                            "l.id_user, "+
                            "l.id_target, "+
                            "l.target_type, "+
                            "l.event_type, "+
                            "l.date, "+
                            "l.extra_data, "+
                            "l.description, "+
                            "(SELECT u.name_user "+
                            "FROM public.app_user u "+
                            "WHERE u.id_user = l.id_user) AS name_user,"+
                            "(SELECT s.name_survey "+
                            "FROM public.survey s "+
                            "WHERE s.id_survey = l.id_target) AS name_survey "+
                        "FROM "+
                            "public.log l;";
    try
    {
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var log = new Log
            {
                id_log = reader.GetInt32(0),
                id_user = reader.GetInt32(1),
                id_target = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                target_type = reader.GetString(3),
                event_type = reader.GetString(4),
                date = reader.GetDateTime(5),
                extra_data = reader.IsDBNull(6) ? "Нет данных" : JObject.Parse(reader.GetString(6)),
                description = reader.GetString(7),
                name_user = reader.IsDBNull(8) ? "Нет данных" : reader.GetString(8),
                name_survey = reader.IsDBNull(9) ? "Нет данных" : reader.GetString(9),
            };
            logs.Add(log);
        }
    }
    catch (Exception ex)
    {
                    Console.WriteLine(ex.Message);
        return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка логов: {ex.Message}" });
    }

    return View(logs);
}

public IActionResult get_dump_logs()
{
    List<Log> logs = new List<Log>();
    using var connection = _connectionFactory.CreateConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT "+
                            "l.id_log, "+
                            "l.id_user, "+
                            "l.id_target, "+
                            "l.target_type, "+
                            "l.event_type, "+
                            "l.date, "+
                            "l.extra_data, "+
                            "l.description, "+
                            "(SELECT u.name_user "+
                            "FROM public.app_user u "+
                            "WHERE u.id_user = l.id_user) AS name_user,"+
                            "(SELECT s.name_survey "+
                            "FROM public.survey s "+
                            "WHERE s.id_survey = l.id_target) AS name_survey "+
                        "FROM "+
                            "public.log l;";
    try
    {
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var log = new Log
            {
                id_log = reader.GetInt32(0),
                id_user = reader.GetInt32(1),
                id_target = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                target_type = reader.GetString(3),
                event_type = reader.GetString(4),
                date = reader.GetDateTime(5),
                extra_data = reader.IsDBNull(6) ? "Нет данных" : JObject.Parse(reader.GetString(6)),
                description = reader.GetString(7),
                name_user = reader.IsDBNull(8) ? "Нет данных" : reader.GetString(8),
                name_survey = reader.IsDBNull(9) ? "Нет данных" : reader.GetString(9),
            };
            logs.Add(log);
        }
    }
    catch (Exception ex)
    {
        return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка логов: {ex.Message}" });
    }

    // Преобразуем логи в текстовый формат
    var logText = GenerateLogText(logs);

    // Сохраняем текст в файл
    var fileName = $"logs_dump_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "dumps", fileName);

    // Создаем директорию, если она не существует
    var directoryPath = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrEmpty(directoryPath))
    {
        Directory.CreateDirectory(directoryPath);
    }

    // Записываем текст в файл
    System.IO.File.WriteAllText(filePath, logText);

    // Возвращаем файл для скачивания
    var fileBytes = System.IO.File.ReadAllBytes(filePath);
    return File(fileBytes, "text/plain", fileName);
}

private string GenerateLogText(List<Log> logs)
{
    var sb = new StringBuilder();

    foreach (var log in logs)
    {
        if (log.event_type == "LOGIN_ERROR")
        {
            sb.AppendLine($"{log.date} Ошибка входа в систему пользователя (ID: {log.id_log}). Причина: {log.description}");
        }
        else if (log.event_type == "USER_SURVEY_ERROR")
        {
            sb.AppendLine($"{log.date} Пользователь (ID: {log.id_log}) не смог завершить прохождение анкеты {log.name_survey}({log.id_target}). Причина: {log.description}");
        }
        else if (log.event_type == "SURVEY_UPDATE")
        {
            sb.AppendLine($"{log.date} Администратор {log.name_user}({log.id_log}) обновил анкету {log.name_survey}({log.id_target}). Подробности: {FormatExtraData(log.extra_data)}");
        }
        else if (log.event_type == "SURVEY_ISSUE")
        {
            sb.AppendLine($"{log.date} Администратор {log.name_user}({log.id_log}) выдал анкету {log.name_survey}({log.id_target}) на прохождение. Подробности: {FormatExtraData(log.extra_data)}");
        }
        else if (log.event_type == "SURVEY_OVERDUE")
        {
            sb.AppendLine($"{log.date} Анкета {log.name_survey}({log.id_target}) перенесена в архив. Причина: {log.description}");
        }
        else if (log.event_type == "BLOCK_Organization" || log.event_type == "BLOCK_ORGANIZATION")
        {
            sb.AppendLine($"{log.date} Организация {log.id_target} заблокирована. Подробности: {FormatExtraData(log.extra_data)}");
        }
        else
        {
            // Если тип события неизвестен, добавляем общую информацию
            sb.AppendLine($"{log.date} Неизвестное событие {log.event_type}. Описание: {log.description}");
        }
    }

    return sb.ToString();
}

private string FormatExtraData(object extraData)
{
    if (extraData is JObject jObject)
    {
        var sb = new StringBuilder();

        // Проверяем, содержит ли extra_data ключи "new" и "old"
        if (jObject.ContainsKey("new") && jObject.ContainsKey("old"))
        {
            var newData = jObject["new"] as JObject;
            var oldData = jObject["old"] as JObject;

            // Форматируем старые атрибуты
            sb.Append("Старые атрибуты: (");
            foreach (var property in oldData.Properties())
            {
                sb.Append($"{property.Name}: {property.Value}, ");
            }
            sb.Length -= 2; // Убираем последнюю запятую и пробел
            sb.Append(")");
            sb.AppendLine();
            
            // Форматируем новые атрибуты
            sb.Append("Новые атрибуты: (");
            foreach (var property in newData.Properties())
            {
                sb.Append($"{property.Name}: {property.Value}, ");
            }
            sb.Length -= 2; // Убираем последнюю запятую и пробел
            sb.Append(")");
            sb.AppendLine();
        }
        else
        {
            // Если нет ключей "new" и "old", обрабатываем как обычный JSON
            foreach (var property in jObject.Properties())
            {
                sb.AppendLine($"{property.Name}: {property.Value}");
            }
        }

        return sb.ToString();
    }
    else
    {
        // Если extra_data не является JObject, возвращаем его как есть
        return extraData?.ToString() ?? "Нет данных";
    }
}
}
