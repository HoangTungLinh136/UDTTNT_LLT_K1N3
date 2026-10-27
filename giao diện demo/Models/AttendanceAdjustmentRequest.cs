using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class AttendanceAdjustmentRequest : IApprovableEntity
    {
        public int Id { get; set; }
        
        public int AttendanceId { get; set; }
        [ForeignKey("AttendanceId")]
        public Attendance? Attendance { get; set; }
        
        public string EmployeeCode { get; set; } = string.Empty;
        
        public TimeSpan? RequestedCheckIn { get; set; }
        public TimeSpan? RequestedCheckOut { get; set; }
        
        public string Reason { get; set; } = string.Empty;
        public string? Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }

        public int GetId() => Id;
    }
}
