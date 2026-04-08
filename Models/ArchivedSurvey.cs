using MainProject.Services.Surveys;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MainProject.Models
{
    public class ArchivedSurvey
    {
        [JsonProperty("archived_survey_id")]
        [JsonPropertyName("archived_survey_id")]
        public int ArchivedSurveyId { get; set; }

        [JsonProperty("id_survey")]
        [JsonPropertyName("id_survey")]
        public int IdSurvey { get; set; }

        [JsonProperty("date_begin")]
        [JsonPropertyName("date_begin")]
        public DateTime DateBegin { get; set; }

        [JsonProperty("date_end")]
        [JsonPropertyName("date_end")]
        public DateTime DateEnd { get; set; }

        public string? Csp { get; set; }

        [JsonProperty("id_user")]
        [JsonPropertyName("id_user")]
        public int IdUser { get; set; }

        [JsonProperty("name_survey")]
        [JsonPropertyName("name_survey")]
        public string NameSurvey { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public List<SurveyQuestionItem> Questions { get; set; } = new();
    }
}
