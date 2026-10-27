using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class AppointmentRecord : IApprovableEntity
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        // Snapshot of employee's current state when creating appointment
        public string? EmployeeCodeSnapshot { get; set; }
        public string? EmployeeNameSnapshot { get; set; }
        public string? PreviousDepartment { get; set; }
        public string? PreviousPosition { get; set; }
        public string? PreviousJobTitle { get; set; }

        // New state after appointment
        public string? NewDepartment { get; set; }
        public string? NewPosition { get; set; } // Can map to JobPosition name
        public string? NewJobTitle { get; set; } 

        public DateTime EffectiveDate { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }

        public string? DecisionNumber { get; set; }
        public DateTime? DecisionDate { get; set; }
        public string? DecisionFilePath { get; set; }

        // Workflow Status: DRAFT, PENDING_APPROVAL, APPROVED, REJECTED, CANCELLED
        public string ApprovalStatus { get; set; } = "DRAFT";
        [NotMapped]
        public string? Status
        {
            get => ApprovalStatus == "PENDING_HR_REVIEW" || ApprovalStatus == "PENDING_LEADER_APPROVAL"
                ? "PENDING_APPROVAL"
                : ApprovalStatus;
            set => ApprovalStatus = value ?? "DRAFT";
        }
        public int GetId() => Id;

        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        [NotMapped] public string? ReviewedBy { get => ApprovedBy; set => ApprovedBy = value; }
        [NotMapped] public DateTime? ReviewedAt { get => ApprovedAt; set => ApprovedAt = value; }

        public string? RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectionReason { get; set; }

        public string? CancelledBy { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }

        public string? AppliedBy { get; set; }
        public DateTime? AppliedAt { get; set; }

        public ICollection<AppointmentHistory> Histories { get; set; } = new List<AppointmentHistory>();

        // Computed properties
        [NotMapped]
        public string EffectiveStatus 
        {
            get
            {
                if (ApprovalStatus == "EFFECTIVE" || AppliedAt != null) return "EFFECTIVE";
                if (ApprovalStatus != "APPROVED") return "---";
                if (EffectiveDate > DateTime.Today) return "UPCOMING";
                return "READY_TO_APPLY";
            }
        }

        [NotMapped]
        public string ApprovalStatusLabel => ApprovalStatus switch
        {
            "DRAFT" => "Bản nháp",
            "PENDING_HR_REVIEW" => "Chờ HR duyệt",
            "PENDING_LEADER_APPROVAL" => "Chờ Lãnh đạo duyệt",
            "PENDING_APPROVAL" => "Chờ duyệt",
            "APPROVED" => "Đã duyệt",
            "EFFECTIVE" => "Có hiệu lực",
            "REJECTED" => "Từ chối",
            "CANCELLED" => "Đã hủy",
            _ => ApprovalStatus
        };

        [NotMapped]
        public string EffectiveStatusLabel => EffectiveStatus switch
        {
            "UPCOMING" => "Sắp hiệu lực",
            "READY_TO_APPLY" => "Đang chờ áp dụng",
            "EFFECTIVE" => "Đã hiệu lực",
            _ => "---"
        };
    }

    public class AppointmentHistory
    {
        public int Id { get; set; }
        public int AppointmentRecordId { get; set; }
        public AppointmentRecord? AppointmentRecord { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? Actor { get; set; }
        public string? Action { get; set; } // CREATE, SUBMIT, APPROVE, REJECT, CANCEL, APPLY
        public string? Details { get; set; }
    }
}
