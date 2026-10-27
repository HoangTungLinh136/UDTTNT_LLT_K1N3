using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    [Table("BenefitEnrollments")]
    public class BenefitEnrollment : IApprovableEntity
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        // DB thật: ProgramId
        [Column("ProgramId")]
        public int BenefitProgramId { get; set; }

        [ForeignKey(nameof(BenefitProgramId))]
        public BenefitProgram? Program { get; set; }

        // DB thật: SubmittedAt
        [Column("SubmittedAt")]
        public DateTime RegistrationDate { get; set; } = DateTime.Now;

        // DB thật: Status
        [Column("Status")]
        public string ApprovalStatus { get; set; } = "DRAFT";

        // Compatibility alias cho code cũ.
        [NotMapped]
        public string? Status
        {
            get => ApprovalStatus;
            set => ApprovalStatus = value ?? "DRAFT";
        }

        // DB thật: Note
        [Column("Note")]
        public string? Notes { get; set; }

        // DB hiện tại không có EmployeeNameSnapshot.
        // Chỉ dùng trong memory; khi entity Include(Employee) thì lấy tên thật.
        private string _employeeNameSnapshot = string.Empty;

        [NotMapped]
        public string EmployeeNameSnapshot
        {
            get => Employee?.Name ?? _employeeNameSnapshot;
            set => _employeeNameSnapshot = value ?? string.Empty;
        }

        // DB thật có ApprovedAt.
        [Column("ApprovedAt")]
        public DateTime? ReviewedAt { get; set; }

        [NotMapped]
        public string? ReviewedBy { get; set; }

        // DB thật: StatusReason
        [Column("StatusReason")]
        public string? RejectionReason { get; set; }

        public int GetId() => Id;
    }
}
