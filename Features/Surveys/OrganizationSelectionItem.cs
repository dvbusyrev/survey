using System.Text.Json.Serialization;
using MainProject.Infrastructure.Serialization;

namespace MainProject.Application.DTO;

public sealed class OrganizationSelectionItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(NullableDateOnlyDateTimeJsonConverter))]
    public DateTime? DateEnd { get; set; }

    [JsonConverter(typeof(NullableDateOnlyDateTimeJsonConverter))]
    public DateTime? SurveyDateEnd { get; set; }
}
