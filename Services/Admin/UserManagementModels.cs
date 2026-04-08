using System.Text.Json.Serialization;
using MainProject.Models;

namespace MainProject.Services.Admin;

public sealed class UserListPageViewModel
{
    public IReadOnlyList<User> Users { get; init; } = Array.Empty<User>();
    public IReadOnlyList<SelectionOption> Organizations { get; init; } = Array.Empty<SelectionOption>();
    public bool OpenAddUserModal { get; init; }
}

public sealed class UserSaveRequest
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;
}

public sealed class UserUpdateRequest
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    [JsonPropertyName("fullName")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("dateBegin")]
    public string? DateBegin { get; init; }

    [JsonPropertyName("dateEnd")]
    public string? DateEnd { get; init; }
}
