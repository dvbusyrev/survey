using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MainProject.Models
{
    public class User
    {
        [JsonProperty("id_user")]
        [JsonPropertyName("id_user")]
        public int IdUser { get; set; }

        [JsonProperty("name_user")]
        [JsonPropertyName("name_user")]
        public string? NameUser { get; set; }

        [JsonProperty("full_name")]
        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }

        [JsonProperty("organization_name")]
        [JsonPropertyName("organization_name")]
        public string? OrganizationName { get; set; }

        [JsonProperty("hash_password")]
        [JsonPropertyName("hash_password")]
        public required string HashPassword { get; set; }

        public string? Email { get; set; }

        [JsonProperty("name_role")]
        [JsonPropertyName("name_role")]
        public required string NameRole { get; set; }

        [JsonProperty("organization_id")]
        [JsonPropertyName("organization_id")]
        public int OrganizationId { get; set; }

        [JsonProperty("key_csp")]
        [JsonPropertyName("key_csp")]
        public string? KeyCsp { get; set; }

        [JsonProperty("date_begin")]
        [JsonPropertyName("date_begin")]
        public DateTime? DateBegin { get; set; }

        [JsonProperty("date_end")]
        [JsonPropertyName("date_end")]
        public DateTime? DateEnd { get; set; }
    }
}
