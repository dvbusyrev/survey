using MainProject.Services.Answers;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MainProject.Models
{
    public class AnswerRecord
    {
        [JsonProperty("id_answer")]
        [JsonPropertyName("id_answer")]
        public int IdAnswer { get; set; }

        [JsonProperty("organization_id")]
        [JsonPropertyName("organization_id")]
        public int OrganizationId { get; set; }

        [JsonProperty("id_survey")]
        [JsonPropertyName("id_survey")]
        public int IdSurvey { get; set; }

        [JsonProperty("organization_name")]
        [JsonPropertyName("organization_name")]
        public string? OrganizationName { get; set; }

        public string Csp { get; set; } = string.Empty;

        [JsonProperty("name_survey")]
        [JsonPropertyName("name_survey")]
        public string? NameSurvey { get; set; }

        [JsonProperty("completion_date")]
        [JsonPropertyName("completion_date")]
        public DateTime? CompletionDate { get; set; }

        [JsonProperty("create_date_survey")]
        [JsonPropertyName("create_date_survey")]
        public DateTime? CreateDateSurvey { get; set; }

        public List<AnswerPayloadItem> Answers { get; set; } = new();
    }
}
