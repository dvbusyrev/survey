using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace MainProject.Domain.Entities
{
    public class Organization
    {
        [JsonPropertyName("organization_id")]
        public int OrganizationId { get; set; }

        [JsonPropertyName("organization_name")]
        public required string OrganizationName { get; set; }

        [JsonPropertyName("date_begin")]
        public DateTime? DateBegin { get; set; }

        [JsonPropertyName("date_end")]
        public DateTime? DateEnd { get; set; }

        [JsonPropertyName("survey_names")]
        public string? SurveyNames { get; set; }

        public bool Block { get; set; }
        public string? Email { get; set; }
    }
}
