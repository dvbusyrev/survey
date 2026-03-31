namespace main_project.Models
{
    public class User
    {
        public int id_user { get; set; }
        public string?  name_user { get; set; }
        public string? full_name { get; set; }
            public string? name_omsu { get; set; }
        public required string hash_password { get; set; }
        public string? email { get; set; }
        public required string name_role { get; set; }
        public int id_omsu { get; set; }
        public string? key_csp { get; set; }
    public DateTime? date_begin { get; set; }
    public DateTime? date_end { get; set; } 
    }
}
