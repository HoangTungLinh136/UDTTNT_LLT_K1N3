using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class DismissalRecord : IApprovableEntity
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        // Snapshot of employee's current state when creating dismissal record
        public string? EmployeeCodeSnapshot { get; set; }
        public string? EmployeeNameSnapshot { get; set; }
        public string? CurrentDepartmentSnapshot { get; set; }
        public string? CurrentPositionSnapshot { get; set; }
        public string? CurrentJobTitleSnapshot { get; set; }

        public DateTime EffectiveDate { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }

        // 1 = RETURN_TO_PREVIOUS_POSITION, 2 = TRANSFER_TO_OTHER_POSITION, 3 = PENDING_ASSIGNMENT
        public string? PostReliefOption { get; set; } 

        public string? TargetDepartment { get; set; }
        public string? TargetPosition { get; set; } 

        public string? DecisionNumber { get; set; }
        public DateTime? DecisionDate { get; set; }
        public string? DecisionFilePath { get; set; }
        public string? Signer { get; set; }

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

        public ICollection<DismissalHistory> Histories { get; set; } = new List<DismissalHistory>();

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
        
        [NotMapped]
        public string PostReliefOptionLabel => PostReliefOption switch
        {
            "RETURN_TO_PREVIOUS_POSITION" => "Về vị trí cũ",
            "TRANSFER_TO_OTHER_POSITION" => "Chuyển vị trí khác",
            "PENDING_ASSIGNMENT" => "Chờ bố trí",
            _ => "---"
        };
    }

    public class DismissalHistory
    {
        public int Id { get; set; }
        public int DismissalRecordId { get; set; }
        public DismissalRecord? DismissalRecord { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? Actor { get; set; }
        public string? Action { get; set; }
        public string? Details { get; set; }
    }
}
