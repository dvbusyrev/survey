using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MainProject.Models
{
using Newtonsoft.Json;
using System.Text.Json.Serialization;

public class OrganizationProd
{
    [JsonProperty("organization_name")]
    [JsonPropertyName("organization_name")]
    public string OrganizationName { get; set; } = string.Empty; // Идентификатор организации (строка)

    [JsonProperty("date_end")]
    [JsonPropertyName("date_end")]
    public string DateEnd { get; set; } = string.Empty; // Дата окончания (строка)
}
}
