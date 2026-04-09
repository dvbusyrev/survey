using MainProject.Services.Surveys;
using System.Text.Json.Serialization;

namespace MainProject.Models
{
    public class Survey
    {
        [JsonPropertyName("id_survey")]
        public int IdSurvey { get; set; }

        [JsonPropertyName("name_survey")]
        public string NameSurvey { get; set; } = string.Empty;

        public string? Description { get; set; }
        public List<SurveyQuestionItem> Questions { get; set; } = new();

        [JsonPropertyName("date_create")]
        public DateTime DateCreate { get; set; }

        [JsonPropertyName("date_open")]
        public DateTime DateOpen { get; set; }

        [JsonPropertyName("date_close")]
        public DateTime DateClose { get; set; }

        [JsonPropertyName("organization_name")]
        public string? OrganizationName { get; set; }

        [JsonPropertyName("organization_id")]
        public int OrganizationId { get; set; }

        public string? Csp { get; set; }

        [JsonPropertyName("completion_date")]
        public DateTime CompletionDate { get; set; }

        [JsonPropertyName("date_begin")]
        public DateTime DateBegin { get; set; }

        [JsonPropertyName("date_end")]
        public DateTime DateEnd { get; set; }

        [JsonPropertyName("id_answer")]
        public int IdAnswer { get; set; }

        public List<AnswerRecord> Answers { get; set; } = new();
    }
}
