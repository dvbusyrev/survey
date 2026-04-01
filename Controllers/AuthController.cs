using Microsoft.AspNetCore.Mvc;
using main_project.Models;
using System.Data;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

//http://localhost:5161/create_otchet_kvartal

public class AuthController : Controller
{
    private readonly DatabaseController _db;

    public AuthController(DatabaseController db)
    {
        _db = db;
    }

    public IActionResult display_auth()
    {
        if (HttpContext.Session.GetInt32("id_user").HasValue && !string.IsNullOrEmpty(HttpContext.Session.GetString("name_role")))
        {
            var userRole = HttpContext.Session.GetString("name_role");

            if (userRole == "Админ")
            {
                return RedirectToAction("get_surveys", "Survey");
            }
            else if (userRole == "user")
            {
                var userId = HttpContext.Session.GetInt32("id_user");
                Console.WriteLine("открываем вкладку польз");
                return RedirectToAction("survey_list_user", "Survey", new { id = userId });
            }
            else
            {
                Console.WriteLine("Неизвестная роль пользователя.");
                return Ok();
            }
        }
        else
        {
            Console.WriteLine("Открытие начально страницы авторизации");
            return View("Auth");
        }
    }

    public IActionResult logout_account()
    {
        Console.WriteLine("=== Начало процесса выхода ===");
        Console.WriteLine("Сессия удалена");
        HttpContext.Session.Remove("id_user"); // Удаляем id_user
        HttpContext.Session.Remove("name_role"); // Удаляем name_role
        HttpContext.Session.Remove("name_omsu"); // Удаляем name_omsu
        Console.WriteLine("=== Процесс выхода завершен ===");

        return RedirectToAction("display_auth");
    }

    [HttpPost]
    public IActionResult login([FromBody] string[] data_user)
    {
        Console.WriteLine("=== Начало метода login ===");
        
        if (data_user == null)
        {
            Console.WriteLine("Ошибка: data_user is null");
            return StatusCode(400, "Неверный формат данных");
        }

        Console.WriteLine($"Получены данные: длина массива = {data_user.Length}");
        
        if (data_user.Length != 2)
        {
            Console.WriteLine($"Ошибка: Неверная длина массива: {data_user.Length}");
            return StatusCode(400, "Неверный формат данных");
        }

        string username = data_user[0];
        string password = data_user[1];

        Console.WriteLine($"Получены данные - username: {username}, password length: {password?.Length ?? 0}");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Ошибка: Пустые данные авторизации");
            return StatusCode(400, "Имя пользователя и пароль не могут быть пустыми");
        }

        string hashedPassword = HashPassword(password);
        Console.WriteLine($"=== Данные для авторизации ===");
        Console.WriteLine($"Пользователь: {username}");
        Console.WriteLine($"Хеш пароля: {hashedPassword}");

        using (var connection = _db.CreateConnection())
        {
            try
            {
                Console.WriteLine("Попытка подключения к БД");
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                    Console.WriteLine("Подключение к БД успешно");
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT u.id_user, u.name_role, COALESCE(o.name_omsu, '') AS name_omsu
                                            FROM public.users u
                                            LEFT JOIN public.omsu o ON u.id_omsu = o.id_omsu
                                            WHERE u.name_user = @username AND u.hash_password = @password";
                    command.Parameters.Add(new NpgsqlParameter("@username", NpgsqlTypes.NpgsqlDbType.Text) { Value = username });
                    command.Parameters.Add(new NpgsqlParameter("@password", NpgsqlTypes.NpgsqlDbType.Text) { Value = hashedPassword });
                    Console.WriteLine($"Выполняется запрос для пользователя: {username}");

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int idUser = reader.GetInt32(0);
                            string nameRole = reader.GetString(1);
                            string nameOmsu = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                            Console.WriteLine($"=== Успешная авторизация ===");
                            Console.WriteLine($"ID пользователя: {idUser}");
                            Console.WriteLine($"Роль: {nameRole}");
                            Console.WriteLine($"ОМСУ: {nameOmsu}");

                            HttpContext.Session.SetInt32("id_user", idUser);
                            HttpContext.Session.SetString("name_role", nameRole);
                            HttpContext.Session.SetString("name_omsu", nameOmsu);

                            return Json(new { 
                                role = nameRole,
                                userId = idUser,
                                nameOmsu = nameOmsu
                            });
                        }
                        else
                        {
                            Console.WriteLine($"Пользователь не найден в БД: {username}");
                            return StatusCode(401, "Неверное имя пользователя или пароль");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ОШИБКА ПРИ АВТОРИЗАЦИИ ===");
                Console.WriteLine($"Тип ошибки: {ex.GetType().Name}");
                Console.WriteLine($"Сообщение: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, "Ошибка сервера при попытке авторизации");
            }
        }
    }

    private string HashPassword(string password)
    {
        Console.WriteLine("=== Начало хеширования пароля ===");
        using (SHA512 sha512 = SHA512.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(password);
            byte[] hash = sha512.ComputeHash(bytes);
            string result = Convert.ToBase64String(hash);
            Console.WriteLine($"Хеш пароля создан: {result}");
            return result;
        }
    }
} 