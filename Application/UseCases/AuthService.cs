using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;

namespace MainProject.Application.UseCases;

public sealed class AuthService : IAuthService
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

        var verificationResult = VerifyPassword(user.UserName, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return new LoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                ErrorMessage = "Неверное имя пользователя или пароль"
            };
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
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

    private static PasswordVerificationResult VerifyPassword(string username, string storedHash, string password)
    {
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
            return PasswordVerificationResult.Failed;
        }

        return PasswordVerificationResult.Failed;
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
