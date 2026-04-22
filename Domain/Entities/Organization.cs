using MainProject.Infrastructure.Serialization;
using System.Text.Json.Serialization;

namespace MainProject.Domain.Entities
{
    public class Organization
    {
        [JsonPropertyName("id_organization")]
        public int OrganizationId { get; set; }

        [JsonPropertyName("organization_name")]
        public required string OrganizationName { get; set; }

        [JsonPropertyName("organization_short_name")]
        public string? OrganizationShortName { get; set; }

        [JsonPropertyName("date_begin")]
        [JsonConverter(typeof(NullableDateOnlyDateTimeJsonConverter))]
        public DateTime? DateBegin { get; set; }

        [JsonPropertyName("date_end")]
        [JsonConverter(typeof(NullableDateOnlyDateTimeJsonConverter))]
        public DateTime? DateEnd { get; set; }

        [JsonPropertyName("survey_names")]
        public string? SurveyNames { get; set; }

        public string? Email { get; set; }
    }
}
