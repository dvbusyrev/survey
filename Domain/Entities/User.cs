using System.Text.Json.Serialization;

namespace MainProject.Domain.Entities
{
    public class User
    {
        [JsonPropertyName("id_user")]
        public int IdUser { get; set; }

        [JsonPropertyName("name_user")]
        public string? NameUser { get; set; }

        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }

        [JsonPropertyName("organization_name")]
        public string? OrganizationName { get; set; }

        [JsonPropertyName("hash_password")]
        public required string HashPassword { get; set; }

        public string? Email { get; set; }

        [JsonPropertyName("name_role")]
        public required string NameRole { get; set; }

        [JsonPropertyName("organization_id")]
        public int OrganizationId { get; set; }

        [JsonPropertyName("key_csp")]
        public string? KeyCsp { get; set; }

        [JsonPropertyName("date_begin")]
        public DateTime? DateBegin { get; set; }

        [JsonPropertyName("date_end")]
        public DateTime? DateEnd { get; set; }
    }
}
