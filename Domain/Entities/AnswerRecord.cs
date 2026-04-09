using MainProject.Application.UseCases.Answers;
using System.Text.Json.Serialization;

namespace MainProject.Domain.Entities
{
    public class AnswerRecord
    {
        [JsonPropertyName("id_answer")]
        public int IdAnswer { get; set; }

        [JsonPropertyName("organization_id")]
        public int OrganizationId { get; set; }

        [JsonPropertyName("id_survey")]
        public int IdSurvey { get; set; }

        [JsonPropertyName("organization_name")]
        public string? OrganizationName { get; set; }

        public string Csp { get; set; } = string.Empty;

        [JsonPropertyName("name_survey")]
        public string? NameSurvey { get; set; }

        [JsonPropertyName("completion_date")]
        public DateTime? CompletionDate { get; set; }

        [JsonPropertyName("create_date_survey")]
        public DateTime? CreateDateSurvey { get; set; }

        public List<AnswerPayloadItem> Answers { get; set; } = new();
    }
}
