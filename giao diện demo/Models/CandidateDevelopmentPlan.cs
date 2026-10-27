using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class CandidateDevelopmentPlan
    {
        public int Id { get; set; }

        public int SuccessionCandidateId { get; set; }
        [ForeignKey("SuccessionCandidateId")]
        public SuccessionCandidate? SuccessionCandidate { get; set; }

        public string? Objective { get; set; }
        public string? DevelopmentGap { get; set; }
        public string? Activities { get; set; }
        public string? ResponsiblePerson { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? TargetDate { get; set; }
        public string? Status { get; set; } // NOT_STARTED, IN_PROGRESS, COMPLETED
        public int Progress { get; set; } = 0; // 0-100
        public string? Notes { get; set; }
    }
}
