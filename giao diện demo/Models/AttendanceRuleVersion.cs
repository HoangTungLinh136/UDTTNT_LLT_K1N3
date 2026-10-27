using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class AttendanceRuleVersion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string VersionCode { get; set; } = "V1";

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, Active, Archived

        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }

        [Required]
        [StringLength(50)]
        public string ScopeType { get; set; } = "Company"; // Company, Department, Employee
        
        public int? ScopeId { get; set; } // Null if Company

        // --- SECTION: LATE ---
        public int LateGraceMinutes { get; set; } = 0;
        public int LateFullCreditThresholdMinutes { get; set; } = 0;
        [StringLength(50)]
        public string LateHandlingMode { get; set; } = "DeductMinutes"; 
        public int LateMaxDeductionMinutes { get; set; } = 0;

        // --- SECTION: EARLY LEAVE ---
        public int EarlyLeaveGraceMinutes { get; set; } = 0;
        public int EarlyLeaveFullCreditThresholdMinutes { get; set; } = 0;
        [StringLength(50)]
        public string EarlyLeaveHandlingMode { get; set; } = "DeductMinutes";
        public int EarlyLeaveMaxDeductionMinutes { get; set; } = 0;

        // --- SECTION: ROUNDING ---
        [StringLength(50)]
        public string CheckInRoundingMode { get; set; } = "None"; // None, Nearest, Up, Down
        public int CheckInRoundingStepMinutes { get; set; } = 0;
        [StringLength(50)]
        public string CheckOutRoundingMode { get; set; } = "None";
        public int CheckOutRoundingStepMinutes { get; set; } = 0;

        // --- SECTION: MISSING PUNCH ---
        [StringLength(50)]
        public string MissingCheckInMode { get; set; } = "RequireAdjustment"; // RequireAdjustment, AssumeShiftStart, DeductFullDay
        public TimeSpan? MissingCheckInAssumedTime { get; set; }
        [StringLength(50)]
        public string MissingCheckOutMode { get; set; } = "RequireAdjustment";
        public TimeSpan? MissingCheckOutAssumedTime { get; set; }

        // --- SECTION: OVERTIME ---
        public double DailyOtThreshold { get; set; } = 8.0;
        public double WeeklyOtThreshold { get; set; } = 40.0;
        public bool AutoCalculateOt { get; set; } = false;
        public bool AllowAfterShiftOt { get; set; } = false;

        // --- AUDIT ---
        [StringLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
