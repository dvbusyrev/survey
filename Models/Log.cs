using System;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MainProject.Models
{
    public class Log
    {
        [JsonProperty("id_log")]
        [JsonPropertyName("id_log")]
        public long IdLog { get; set; }

        [JsonProperty("id_user")]
        [JsonPropertyName("id_user")]
        public int? IdUser { get; set; }

        [JsonProperty("target_type")]
        [JsonPropertyName("target_type")]
        public string? TargetType { get; set; }

        [JsonProperty("event_type")]
        [JsonPropertyName("event_type")]
        public string? EventType { get; set; }

        public DateTime Date { get; set; }

        [JsonProperty("extra_data")]
        [JsonPropertyName("extra_data")]
        public object? ExtraData { get; set; }

        public string? Description { get; set; }

        [JsonProperty("name_user")]
        [JsonPropertyName("name_user")]
        public string? NameUser { get; set; }

        [JsonProperty("target_name")]
        [JsonPropertyName("target_name")]
        public string? TargetName { get; set; }
    }
}
