using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class KPIEvaluation
    {
        [Key]
        public int Id { get; set; }

        public int PeriodId { get; set; }
        [ForeignKey("PeriodId")]
        public KPIPeriod? Period { get; set; }

        public int EmployeeId { get; set; }
        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        // Stable identity for Manager
        public int? ManagerUserId { get; set; }
        [ForeignKey("ManagerUserId")]
        public User? ManagerUser { get; set; }

        [StringLength(100)]
        public string? ManagerUsernameSnapshot { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "DRAFT"; // DRAFT, ACTIVE, EVALUATING, COMPLETED, CANCELLED

        [Column(TypeName = "decimal(5,2)")]
        public decimal? FinalScore { get; set; }

        [StringLength(20)]
        public string? FinalRating { get; set; }

        [StringLength(100)]
        public string? EmployeeDepartmentSnapshot { get; set; }

        [StringLength(100)]
        public string? EmployeePositionSnapshot { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? CompletedAt { get; set; }

        [StringLength(100)]
        public string? CompletedBy { get; set; }

        // Navigation
        public ICollection<KPITarget> Targets { get; set; } = new List<KPITarget>();
    }
}
