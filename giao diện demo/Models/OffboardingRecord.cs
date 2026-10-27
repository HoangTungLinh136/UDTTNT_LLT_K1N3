using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class OffboardingRecord : IApprovableEntity
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        // Snapshot of employee's current state when creating offboarding record
        public string? EmployeeCodeSnapshot { get; set; }
        public string? EmployeeNameSnapshot { get; set; }
        public string? DepartmentSnapshot { get; set; }
        public string? PositionSnapshot { get; set; }

        public string? ExitType { get; set; } // Tự nguyện, Hết hợp đồng, Chấm dứt HĐ...
        
        public DateTime? NoticeDate { get; set; }
        public DateTime LastWorkingDate { get; set; }
        public DateTime EffectiveDate { get; set; }
        
        public string? Reason { get; set; }
        public string? Notes { get; set; }

        public int? ResponsiblePersonId { get; set; }
        public string? ResponsiblePersonSnapshot { get; set; }

        // Workflow Status: DRAFT, PENDING_APPROVAL, APPROVED, REJECTED, CANCELLED
        public string ApprovalStatus { get; set; } = "DRAFT";
        [NotMapped] public string? Status { get => ApprovalStatus; set => ApprovalStatus = value ?? "DRAFT"; }
        public int GetId() => Id;
        
        // Progress Status: PROCESSING, HANDOVER_PENDING, SETTLEMENT_PENDING, COMPLETED
        public string OffboardingStatus { get; set; } = "PROCESSING";

        // Sub-status
        public string HandoverStatus { get; set; } = "PENDING"; // PENDING, IN_PROGRESS, COMPLETED
        public string AttendanceClosingStatus { get; set; } = "PENDING"; // PENDING, COMPLETED
        public string LeaveClosingStatus { get; set; } = "PENDING"; // PENDING, COMPLETED
        public string SettlementStatus { get; set; } = "PENDING"; // PENDING, IN_PROGRESS, COMPLETED

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

        public string? CompletedBy { get; set; }
        public DateTime? CompletedAt { get; set; }

        public ICollection<OffboardingHistory> Histories { get; set; } = new List<OffboardingHistory>();
        public ICollection<OffboardingChecklistTask> ChecklistTasks { get; set; } = new List<OffboardingChecklistTask>();

        // Computed properties for UI
        [NotMapped]
        public string ApprovalStatusLabel => ApprovalStatus switch
        {
            "DRAFT" => "Bản nháp",
            "PENDING_APPROVAL" => "Chờ duyệt",
            "APPROVED" => "Đã duyệt",
            "REJECTED" => "Từ chối",
            "CANCELLED" => "Đã hủy",
            _ => ApprovalStatus
        };

        [NotMapped]
        public string OffboardingStatusLabel => OffboardingStatus switch
        {
            "PROCESSING" => "Đang xử lý",
            "HANDOVER_PENDING" => "Chờ bàn giao",
            "SETTLEMENT_PENDING" => "Chờ quyết toán",
            "COMPLETED" => "Đã nghỉ",
            _ => OffboardingStatus
        };
    }
}
