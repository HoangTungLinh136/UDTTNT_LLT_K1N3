using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class IncidentParticipant
    {
        [Key]
        public int Id { get; set; }

        public int IncidentRecordId { get; set; }
        [ForeignKey("IncidentRecordId")]
        public IncidentRecord? IncidentRecord { get; set; }

        public int EmployeeId { get; set; }
        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        public string? EmployeeCodeSnapshot { get; set; }
        public string? EmployeeNameSnapshot { get; set; }
        public string? DepartmentSnapshot { get; set; }
        public string? PositionSnapshot { get; set; }

        // Vai trò của người tham gia trong sự cố: RELATED_PERSON, REPORTER, OTHER
        public string? ParticipantRole { get; set; }
    }
}
