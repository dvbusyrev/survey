using MainProject.Services.Surveys;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MainProject.Models
{
    public class Survey
    {
        [JsonProperty("id_survey")]
        [JsonPropertyName("id_survey")]
        public int IdSurvey { get; set; }

        [JsonProperty("name_survey")]
        [JsonPropertyName("name_survey")]
        public string NameSurvey { get; set; } = string.Empty;

        public string? Description { get; set; }
        public List<SurveyQuestionItem> Questions { get; set; } = new();

        [JsonProperty("date_create")]
        [JsonPropertyName("date_create")]
        public DateTime DateCreate { get; set; }

        [JsonProperty("date_open")]
        [JsonPropertyName("date_open")]
        public DateTime DateOpen { get; set; }

        [JsonProperty("date_close")]
        [JsonPropertyName("date_close")]
        public DateTime DateClose { get; set; }

        [JsonProperty("organization_name")]
        [JsonPropertyName("organization_name")]
        public string? OrganizationName { get; set; }

        [JsonProperty("organization_id")]
        [JsonPropertyName("organization_id")]
        public int OrganizationId { get; set; }

        public string? Csp { get; set; }

        [JsonProperty("completion_date")]
        [JsonPropertyName("completion_date")]
        public DateTime CompletionDate { get; set; }

        [JsonProperty("date_begin")]
        [JsonPropertyName("date_begin")]
        public DateTime DateBegin { get; set; }

        [JsonProperty("date_end")]
        [JsonPropertyName("date_end")]
        public DateTime DateEnd { get; set; }

        [JsonProperty("id_answer")]
        [JsonPropertyName("id_answer")]
        public int IdAnswer { get; set; }

        public List<AnswerRecord> Answers { get; set; } = new();
    }
}
