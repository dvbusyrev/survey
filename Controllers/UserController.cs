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
        try
        {
            ViewBag.OpenAddUserModal = false;
            PopulateOrganizationsViewBag();
            return View(GetActiveUsers());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении пользователей: {ex.Message}" });
        }
    }

    public IActionResult add_user()
    {
        try
        {
            ViewBag.OpenAddUserModal = true;
            PopulateOrganizationsViewBag();
            return View("get_users", GetActiveUsers());
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при открытии формы добавления пользователя: {ex.Message}" });
        }
    }

    public IActionResult update_user(int id)
    {
        User? user = null;

        using var connection = _db.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT u.id_user, u.full_name, u.name_user, u.email, COALESCE(o.name_omsu, '') AS name_omsu,
                   u.id_omsu, u.name_role, u.date_begin, u.date_end, u.hash_password
            FROM public.users u
            LEFT JOIN public.omsu o ON u.id_omsu = o.id_omsu
            WHERE u.id_user = @id";
        command.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Integer) { Value = id });

        try
        {
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                user = new User
                {
                    id_user = reader.GetInt32(0),
                    full_name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    name_user = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    email = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    name_omsu = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    id_omsu = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    name_role = reader.IsDBNull(6) ? "user" : reader.GetString(6),
                    date_begin = reader.IsDBNull(7) ? null : (DateTime?)reader.GetDateTime(7),
                    date_end = reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8),
                    hash_password = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                };
            }
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении данных пользователя: {ex.Message}" });
        }

        if (user == null)
        {
            return NotFound("Пользователь не найден.");
        }

        PopulateOrganizationsViewBag();
        return View(user);
    }

    [HttpPost]
    public IActionResult add_user_bd([FromBody] Dictionary<string, string> formData)
    {
        try
        {
            Console.WriteLine($"Полученные данные: {Newtonsoft.Json.JsonConvert.SerializeObject(formData)}");

            string username = formData?.ContainsKey("username") == true ? formData["username"] : "ERROR_NO_USERNAME";
            string password = formData?.ContainsKey("password") == true ? formData["password"] : "ERROR_NO_PASSWORD";
            string fullName = formData?.ContainsKey("fullName") == true ? formData["fullName"] : "ERROR_NO_FULLNAME";
            string email = formData?.ContainsKey("email") == true ? formData["email"] : string.Empty;
            string organizationId = formData?.ContainsKey("organizationId") == true ? formData["organizationId"] : "0";
            string role = formData?.ContainsKey("role") == true ? formData["role"] : "user";

            if (!IsPasswordValid(password, out var passwordError))
            {
                return Json(new
                {
                    success = false,
                    message = passwordError
                });
            }

            string hashedPassword = HashPassword(password);

            using var connection = _db.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO public.users
                (id_omsu, name_user, full_name, name_role, hash_password, email, date_begin)
                VALUES (@org, @user, @name, @role, @pwd, @email, NOW())";

            cmd.Parameters.Add(new NpgsqlParameter("@org", NpgsqlDbType.Integer)
            {
                Value = int.TryParse(organizationId, out int orgId) ? orgId : 0
            });
            cmd.Parameters.Add(new NpgsqlParameter("@user", NpgsqlDbType.Text) { Value = username });
            cmd.Parameters.Add(new NpgsqlParameter("@name", NpgsqlDbType.Text) { Value = fullName });
            cmd.Parameters.Add(new NpgsqlParameter("@role", NpgsqlDbType.Text) { Value = role });
            cmd.Parameters.Add(new NpgsqlParameter("@pwd", NpgsqlDbType.Text) { Value = hashedPassword });
            cmd.Parameters.Add(new NpgsqlParameter("@email", NpgsqlDbType.Text) { Value = string.IsNullOrWhiteSpace(email) ? string.Empty : email });

            int rowsAffected = cmd.ExecuteNonQuery();

            return Json(new
            {
                success = rowsAffected > 0,
                message = rowsAffected > 0
                    ? $"Добавлен пользователь: {username}"
                    : "Не удалось добавить запись в БД"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex}");
            return Json(new
            {
                success = false,
                message = $"Серверная ошибка: {ex.Message}"
            });
        }
    }

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

        if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[a-z]"))
        {
            errorMessage = "Пароль должен содержать хотя бы одну строчную латинскую букву.";
            return false;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Z]"))
        {
            errorMessage = "Пароль должен содержать хотя бы одну заглавную латинскую букву.";
            return false;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]"))
        {
            errorMessage = "Пароль должен содержать хотя бы одну цифру.";
            return false;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
        {
            errorMessage = "Пароль должен содержать хотя бы один спецсимвол.";
            return false;
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(password, "[А-Яа-яЁё]"))
        {
            errorMessage = "Пароль должен содержать только латинские буквы.";
            return false;
        }

        return true;
    }

    [HttpPost]
    public IActionResult update_user_bd(int id, [FromBody] Dictionary<string, string> formData)
    {
        try
        {
            if (formData == null ||
                !formData.ContainsKey("username") ||
                !formData.ContainsKey("fullName") ||
                !formData.ContainsKey("organizationId"))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Не указаны обязательные данные (логин, ФИО, организация)"
                });
            }

            using var connection = _db.CreateConnection();
            using var cmd = connection.CreateCommand();

            var query = new StringBuilder("UPDATE public.users SET ");
            var parameters = new List<NpgsqlParameter>
            {
                new("@username", formData["username"]),
                new("@fullName", formData["fullName"]),
                new("@organization", int.Parse(formData["organizationId"]))
            };

            query.Append("name_user = @username, full_name = @fullName, id_omsu = @organization, ");

            if (formData.ContainsKey("role"))
            {
                query.Append("name_role = @role, ");
                parameters.Add(new NpgsqlParameter("@role", formData["role"]));
            }

            if (formData.ContainsKey("email"))
            {
                query.Append("email = @email, ");
                parameters.Add(new NpgsqlParameter("@email", string.IsNullOrWhiteSpace(formData["email"]) ? DBNull.Value : (object)formData["email"]));
            }

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

            if (formData.ContainsKey("dateBegin"))
            {
                query.Append("date_begin = @date_begin, ");
                parameters.Add(new NpgsqlParameter("@date_begin",
                    string.IsNullOrEmpty(formData["dateBegin"])
                        ? DBNull.Value
                        : (object)DateTime.Parse(formData["dateBegin"])));
            }

            if (formData.ContainsKey("dateEnd"))
            {
                query.Append("date_end = @date_end, ");
                parameters.Add(new NpgsqlParameter("@date_end",
                    string.IsNullOrEmpty(formData["dateEnd"])
                        ? DBNull.Value
                        : (object)DateTime.Parse(formData["dateEnd"])));
            }

            if (query.ToString().EndsWith(", "))
            {
                query.Remove(query.Length - 2, 2);
            }

            query.Append(" WHERE id_user = @id");
            parameters.Add(new NpgsqlParameter("@id", id));

            cmd.CommandText = query.ToString();
            foreach (var param in parameters)
            {
                cmd.Parameters.Add(param);
            }

            int rowsAffected = cmd.ExecuteNonQuery();

            return Json(new
            {
                success = rowsAffected > 0,
                message = rowsAffected > 0
                    ? "Данные пользователя успешно обновлены"
                    : "Пользователь не найден или данные не изменились"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
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
            using var connection = _db.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM public.users WHERE id_user = @id";
            command.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Integer) { Value = id });

            int rowsAffected = command.ExecuteNonQuery();
            if (rowsAffected == 0)
            {
                return BadRequest("Пользователь с указанным ID не найден.");
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
        var users = new List<User>();

        using var connection = _db.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT u.id_user,
                                       COALESCE(o.name_omsu, '') AS name_omsu,
                                       u.id_omsu,
                                       u.name_user,
                                       u.name_role,
                                       u.hash_password,
                                       u.date_begin,
                                       u.date_end,
                                       u.full_name,
                                       u.email
                                FROM public.users u
                                LEFT JOIN public.omsu o ON u.id_omsu = o.id_omsu
                                WHERE u.date_end < CURRENT_DATE";

        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new User
                {
                    id_user = reader.GetInt32(0),
                    name_omsu = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    id_omsu = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    name_user = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    name_role = reader.IsDBNull(4) ? "user" : reader.GetString(4),
                    hash_password = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    date_begin = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                    date_end = reader.IsDBNull(7) ? null : (DateTime?)reader.GetDateTime(7),
                    full_name = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    email = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                });
            }

            return View(users);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении пользователей: {ex.Message}" });
        }
    }

    private static string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(string.Empty, password);
    }

    private List<User> GetActiveUsers()
    {
        var users = new List<User>();

        using var connection = _db.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT u.id_user,
                   COALESCE(o.name_omsu, '') AS name_omsu,
                   u.id_omsu,
                   u.name_user,
                   u.name_role,
                   u.hash_password,
                   u.date_begin,
                   u.date_end,
                   u.full_name,
                   u.email
            FROM public.users u
            LEFT JOIN public.omsu o ON u.id_omsu = o.id_omsu
            WHERE u.date_end IS NULL OR u.date_end >= CURRENT_DATE
            ORDER BY u.full_name NULLS LAST, u.name_user";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new User
            {
                id_user = reader.GetInt32(0),
                name_omsu = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                id_omsu = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                name_user = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                name_role = reader.IsDBNull(4) ? "user" : reader.GetString(4),
                hash_password = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                date_begin = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                date_end = reader.IsDBNull(7) ? null : (DateTime?)reader.GetDateTime(7),
                full_name = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                email = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
            });
        }

        return users;
    }

    private void PopulateOrganizationsViewBag()
    {
        var organizations = new List<object>();

        using var connection = _db.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id_omsu, name_omsu FROM public.omsu WHERE block = false ORDER BY name_omsu";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            organizations.Add(new
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        ViewBag.Organizations = organizations;
    }
}
