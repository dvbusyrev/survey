using main_project.Services.Surveys;

namespace main_project.Models
{
    public class Survey
    {
        public int id_survey { get; set; }
        public string name_survey { get; set; }
        public string? description { get; set; }
        public List<SurveyQuestionItem> Questions { get; set; } = new();
        public DateTime date_create { get; set; }
        public DateTime date_open { get; set; }
        public DateTime date_close { get; set; }
        public string? name_omsu { get; set; }
        public int id_omsu { get; set; }
        public string? csp { get; set; }
        public DateTime completion_date { get; set; }
        public DateTime date_begin { get; set; }
        public DateTime date_end { get; set; }
        public int id_answer { get; set; }
        public List<HistoryAnswer> Answers { get; set; } = new List<HistoryAnswer>();
    }
}
