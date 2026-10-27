using System;

namespace giao_dien_demo.Models
{
    public class Contract : IApprovableEntity
    {
        public int Id { get; set; }
        public string? EmployeeNameSnapshot { get; set; }

        // Historical values captured when the contract is created.
        public string? DepartmentSnapshot { get; set; }
        public string? PositionSnapshot { get; set; }
        public string? JobTitleSnapshot { get; set; }
        public decimal? ContractSalarySnapshot { get; set; }

        public int? EmployeeId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? EmployeeName { get => EmployeeNameSnapshot; set => EmployeeNameSnapshot = value; }
        public string? Duration { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Chờ duyệt";

        // Nội dung và chữ ký
        public string? Content { get; set; }
        public string? Signature { get; set; }
        public string? HrSignature { get; set; }
        public DateTime? ExtensionDate { get; set; }
        public DateTime? EmployeeSignedAt { get; set; }

        // Đường dẫn tới file PDF chính thức (được tạo khi hợp đồng hoàn tất)
        public string? PdfFilePath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }

        public int GetId() => Id;
    }
}