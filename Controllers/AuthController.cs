using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

public class AuthController : Controller
{
    private readonly DatabaseController _db;
    private static readonly PasswordHasher<string> _passwordHasher = new();

    public AuthController(DatabaseController db)
    {
        _db = db;
    }

    public IActionResult display_auth()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userRole == "Админ")
            {
                return RedirectToAction("get_surveys", "Survey");
            }
            else if (userRole == "user" && !string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("survey_list_user", "Survey", new { id = userId });
            }
        }

        return View("Auth");
    }

    public async Task<IActionResult> logout_account()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("display_auth");
    }

    [HttpPost]
    public async Task<IActionResult> login([FromBody] string[] data_user)
    {
        if (data_user == null || data_user.Length != 2)
            return StatusCode(400, "Неверный формат данных");

        string username = data_user[0];
        string password = data_user[1];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return StatusCode(400, "Имя пользователя и пароль не могут быть пустыми");

        using var connection = _db.CreateConnection();

        try
        {
            if (connection.State != ConnectionState.Open)
                connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT u.id_user, u.name_role, u.name_user, COALESCE(o.name_omsu, '') AS name_omsu, u.hash_password
                                    FROM public.users u
                                    LEFT JOIN public.omsu o ON u.id_omsu = o.id_omsu
                                    WHERE u.name_user = @username";
            command.Parameters.Add(new NpgsqlParameter("@username", NpgsqlTypes.NpgsqlDbType.Text) { Value = username });

            int idUser;
            string nameRole;
            string nameUser;
            string nameOmsu;
            string storedHash;

            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                    return StatusCode(401, "Неверное имя пользователя или пароль");

                idUser = reader.GetInt32(0);
                nameRole = reader.GetString(1);
                nameUser = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                nameOmsu = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                storedHash = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
            }

            bool isLegacyHash;
            var verify = VerifyPassword(username, storedHash, password, out isLegacyHash);
            if (verify == PasswordVerificationResult.Failed)
                return StatusCode(401, "Неверное имя пользователя или пароль");

            if (verify == PasswordVerificationResult.SuccessRehashNeeded || isLegacyHash)
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE public.users SET hash_password = @hash WHERE id_user = @id";
                updateCommand.Parameters.Add(new NpgsqlParameter("@hash", NpgsqlTypes.NpgsqlDbType.Text)
                {
                    Value = _passwordHasher.HashPassword(username, password)
                });
                updateCommand.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Integer) { Value = idUser });
                updateCommand.ExecuteNonQuery();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, idUser.ToString()),
                new Claim(ClaimTypes.Name, nameUser),
                new Claim(ClaimTypes.Role, nameRole),
                new Claim("omsu_name", nameOmsu)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Json(new
            {
                role = nameRole,
                userId = idUser,
                nameUser,
                nameOmsu
            });
        }
        catch
        {
            return StatusCode(500, "Ошибка сервера при попытке авторизации");
        }
    }

    private PasswordVerificationResult VerifyPassword(string username, string storedHash, string password, out bool isLegacyHash)
    {
        isLegacyHash = false;

        if (string.IsNullOrWhiteSpace(storedHash))
            return PasswordVerificationResult.Failed;

        try
        {
            var result = _passwordHasher.VerifyHashedPassword(username, storedHash, password);
            if (result != PasswordVerificationResult.Failed)
                return result;
        }
        catch
        {
        }

        if (storedHash == ComputeLegacySha512(password))
        {
            isLegacyHash = true;
            return PasswordVerificationResult.SuccessRehashNeeded;
        }

        return PasswordVerificationResult.Failed;
    }

    private static string ComputeLegacySha512(string password)
    {
        using var sha512 = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha512.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
