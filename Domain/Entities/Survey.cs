using MainProject.Application.DTO;
using MainProject.Infrastructure.Serialization;
using System.Text.Json.Serialization;

namespace MainProject.Domain.Entities
{
    public class Survey
    {
        [JsonPropertyName("id_survey")]
        public int IdSurvey { get; set; }

        [JsonPropertyName("name_survey")]
        public string NameSurvey { get; set; } = string.Empty;

        public string? Description { get; set; }
        public List<SurveyQuestionItem> Questions { get; set; } = new();

        [JsonPropertyName("organization_name")]
        public string? OrganizationName { get; set; }

        [JsonPropertyName("id_organization")]
        public int OrganizationId { get; set; }

        public string? Csp { get; set; }

        [JsonPropertyName("completion_date")]
        public DateTime CompletionDate { get; set; }

        [JsonPropertyName("date_begin")]
        [JsonConverter(typeof(DateOnlyDateTimeJsonConverter))]
        public DateTime DateBegin { get; set; }

        [JsonPropertyName("date_end")]
        [JsonConverter(typeof(DateOnlyDateTimeJsonConverter))]
        public DateTime DateEnd { get; set; }

        [JsonPropertyName("id_answer")]
        public int IdAnswer { get; set; }

        public List<AnswerRecord> Answers { get; set; } = new();
    }
}
