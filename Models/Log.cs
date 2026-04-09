using System;
using System.Text.Json.Serialization;

namespace MainProject.Models
{
    public class Log
    {
        [JsonPropertyName("id_log")]
        public long IdLog { get; set; }

        [JsonPropertyName("id_user")]
        public int? IdUser { get; set; }

        [JsonPropertyName("target_type")]
        public string? TargetType { get; set; }

        [JsonPropertyName("event_type")]
        public string? EventType { get; set; }

        public DateTime Date { get; set; }

        [JsonPropertyName("extra_data")]
        public object? ExtraData { get; set; }

        public string? Description { get; set; }

        [JsonPropertyName("name_user")]
        public string? NameUser { get; set; }

        [JsonPropertyName("target_name")]
        public string? TargetName { get; set; }
    }
}
