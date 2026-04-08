using main_project.Services.Answers;

namespace main_project.Models
{
public class HistoryAnswer
{
     public int id_answer { get; set; }         // Идентификатор Organization (организации)
    public int organization_id { get; set; }         // Идентификатор Organization (организации)
    public int id_survey { get; set; }       // Идентификатор опроса
    public string? organization_name { get; set; }   // Название Organization
     public string csp { get; set; }         // Дополнительная информация (может быть пустой)
    public string? name_survey { get; set; }  // Название опроса
    public DateTime? completion_date { get; set; }  // Дата завершения опроса
    public DateTime? create_date_survey { get; set; }
    public List<AnswerPayloadItem> Answers { get; set; } = new();
}
}
