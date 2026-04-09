namespace MainProject.Application.DTO;

public sealed class LoginResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int UserId { get; init; }
    public string Role { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string OrganizationName { get; init; } = string.Empty;
}
