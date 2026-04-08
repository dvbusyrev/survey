using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace main_project.Models
{
    public class Organization
    {
        public int organization_id { get; set; }
        public required string organization_name { get; set; }
        public DateTime? date_begin { get; set; }
        public DateTime? date_end { get; set; }
        public string? survey_names { get; set; }
        public bool block { get; set; }
        public string? email { get; set; }
    }
}
