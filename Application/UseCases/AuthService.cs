using Dapper;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Security;
using MainProject.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace MainProject.Application.UseCases;

public class AuthService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private static readonly PasswordHasher<string> PasswordHasher = new();

    protected AuthService()
    {
        _connectionFactory = null!;
    }

    public AuthService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public virtual async Task<LoginResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await GetByLoginAsync(username, cancellationToken);

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
            await UpdatePasswordHashAsync(
                user.UserId,
                PasswordHasher.HashPassword(user.UserName, password),
                cancellationToken);
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

    private async Task<AuthUserRecord?> GetByLoginAsync(string login, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<AuthUserRecord>(new CommandDefinition(
            """
            SELECT
                u.id_user AS UserId,
                u.role AS Role,
                u.login AS UserName,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, '') AS OrganizationName,
                u.password AS PasswordHash
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON u.id_organization = o.id_organization
            WHERE u.login = @Login;
            """,
            new { Login = login },
            cancellationToken: cancellationToken));
    }

    private async Task UpdatePasswordHashAsync(
        int userId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.app_user
            SET password = @PasswordHash
            WHERE id_user = @UserId;
            """,
            new { UserId = userId, PasswordHash = passwordHash },
            cancellationToken: cancellationToken));
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
}

internal sealed record AuthUserRecord(
    int UserId,
    string Role,
    string UserName,
    string OrganizationName,
    string PasswordHash);
