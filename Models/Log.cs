using System;
using Newtonsoft.Json.Linq;

namespace main_project.Models
{
public class Log
{
    public int id_log { get; set; }
    public int id_user { get; set; }
    public int? id_target { get; set; }
    public string? target_type { get; set; }
    public string? event_type { get; set; }
    public DateTime date { get; set; }
    public object? extra_data { get; set; } // Используем object, так как extra_data может быть строкой или JObject
    public string? description { get; set; }

    public string? name_user { get; set; }
    public string? name_survey { get; set; }
}
}