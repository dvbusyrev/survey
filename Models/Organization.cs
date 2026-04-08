using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MainProject.Models
{
    public class Organization
    {
        [JsonProperty("organization_id")]
        [JsonPropertyName("organization_id")]
        public int OrganizationId { get; set; }

        [JsonProperty("organization_name")]
        [JsonPropertyName("organization_name")]
        public required string OrganizationName { get; set; }

        [JsonProperty("date_begin")]
        [JsonPropertyName("date_begin")]
        public DateTime? DateBegin { get; set; }

        [JsonProperty("date_end")]
        [JsonPropertyName("date_end")]
        public DateTime? DateEnd { get; set; }

        [JsonProperty("survey_names")]
        [JsonPropertyName("survey_names")]
        public string? SurveyNames { get; set; }

        public bool Block { get; set; }
        public string? Email { get; set; }
    }
}
