using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface IAuthService
{
    LoginResult Authenticate(string username, string password);
}
