using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class SuccessionCandidate
    {
        public int Id { get; set; }

        public int SuccessionPlanId { get; set; }
        [ForeignKey("SuccessionPlanId")]
        public SuccessionPlan? SuccessionPlan { get; set; }

        public int EmployeeId { get; set; }
        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }
        
        // Snapshots for history
        public string? EmployeeCodeSnapshot { get; set; }
        public string? EmployeeNameSnapshot { get; set; }
        public string? DepartmentSnapshot { get; set; }
        public string? PositionSnapshot { get; set; }
        
        // Assessment scores
        public int? CompetencyScore { get; set; }
        public int? ExperienceScore { get; set; }
        public int? PerformanceScore { get; set; }
        public int? PotentialScore { get; set; }
        
        public string? ReadinessLevel { get; set; } // READY_NOW, READY_1_2_YEARS, READY_2_3_YEARS, DEVELOPMENT_REQUIRED
        public int? Rank { get; set; }
        public string? AssessmentNotes { get; set; }
        
        public DateTime AddedAt { get; set; } = DateTime.Now;

        // Navigation
        public CandidateDevelopmentPlan? DevelopmentPlan { get; set; }
    }
}
