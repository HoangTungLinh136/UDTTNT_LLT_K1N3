using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class IncidentRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string? IncidentCode { get; set; }

        [Required]
        public string? Title { get; set; }

        [Required]
        public string? IncidentType { get; set; }

        [Required]
        public string? Severity { get; set; }

        [Required]
        public DateTime OccurredAt { get; set; }

        public string? Location { get; set; }
        
        [Required]
        public string? Description { get; set; }

        // Người báo cáo (có thể là Username hoặc EmployeeName)
        public string? ReportedBy { get; set; }

        // Người phụ trách xác minh / xử lý
        public int? ResponsiblePersonId { get; set; }
        
        [ForeignKey("ResponsiblePersonId")]
        public Employee? ResponsiblePerson { get; set; }
        public string? ResponsiblePersonNameSnapshot { get; set; }

        // Trạng thái: NEW, INVESTIGATING, PROCESSING, RESOLVED, CLOSED, CANCELLED
        [Required]
        public string Status { get; set; } = "NEW";

        // Xác minh / Kết luận
        public string? InvestigationSummary { get; set; }
        public string? ProcessingNotes { get; set; }
        public string? Conclusion { get; set; }
        public string? Resolution { get; set; } // Biện pháp xử lý

        // Timestamps
        public DateTime? InvestigationStartedAt { get; set; }
        public string? InvestigationStartedBy { get; set; }

        public DateTime? ProcessingStartedAt { get; set; }
        public string? ProcessingStartedBy { get; set; }

        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }

        public DateTime? ClosedAt { get; set; }
        public string? ClosedBy { get; set; }

        public DateTime? CancelledAt { get; set; }
        public string? CancelledBy { get; set; }
        public string? CancellationReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string? UpdatedBy { get; set; }

        // Navigation properties
        public ICollection<IncidentParticipant> Participants { get; set; } = new List<IncidentParticipant>();
        public ICollection<IncidentWitness> Witnesses { get; set; } = new List<IncidentWitness>();
        public ICollection<IncidentEvidence> Evidences { get; set; } = new List<IncidentEvidence>();
        public ICollection<IncidentHistory> Histories { get; set; } = new List<IncidentHistory>();
    }
}
