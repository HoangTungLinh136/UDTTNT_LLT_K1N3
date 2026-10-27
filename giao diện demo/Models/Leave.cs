using System;

namespace giao_dien_demo.Models
{
    public class Leave : IApprovableEntity
    {
        public int Id { get; set; }
        public string? EmployeeNameSnapshot { get; set; }

        public int? EmployeeId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? EmployeeName { get => EmployeeNameSnapshot; set => EmployeeNameSnapshot = value; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Reason { get; set; }

        public string? Status { get; set; } = "Chờ duyệt";
        public string? LeaveType { get; set; } = "Nghỉ phép năm"; // Loại phép
        public string? EmployeeSignature { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }

        public int GetId() => Id;
    }
}