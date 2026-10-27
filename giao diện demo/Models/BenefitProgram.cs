using System;

namespace giao_dien_demo.Models
{
    public class BenefitProgram
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        
        // Loai: Insurance, Allowance, Special, Health...
        public string BenefitType { get; set; } = "Other"; 

        // Trang thai: Open, Closed, Upcoming, Draft
        public string Status { get; set; } = "Draft";

        public DateTime? RegistrationStart { get; set; }
        public DateTime? RegistrationEnd { get; set; }

        public bool RequiresApproval { get; set; } = true;
        public bool IsAutoApplied { get; set; } = false;

        public int? Capacity { get; set; } // So luong gioi han (neu co)
        
        public string? RequiredContractType { get; set; } // (VD: Chinh thuc)
    }
}
