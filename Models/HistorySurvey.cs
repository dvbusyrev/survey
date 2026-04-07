using main_project.Services.Surveys;

namespace main_project.Models
{
    public class HistorySurvey
    {
        public int id_hSurvey { get; set; }
        public int id_survey { get; set; }
        public DateTime date_begin { get; set; }
        public DateTime date_end { get; set; }
        public string? csp { get; set; }
        public int id_user { get; set; }
        public string name_survey { get; set; }
        public string description { get; set; }
        public List<SurveyQuestionItem> Questions { get; set; } = new();
    }
}
