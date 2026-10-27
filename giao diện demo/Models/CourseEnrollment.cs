using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    [Table("TrainingEnrollments")]
    public class CourseEnrollment : IApprovableEntity
    {
        [Key]
        public int Id { get; set; }

        public int CourseId { get; set; }
        [ForeignKey(nameof(CourseId))]
        public TrainingCourse? Course { get; set; }

        public int EmployeeId { get; set; }
        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        [Required, StringLength(60)]
        public string Source { get; set; } = "SELF";

        [Column("Status"), Required, StringLength(60)]
        public string ProgressStatus { get; set; } = "DRAFT";

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProgressPercent { get; set; }

        [StringLength(60)]
        public string? Result { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Score { get; set; }

        [Column("EnrolledAt")]
        public DateTime RegistrationDate { get; set; } = DateTime.Now;

        public DateTime? StartedAt { get; set; }

        [Column("CompletedAt")]
        public DateTime? CompletionDate { get; set; }

        public DateTime? ApprovedAt { get; set; }

        // Compatibility only. Canonical DB status is ProgressStatus -> Status.
        [NotMapped]
        public string ApprovalStatus { get; set; } = "DRAFT";

        [NotMapped]
        public string? Status
        {
            get => ApprovalStatus;
            set => ApprovalStatus = value ?? "DRAFT";
        }

        [NotMapped]
        public string EmployeeNameSnapshot
        {
            get; set;
        }

        [NotMapped]
        public string? Notes { get; set; }

        [NotMapped]
        public string? ReviewedBy { get; set; }

        [NotMapped]
        public DateTime? ReviewedAt
        {
            get => ApprovedAt;
            set => ApprovedAt = value;
        }

        [NotMapped]
        public string? RejectionReason { get; set; }

        public TrainingCertificate? Certificate { get; set; }

        public int GetId() => Id;
    }
}
