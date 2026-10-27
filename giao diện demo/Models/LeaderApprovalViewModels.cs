using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class LeaderApprovalCenterViewModel
    {
        public int ProposalPendingCount { get; set; }
        public int ProcessedPage { get; set; } = 1;
        public int ProcessedPageSize { get; set; } = 25;
        public int ProcessedPageCount => ProcessedCount == 0 ? 0 : (int)Math.Ceiling(ProcessedCount / (double)ProcessedPageSize);
        public string ActiveTab { get; set; } = "all";
        public int TotalPendingCount => AppointmentPendingCount + DismissalPendingCount + TransferPendingCount + RewardPendingCount + ProposalPendingCount;
        public int AppointmentPendingCount { get; set; }
        public int DismissalPendingCount { get; set; }
        public int TransferPendingCount { get; set; }
        public int RewardPendingCount { get; set; }
        public int ProcessedCount { get; set; }

        public List<LeaderApprovalItemViewModel> PendingItems { get; set; } = new();
        public List<LeaderApprovalItemViewModel> ProcessedItems { get; set; } = new();
    }

    public class LeaderApprovalItemViewModel
    {
        public int Id { get; set; }
        public string WorkflowType { get; set; } = string.Empty; // "APPOINTMENT", "DISMISSAL", "TRANSFER", "REWARD"
        public string WorkflowTypeLabel { get; set; } = string.Empty; // "Bổ nhiệm", "Miễn nhiệm", "Điều chuyển", "Khen thưởng"
        public string WorkflowBadgeClass { get; set; } = string.Empty;
        public string WorkflowIcon { get; set; } = string.Empty;

        public int? EmployeeId { get; set; }
        public string? EmployeeCode { get; set; } = string.Empty;
        public string? EmployeeName { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public string? Department { get; set; } = string.Empty;
        public string? CurrentPosition { get; set; } = string.Empty;

        public string? SummaryChange { get; set; } = string.Empty;
        public string? Reason { get; set; } = string.Empty;
        public string? DecisionNumber { get; set; }
        public string? ProposedBy { get; set; } = string.Empty;
        public DateTime ProposedAt { get; set; }
        public DateTime? EffectiveDate { get; set; }

        public string? ApprovalStatus { get; set; } = string.Empty;
        public string? ApprovalStatusLabel { get; set; } = string.Empty;
        public bool CanApprove { get; set; } = true;
        public string? SelfApprovalNotice { get; set; }

        // Action routes
        public string ControllerName { get; set; } = string.Empty; // "Appointment", "Dismissal", "Transfer", "Reward"
        public string DetailUrl => $"/{ControllerName}/Detail/{Id}";
        public string ApproveUrl => $"/{ControllerName}/Approve/{Id}";
        public string RejectUrl => $"/{ControllerName}/Reject/{Id}";
    }
}
