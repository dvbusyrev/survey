using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Read;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;

namespace MainProject.Application.UseCases;

public sealed class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private static readonly PasswordHasher<string> PasswordHasher = new();

    public AuthService(IAuthRepository authRepository)
    {
        _authRepository = authRepository;
    }

    public async Task<LoginResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _authRepository.GetByLoginAsync(username, cancellationToken);

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
            await _authRepository.UpdatePasswordHashAsync(
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
