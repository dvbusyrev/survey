using System.Security.Cryptography;
using System.Text;
using Dapper;
using MainProject.Infrastructure.Database;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;

namespace MainProject.Services;

public sealed class AuthService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private static readonly PasswordHasher<string> PasswordHasher = new();

    public AuthService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public LoginResult Authenticate(string username, string password)
    {
        using var connection = _connectionFactory.CreateConnection();

        var user = connection.QueryFirstOrDefault<AuthUserRow>(
            """
            SELECT
                u.id_user AS UserId,
                u.name_role AS Role,
                u.name_user AS UserName,
                COALESCE(o.organization_name, '') AS OrganizationName,
                u.hash_password AS PasswordHash
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON u.organization_id = o.organization_id
            WHERE u.name_user = @username;
            """,
            new { username });

        if (user == null)
        {
            return new LoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                ErrorMessage = "Неверное имя пользователя или пароль"
            };
        }

        var normalizedRole = AppRoles.Normalize(user.Role);
        if (!AppRoles.IsSupported(normalizedRole))
        {
            return new LoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status500InternalServerError,
                ErrorMessage = "Для пользователя задана неподдерживаемая роль"
            };
        }

        var verificationResult = VerifyPassword(user.UserName, user.PasswordHash, password, out var isLegacyHash);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return new LoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                ErrorMessage = "Неверное имя пользователя или пароль"
            };
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded || isLegacyHash)
        {
            connection.Execute(
                """
                UPDATE public.app_user
                SET hash_password = @hash
                WHERE id_user = @id;
                """,
                new
                {
                    id = user.UserId,
                    hash = PasswordHasher.HashPassword(user.UserName, password)
                });
        }

        return new LoginResult
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            UserId = user.UserId,
            Role = normalizedRole,
            UserName = user.UserName,
            OrganizationName = user.OrganizationName
        };
    }

    private static PasswordVerificationResult VerifyPassword(string username, string storedHash, string password, out bool isLegacyHash)
    {
        isLegacyHash = false;

        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return PasswordVerificationResult.Failed;
        }

        try
        {
            var result = PasswordHasher.VerifyHashedPassword(username, storedHash, password);
            if (result != PasswordVerificationResult.Failed)
            {
                return result;
            }
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

    private sealed class AuthUserRow
    {
        public int UserId { get; init; }
        public string Role { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string OrganizationName { get; init; } = string.Empty;
        public string PasswordHash { get; init; } = string.Empty;
    }
}
