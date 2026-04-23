using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface IUserChromeContextService
{
    UserChromeContext GetCurrentContext();
}
