using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class SuccessionPlan
    {
        public int Id { get; set; }

        public int PlanningPeriodId { get; set; }
        [ForeignKey("PlanningPeriodId")]
        public PlanningPeriod? PlanningPeriod { get; set; }

        public int TargetPositionId { get; set; }
        [ForeignKey("TargetPositionId")]
        public JobPosition? TargetPosition { get; set; }

        public string? Department { get; set; }
        public string? Priority { get; set; } // CRITICAL, HIGH, MEDIUM, LOW
        public string? Reason { get; set; }
        public string? OwnerName { get; set; }
        public string? Notes { get; set; }
        public string? Status { get; set; } // DRAFT, PENDING_APPROVAL, APPROVED
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<SuccessionCandidate> Candidates { get; set; } = new List<SuccessionCandidate>();
        public ICollection<SuccessionHistory> Histories { get; set; } = new List<SuccessionHistory>();
    }
}
