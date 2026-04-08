using Microsoft.AspNetCore.Mvc;
using MainProject.Infrastructure.Database;
using MainProject.Infrastructure.Security;
using MainProject.Models;
using System.Data;
using Npgsql;
using NpgsqlTypes;
using System.Security.Cryptography;
using System.Text;

public class UserController : Controller
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserController(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

     [ActionName("get_users")]
     public IActionResult GetUsers()
    {
        List<User> users = new List<User>();

        using (var connection = _connectionFactory.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
command.CommandText = @"
    SELECT 
        id_user,
        (SELECT organization_name FROM public.organization WHERE public.app_user.organization_id = public.organization.organization_id) AS organization_name,
        name_user,
        name_role,
        hash_password,
        date_begin,
        date_end,
        full_name,
        email,
        organization_id
    FROM public.app_user
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
                                IdUser = reader.GetInt32(0),
                                OrganizationName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                NameUser = reader.GetString(2),
                                NameRole = reader.GetString(3),
                                HashPassword = reader.GetString(4),
                                DateBegin = reader.IsDBNull(5) ? null : (DateTime?)reader.GetDateTime(5),
                                DateEnd = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                                FullName = reader.IsDBNull(7) ? null : reader.GetString(7),
                                Email = reader.IsDBNull(8) ? null : reader.GetString(8),
                                OrganizationId = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                            };
                            users.Add(user);
                        }
                        
                    }
                    PopulateOrganizationsViewBag();
                    ViewBag.OpenAddUserModal = false;
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

    [ActionName("add_user")]
    public IActionResult AddUser()
    {
        try
        {
            PopulateOrganizationsViewBag();
            ViewBag.OpenAddUserModal = true;
            return View("get_users", GetActiveUsers());
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при открытии формы добавления пользователя: {ex.Message}" });
        }
    }

[ActionName("update_user")]
public IActionResult UpdateUser(int id)
{
    User user = null;

    using (var connection = _connectionFactory.CreateConnection())
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT u.id_user, u.full_name, u.name_user, u.email, o.organization_name, u.organization_id, u.name_role, u.date_begin, u.date_end, u.hash_password 
            FROM public.app_user u
            JOIN public.organization o ON u.organization_id = o.organization_id
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
                        IdUser = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        NameUser = reader.GetString(2),
                        Email = reader.GetString(3),
                        OrganizationName = reader.GetString(4),
                        OrganizationId = reader.GetInt32(5),
                        NameRole = reader.GetString(6),
                        DateBegin = reader.GetDateTime(7),
                        DateEnd = reader.GetDateTime(8),
                        HashPassword = reader.GetString(9)
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
[ActionName("add_user_bd")]
public IActionResult AddUserBd([FromBody] Dictionary<string, string> formData)
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
        string role = formData?.ContainsKey("role") == true ? formData["role"] : AppRoles.User;
        role = AppRoles.Normalize(role);

        Console.WriteLine($"Обработанные данные: {username}, {password}, {fullName}, {email}");


        if (!AppRoles.IsSupported(role))
        {
            return Json(new
            {
                success = false,
                message = $"Недопустимая роль. Допустимые значения: {string.Join(", ", AppRoles.SupportedRoles)}"
            });
        }

             if (!IsPasswordValid(password, out var passwordError))
             {
                 return Json(new
                 {
                     success = false,
                     message = passwordError
                 });
             }

             string hashedPassword = HashPassword(password);

        using (var connection = _connectionFactory.CreateConnection())
        {
            
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO public.app_user 
                    (organization_id, name_user, full_name, name_role, hash_password, email, date_begin) 
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

private static bool IsPasswordValid(string password, out string errorMessage)
{
    errorMessage = "";

    if (string.IsNullOrWhiteSpace(password))
    {
        errorMessage = "Пароль не должен быть пустым.";
        return false;
    }

    if (password.Length < 14)
    {
        errorMessage = "Пароль должен быть длиной не менее 14 символов.";
        return false;
    }

    if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"\p{Ll}"))
    {
        errorMessage = "Пароль должен содержать хотя бы одну строчную букву.";
        return false;
    }

    if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"\p{Lu}"))
    {
        errorMessage = "Пароль должен содержать хотя бы одну заглавную букву.";
        return false;
    }

    if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]"))
    {
        errorMessage = "Пароль должен содержать хотя бы одну цифру.";
        return false;
    }

    if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[^\p{L}\p{Nd}]"))
    {
        errorMessage = "Пароль должен содержать хотя бы один спецсимвол.";
        return false;
    }

    return true;
}

[ActionName("update_user_bd")]
public IActionResult UpdateUserBd(int id, [FromBody] Dictionary<string, string> formData)
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

        using (var connection = _connectionFactory.CreateConnection())
        {
            
            using (var cmd = connection.CreateCommand())
            {
                var query = new StringBuilder("UPDATE public.app_user SET ");
                var parameters = new List<NpgsqlParameter>();
                
                // Обязательные поля
                query.Append("name_user = @username, full_name = @fullName, organization_id = @organization, ");
                parameters.Add(new NpgsqlParameter("@username", formData["username"]));
                parameters.Add(new NpgsqlParameter("@fullName", formData["fullName"]));
                parameters.Add(new NpgsqlParameter("@organization", int.Parse(formData["organizationId"])));

                // Опциональные поля
                if (formData.ContainsKey("role"))
                {
                    var normalizedRole = AppRoles.Normalize(formData["role"]);
                    if (!AppRoles.IsSupported(normalizedRole))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Недопустимая роль. Допустимые значения: {string.Join(", ", AppRoles.SupportedRoles)}"
                        });
                    }

                    query.Append("name_role = @role, ");
                    parameters.Add(new NpgsqlParameter("@role", normalizedRole));
                }

                // Пароль (если реально меняется)
                if (formData.ContainsKey("password"))
                {
                    var newPassword = formData["password"];

                    if (!string.IsNullOrWhiteSpace(newPassword) && newPassword != "keep_original")
                    {
                        if (!IsPasswordValid(newPassword, out var passwordError))
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = passwordError
                            });
                        }

                        query.Append("hash_password = @password, ");
                        parameters.Add(new NpgsqlParameter("@password", HashPassword(newPassword)));
                    }
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
[ActionName("delete_user")]
public IActionResult DeleteUser(int id)
{
    try
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM public.app_user WHERE id_user = @id";
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

    [ActionName("archive_list_users")]
    public IActionResult ArchiveListUsers()
    {
List<User> users = new List<User>();

        using (var connection = _connectionFactory.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id_user, (SELECT organization_name FROM public.organization WHERE public.app_user.organization_id = public.organization.organization_id) AS organization_name, "+
                "name_user, name_role, hash_password, date_begin, date_end, full_name, email, organization_id FROM public.app_user WHERE date_end < CURRENT_DATE";

                try
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var user = new User
                            {
                                IdUser = reader.GetInt32(0),
                                OrganizationName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                NameUser = reader.GetString(2),
                                NameRole = reader.GetString(3),
                                HashPassword = reader.GetString(4),
                                DateBegin = reader.IsDBNull(5) ? null : (DateTime?)reader.GetDateTime(5),
                                DateEnd = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                                FullName = reader.IsDBNull(7) ? null : reader.GetString(7),
                                Email = reader.IsDBNull(8) ? null : reader.GetString(8),
                                OrganizationId = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
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


    private List<User> GetActiveUsers()
    {
        List<User> users = new List<User>();

        using (var connection = _connectionFactory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT 
                    id_user,
                    (SELECT organization_name FROM public.organization WHERE public.app_user.organization_id = public.organization.organization_id) AS organization_name,
                    name_user,
                    name_role,
                    hash_password,
                    date_begin,
                    date_end,
                    full_name,
                    email,
                    organization_id
                FROM public.app_user
                WHERE date_end IS NULL OR date_end >= CURRENT_DATE;";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    users.Add(new User
                    {
                        IdUser = reader.GetInt32(0),
                        OrganizationName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        NameUser = reader.GetString(2),
                        NameRole = reader.GetString(3),
                        HashPassword = reader.GetString(4),
                        DateBegin = reader.IsDBNull(5) ? null : (DateTime?)reader.GetDateTime(5),
                        DateEnd = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                        FullName = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Email = reader.IsDBNull(8) ? null : reader.GetString(8),
                        OrganizationId = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                    });
                }
            }
        }

        return users;
    }

    private void PopulateOrganizationsViewBag()
    {
        var organizations = new List<object>();

        using (var connection = _connectionFactory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT organization_id, organization_name FROM public.organization WHERE block = false ORDER BY organization_name";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    organizations.Add(new
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1)
                    });
                }
            }
        }

        ViewBag.Organizations = organizations;
    }

        private string HashPassword(string password)
    {
        using (SHA512 sha512 = SHA512.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(password);
            byte[] hash = sha512.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
