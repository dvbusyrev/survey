using System.Text.Json.Serialization;
using MainProject.Models;

namespace MainProject.Services.Admin;

public sealed class OrganizationListPageViewModel
{
    public IReadOnlyList<Organization> Organizations { get; init; } = Array.Empty<Organization>();
    public bool OpenAddOrganizationModal { get; init; }
}

public sealed class OrganizationSaveRequest
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("Email")]
    public string? Email { get; init; }

    [JsonPropertyName("DateBegin")]
    public string? DateBegin { get; init; }

    [JsonPropertyName("DateEnd")]
    public string? DateEnd { get; init; }
}

public sealed class OrganizationDataResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
