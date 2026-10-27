using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class BenefitEntitlement
    {
        public int Id { get; set; }
        
        public int EmployeeId { get; set; }
        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        public string EmployeeNameSnapshot { get; set; } = string.Empty;

        public int BenefitProgramId { get; set; }

        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }

        public string Status { get; set; } = "Active"; // Active, Expired, Cancelled

        public decimal? Amount { get; set; }
        
        public string? Notes { get; set; }

        [ForeignKey("BenefitProgramId")]
        public BenefitProgram? Program { get; set; }
    }
}
