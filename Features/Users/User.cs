using MainProject.Infrastructure.Serialization;
using System.Text.Json.Serialization;

namespace MainProject.Domain.Entities
{
    public class User
    {
        [JsonPropertyName("id_user")]
        public int IdUser { get; set; }

        [JsonPropertyName("login")]
        public string? NameUser { get; set; }

        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }

        [JsonPropertyName("organization_name")]
        public string? OrganizationName { get; set; }

        [JsonPropertyName("password")]
        public required string HashPassword { get; set; }

        public string? Email { get; set; }

        [JsonPropertyName("role")]
        public required string NameRole { get; set; }

        [JsonPropertyName("id_organization")]
        public int OrganizationId { get; set; }

        [JsonPropertyName("key_csp")]
        public string? KeyCsp { get; set; }

        [JsonPropertyName("date_begin")]
        [JsonConverter(typeof(NullableDateOnlyDateTimeJsonConverter))]
        public DateTime? DateBegin { get; set; }

        [JsonPropertyName("date_end")]
        [JsonConverter(typeof(NullableDateOnlyDateTimeJsonConverter))]
        public DateTime? DateEnd { get; set; }
    }
}
