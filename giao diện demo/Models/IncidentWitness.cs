using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class IncidentWitness
    {
        [Key]
        public int Id { get; set; }

        public int IncidentRecordId { get; set; }
        [ForeignKey("IncidentRecordId")]
        public IncidentRecord? IncidentRecord { get; set; }

        [Required]
        public string? WitnessName { get; set; }
        public string? EmployeeCode { get; set; } // Tùy chọn nếu là nhân viên
        public string? Department { get; set; }
        public string? Notes { get; set; }
    }
}
