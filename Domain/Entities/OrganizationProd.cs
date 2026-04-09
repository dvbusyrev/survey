using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace MainProject.Domain.Entities
{
    public class OrganizationProd
    {
        [JsonPropertyName("organization_name")]
        public string OrganizationName { get; set; } = string.Empty; // Идентификатор организации (строка)

        [JsonPropertyName("date_end")]
        public string DateEnd { get; set; } = string.Empty; // Дата окончания (строка)
    }
}
