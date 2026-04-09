using MainProject.Services.Surveys;
using System.Text.Json.Serialization;

namespace MainProject.Models
{
    public class ArchivedSurvey
    {
        [JsonPropertyName("archived_survey_id")]
        public int ArchivedSurveyId { get; set; }

        [JsonPropertyName("id_survey")]
        public int IdSurvey { get; set; }

        [JsonPropertyName("date_begin")]
        public DateTime DateBegin { get; set; }

        [JsonPropertyName("date_end")]
        public DateTime DateEnd { get; set; }

        public string? Csp { get; set; }

        [JsonPropertyName("id_user")]
        public int IdUser { get; set; }

        [JsonPropertyName("name_survey")]
        public string NameSurvey { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public List<SurveyQuestionItem> Questions { get; set; } = new();
    }
}
