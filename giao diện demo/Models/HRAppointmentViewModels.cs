using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class HRAppointmentSummaryViewModel
    {
        public int TotalRecords { get; set; }
        public int PendingHR { get; set; }
        public int PendingLeader { get; set; }
        public int Approved { get; set; }
        public int Effective { get; set; }
        public int Rejected { get; set; }
        public int Cancelled { get; set; }
    }

    public class AppointmentItemViewModel
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "---";
        public string EmployeeName { get; set; } = "---";
        public string? AvatarPath { get; set; }

        public string? PreviousDepartment { get; set; }
        public string? PreviousPosition { get; set; }
        public string? PreviousJobTitle { get; set; }

        public string? NewDepartment { get; set; }
        public string? NewPosition { get; set; }
        public string? NewJobTitle { get; set; }

        public DateTime EffectiveDate { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }

        public string? DecisionNumber { get; set; }
        public DateTime? DecisionDate { get; set; }

        public string ApprovalStatus { get; set; } = "DRAFT";
        public string ApprovalStatusLabel { get; set; } = "Bản nháp";
        public string EffectiveStatus { get; set; } = "---";
        public string EffectiveStatusLabel { get; set; } = "---";

        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectionReason { get; set; }
        public string? AppliedBy { get; set; }
        public DateTime? AppliedAt { get; set; }
    }

    public class HRAppointmentIndexViewModel
    {
        public string CurrentRole { get; set; } = "HR";
        public HRAppointmentSummaryViewModel Summary { get; set; } = new();
        public List<AppointmentItemViewModel> Items { get; set; } = new();

        // Filters
        public string? SearchName { get; set; }
        public string? Department { get; set; } // New Department
        public string? PreviousDepartment { get; set; } // Current/Previous Department
        public string? Position { get; set; } // New Position
        public string? Status { get; set; }
        public string? EffectiveDate { get; set; } // yyyy-MM-dd

        // Options
        public List<string> DepartmentOptions { get; set; } = new();
        public List<string> PositionOptions { get; set; } = new();

        // Pagination
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / (PageSize > 0 ? PageSize : 10));
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
