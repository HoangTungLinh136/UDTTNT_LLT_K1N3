using System;

namespace giao_dien_demo.Models
{
    public class PayrollSettingVersion
    {
        public string VersionId { get; set; } = Guid.NewGuid().ToString();
        public string VersionName { get; set; }
        
        public string Status { get; set; } // DRAFT, ACTIVE, SUPERSEDED
        
        public DateTime? EffectiveFrom { get; set; }
        
        // Tab 1: Kỳ lương
        public string CycleType { get; set; } // MONTHLY, WEEKLY
        public string PeriodStartRule { get; set; } // e.g. "26"
        public string PeriodEndRule { get; set; } // e.g. "25"
        
        // Tab 2: Ngày trả lương
        public string PayDateRule { get; set; } // e.g. "5" (Ngày mùng 5 hàng tháng)
        public string HolidayHandlingRule { get; set; } // PREVIOUS_WORKING_DAY, NEXT_WORKING_DAY, KEEP_DATE
        
        // Tab 3: Làm tròn
        public string SalaryRoundingMode { get; set; } // ROUND, FLOOR, CEIL, NONE
        public int SalaryRoundingStep { get; set; } // e.g. 1000
        
        // Tab 4 & 5: Thuế & Bảo hiểm (Reference)
        public string TaxConfigVersionId { get; set; }
        public string InsuranceConfigVersionId { get; set; }
        
        // Tab 6: Khóa kỳ
        public int AutoLockDays { get; set; } // Số ngày tự động khóa sau khi kết thúc kỳ. 0 = Không tự động khóa
        
        // Audit
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        
        public string ActivatedBy { get; set; }
        public DateTime? ActivatedAt { get; set; }
    }
}
