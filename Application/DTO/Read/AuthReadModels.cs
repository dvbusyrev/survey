namespace MainProject.Application.DTO.Read;

public sealed record AuthUserRecord(
    int UserId,
    string Role,
    string UserName,
    string OrganizationName,
    string PasswordHash);
