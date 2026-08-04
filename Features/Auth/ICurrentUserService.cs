namespace MainProject.Application.Contracts;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    string UserName { get; }
    string Role { get; }
    string OrganizationName { get; }
    bool IsAdmin { get; }
}
