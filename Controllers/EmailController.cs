using Microsoft.AspNetCore.Authorization;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using main_project.Infrastructure.Security;
using main_project.Models;

namespace main_project.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class EmailController : Controller
    {

[HttpPost]
public IActionResult SaveEmailSettings([FromBody] EmailSettingsModel settings)
{
    try
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "email_settings.txt");
        var settingsText = $"Кому: {settings.to}\nТема: {settings.subject}\nСодержание: {settings.content}";
        
        System.IO.File.WriteAllText(filePath, settingsText);
        return Ok(new { success = true });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { success = false, error = ex.Message });
    }
}

public class EmailSettingsModel
{
    public string to { get; set; }
    public string subject { get; set; }
    public string content { get; set; }
}

[HttpGet]
public IActionResult GetEmailSettings()
{
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "email_settings.txt");
    
    if (!System.IO.File.Exists(filePath))
    {
        System.IO.File.WriteAllText(filePath, "Кому:\nТема:\nСодержание:");
    }
    
    return PhysicalFile(filePath, "text/plain");
}



[ValidateAntiForgeryToken]
    public async Task<IActionResult> send_message()
    {
        try
        {
            // 1. Чтение данных из файла
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "email_settings.txt");
            
            if (!System.IO.File.Exists(filePath))
            {
                return BadRequest(new { message = "Файл с данными email_settings.txt не найден" });
            }

            var lines = await System.IO.File.ReadAllLinesAsync(filePath);
            
            string to = "", subject = "", body = "";
            
            foreach (var line in lines)
            {
                if (line.StartsWith("Кому:")) to = line.Substring(5).Trim();
                else if (line.StartsWith("Тема:")) subject = line.Substring(5).Trim();
                else if (line.StartsWith("Содержание:")) body = line.Substring(11).Trim();
            }

            // 2. Проверка данных
            if (string.IsNullOrEmpty(to))
                return BadRequest(new { message = "Не указан получатель в файле" });

            // 3. Настройка и отправка письма
            using (var mail = new MailMessage())
            {
                mail.From = new MailAddress("DRXPost@cherepovetscity.ru", "drxpost");
                mail.To.Add(to);
                mail.Subject = string.IsNullOrEmpty(subject) ? "Без темы" : subject;
                mail.Body = string.IsNullOrEmpty(body) ? "Пустое письмо" : body;
                mail.IsBodyHtml = true;

                using (var smtp = new SmtpClient("mx.cherepovetscity.ru", 587))
                {
                    smtp.Credentials = new NetworkCredential("drxpost", "Lbhtrnev0");
                    smtp.EnableSsl = true;
                    await smtp.SendMailAsync(mail);
                }
            }

            return Ok(new { message = $"Письмо успешно отправлено на {to}!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Ошибка: {ex.Message}" });
        }
    }
                public async Task<IActionResult> update_settings()
        {
            return View("update_settings");
        }
    }
}
