using System;
using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    public class WorkShift
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string ShiftCode { get; set; } = "";

        [Required]
        [StringLength(200)]
        public string ShiftName { get; set; } = "";

        [StringLength(100)]
        public string? ShiftType { get; set; } // e.g., Hành chính, Làm ca

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public int BreakDurationMinutes { get; set; } = 0;
        public int LateGraceMinutes { get; set; } = 0;
        public int EarlyLeaveGraceMinutes { get; set; } = 0;

        public bool IsOvernight { get; set; }
        public bool IsOvertimeEnabled { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Active"; // Active, Inactive

        public string? Notes { get; set; }

        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
