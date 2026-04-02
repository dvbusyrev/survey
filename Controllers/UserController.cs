using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using main_project.Models;
using Npgsql;
using NpgsqlTypes;
using System.Data;
using System.Text;

public class UserController : Controller
{
    private readonly DatabaseController _db;
    private static readonly PasswordHasher<string> _passwordHasher = new();

    public UserController(DatabaseController db)
    {
        _db = db;
    }

     public IActionResult get_users()
    {
        List<User> users = new List<User>();

        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
command.CommandText = @"
    SELECT 
        id_user,
        (SELECT name_omsu FROM public.omsu WHERE public.users.id_omsu = public.omsu.id_omsu) AS name_omsu,
        name_user,
        name_role,
        hash_password,
        date_begin,
        date_end,
        full_name,
        email
    FROM public.users
    WHERE date_end IS NULL OR date_end >= CURRENT_DATE;
";


                try
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var user = new User
                            {
                                id_user = reader.GetInt32(0),
                                name_omsu = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                name_user = reader.GetString(2),
                                name_role = reader.GetString(3),
                                hash_password = reader.GetString(4),
date_begin = reader.IsDBNull(5) ? null : (DateTime?)reader.GetDateTime(5),
date_end = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                                full_name = reader.IsDBNull(7) ? null : reader.GetString(7),
                                email = reader.IsDBNull(8) ? null : reader.GetString(8),
                            };
                            users.Add(user);
                        }
                        
                    }
                    return View(users);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return View("Error", new ErrorViewModel { Message = $"Ошибка при получении пользователей: {ex.Message}" });
                }
            }
        }
    }

    public IActionResult add_user()
    {

        List<string> ids = new List<string>();
        List<string> names = new List<string>();

        using (var connection = _db.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id_omsu, name_omsu FROM public.omsu WHERE block = false";

            try
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ids.Add(reader.GetInt32(0).ToString());
                        names.Add(reader.GetString(1));
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Ошибка при получении списка организаций: {ex.Message}" });
            }
        }

    ViewBag.Ids = ids;
    ViewBag.Names = names;

    return View();
    }

public IActionResult update_user(int id)
{
    User user = null;

    using (var connection = _db.CreateConnection())
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT u.id_user, u.full_name, u.name_user, u.email, o.name_omsu, u.id_omsu, u.name_role, u.date_begin, u.date_end, u.hash_password 
            FROM public.users u
            JOIN public.omsu o ON u.id_omsu = o.id_omsu
            WHERE u.id_user = @id";

        command.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });

        try
        {
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    user = new User
                    {
                        id_user = reader.GetInt32(0),
                        full_name = reader.GetString(1),
                        name_user = reader.GetString(2),
                        email = reader.GetString(3),
                        name_omsu = reader.GetString(4),
                        id_omsu = reader.GetInt32(5),
                        name_role = reader.GetString(6),
                        date_begin = reader.GetDateTime(7),
                        date_end = reader.GetDateTime(8),
                        hash_password = reader.GetString(9)
                    };
                }
            }
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении данных пользователя: {ex.Message}" });
        }
    }

    if (user == null)
    {
        return NotFound("Пользователь не найден.");
    }

    return View(user);
}

[HttpPost]
public IActionResult add_user_bd([FromBody] Dictionary<string, string> formData)
{
    try
    {
        Console.WriteLine($"Полученные данные: {Newtonsoft.Json.JsonConvert.SerializeObject(formData)}");

        // Жёсткое получение значений
        string username = formData?.ContainsKey("username") == true ? formData["username"] : "ERROR_NO_USERNAME";
        string password = formData?.ContainsKey("password") == true ? formData["password"] : "ERROR_NO_PASSWORD";
        string fullName = formData?.ContainsKey("fullName") == true ? formData["fullName"] : "ERROR_NO_FULLNAME";
        string email = formData?.ContainsKey("email") == true ? formData["email"] : "ERROR_NO_EMAIL";
        string organizationId = formData?.ContainsKey("organizationId") == true ? formData["organizationId"] : "0";
        string role = formData?.ContainsKey("role") == true ? formData["role"] : "user";

        Console.WriteLine($"Обработанные данные: {username}, {password}, {fullName}, {email}");


             string hashedPassword = _passwordHasher.HashPassword(username, password);

        using (var connection = _db.CreateConnection())
        {
            
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO public.users 
                    (id_omsu, name_user, full_name, name_role, hash_password, email, date_begin) 
                    VALUES (@org, @user, @name, @role, @pwd, @email, NOW() )";
                
                cmd.Parameters.Add(new NpgsqlParameter("@org", NpgsqlDbType.Integer) { 
                    Value = int.TryParse(organizationId, out int orgId) ? orgId : 0 
                });
                cmd.Parameters.Add(new NpgsqlParameter("@user", NpgsqlDbType.Text) { Value = username });
                cmd.Parameters.Add(new NpgsqlParameter("@name", NpgsqlDbType.Text) { Value = fullName });
                cmd.Parameters.Add(new NpgsqlParameter("@role", NpgsqlDbType.Text) { Value = role });
                cmd.Parameters.Add(new NpgsqlParameter("@pwd", NpgsqlDbType.Text) { Value = hashedPassword  });
                cmd.Parameters.Add(new NpgsqlParameter("@email", NpgsqlDbType.Text) { Value = email });
                
                int rowsAffected = cmd.ExecuteNonQuery();
                
                return Json(new { 
                    success = rowsAffected > 0,
                    message = rowsAffected > 0 
                        ? $"Добавлен пользователь: {username}" 
                        : "Не удалось добавить запись в БД"
                });
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка: {ex}");
        return Json(new { 
            success = false,
            message = $"Серверная ошибка: {ex.Message}"
        });
    }
}
[HttpPost]
public IActionResult update_user_bd(int id, [FromBody] Dictionary<string, string> formData)
{
    try
    {
        // Проверка наличия обязательных данных
        if (formData == null || 
            !formData.ContainsKey("username") || 
            !formData.ContainsKey("fullName") ||
            !formData.ContainsKey("organizationId"))
        {
            return BadRequest(new { 
                success = false, 
                message = "Не указаны обязательные данные (логин, ФИО, организация)" 
            });
        }

        using (var connection = _db.CreateConnection())
        {
            
            using (var cmd = connection.CreateCommand())
            {
                var query = new StringBuilder("UPDATE public.users SET ");
                var parameters = new List<NpgsqlParameter>();
                
                // Обязательные поля
                query.Append("name_user = @username, full_name = @fullName, id_omsu = @organization, ");
                parameters.Add(new NpgsqlParameter("@username", formData["username"]));
                parameters.Add(new NpgsqlParameter("@fullName", formData["fullName"]));
                parameters.Add(new NpgsqlParameter("@organization", int.Parse(formData["organizationId"])));

                // Опциональные поля
                if (formData.ContainsKey("role"))
                {
                    query.Append("name_role = @role, ");
                    parameters.Add(new NpgsqlParameter("@role", formData["role"]));
                }

                // Пароль (если не keep_original)
                if (formData.ContainsKey("password") && formData["password"] != "keep_original")
                {
                    query.Append("hash_password = @password, ");
                    parameters.Add(new NpgsqlParameter("@password", _passwordHasher.HashPassword(formData["username"], formData["password"])));
                }

                // Даты
                if (formData.ContainsKey("dateBegin"))
                {
                    query.Append("date_begin = @date_begin, ");
                    parameters.Add(new NpgsqlParameter("@date_begin", 
                        string.IsNullOrEmpty(formData["dateBegin"]) ? 
                        DBNull.Value : (object)DateTime.Parse(formData["dateBegin"])));
                }

                if (formData.ContainsKey("dateEnd"))
                {
                    query.Append("date_end = @date_end, ");
                    parameters.Add(new NpgsqlParameter("@date_end", 
                        string.IsNullOrEmpty(formData["dateEnd"]) ? 
                        DBNull.Value : (object)DateTime.Parse(formData["dateEnd"])));
                }

                // Удаляем последнюю запятую и пробел
                if (query.ToString().EndsWith(", "))
                {
                    query.Remove(query.Length - 2, 2);
                }

                // Добавляем условие WHERE
                query.Append(" WHERE id_user = @id");
                parameters.Add(new NpgsqlParameter("@id", id));

                // Выполняем запрос
                cmd.CommandText = query.ToString();
                foreach (var param in parameters)
                {
                    cmd.Parameters.Add(param);
                }
                
                int rowsAffected = cmd.ExecuteNonQuery();
                
                return Json(new { 
                    success = rowsAffected > 0,
                    message = rowsAffected > 0 ? 
                        "Данные пользователя успешно обновлены" : 
                        "Пользователь не найден или данные не изменились"
                });
            }
        }
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { 
            success = false,
            message = $"Ошибка при обновлении: {ex.Message}"
        });
    }
}

 [HttpPost]
public IActionResult delete_user(int id)
{
    try
    {
        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM public.users WHERE id_user = @id";
                command.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });

                int rowsAffected = command.ExecuteNonQuery();
                if (rowsAffected == 0)
                {
                    return BadRequest("Пользователь с указанным ID не найден.");
                }
            }
        }

        return Ok("Пользователь успешно удален.");
    }
    catch (Exception ex)
    {
        return BadRequest($"Ошибка при удалении пользователя: {ex.Message}");
    }
}

    public IActionResult archive_list_users()
    {
List<User> users = new List<User>();

        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id_user, (SELECT name_omsu FROM public.omsu WHERE public.users.id_omsu = public.omsu.id_omsu) AS name_omsu, "+
                "name_user, name_role, hash_password, date_begin, date_end, full_name, email FROM public.users WHERE date_end < CURRENT_DATE";

                try
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var user = new User
                            {
                                id_user = reader.GetInt32(0),
                                name_omsu = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                name_user = reader.GetString(2),
                                name_role = reader.GetString(3),
                                hash_password = reader.GetString(4),
date_begin = reader.IsDBNull(5) ? null : (DateTime?)reader.GetDateTime(5),
date_end = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                                full_name = reader.IsDBNull(7) ? null : reader.GetString(7),
                                email = reader.IsDBNull(8) ? null : reader.GetString(8),
                            };
                            users.Add(user);
                        }
                        
                    }
                    return View(users);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return View("Error", new ErrorViewModel { Message = $"Ошибка при получении пользователей: {ex.Message}" });
                }
            }
        }
    }
}