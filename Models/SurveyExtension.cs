using System;

namespace MainProject.Models
{
    public class SurveyExtension
    {
        public int Id { get; set; }
        public int SurveyId { get; set; }
        public int OrganizationId { get; set; }
        public DateTime ExtendedUntil { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
