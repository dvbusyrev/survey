using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace main_project.Models
{
    public class OMSU
    {
        public int id_omsu { get; set; }
        public required string name_omsu { get; set; }
public DateTime? date_begin { get; set; }
public DateTime? date_end { get; set; }
        public required string list_surveys { get; set; }
        public bool block { get; set; }
        public required string email { get; set; }
    }
}