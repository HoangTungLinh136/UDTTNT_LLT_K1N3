using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class HRLeaveSummaryViewModel
    {
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int OnLeaveToday { get; set; }
    }

    public class LeaveItemViewModel
    {
        public int Id { get; set; }
        public int? EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "---";
        public string EmployeeName { get; set; } = "---";
        public string? Department { get; set; }
        public string? AvatarUrl { get; set; }
        public string LeaveType { get; set; } = "Nghỉ phép năm";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int DurationDays { get; set; }
        public string? Reason { get; set; }
        public string Status { get; set; } = "Chờ duyệt";
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class HRLeaveIndexViewModel
    {
        public string CurrentRole { get; set; } = "HR";
        public HRLeaveSummaryViewModel Summary { get; set; } = new();
        public List<LeaveItemViewModel> Items { get; set; } = new();

        // Active Filters
        public string? Search { get; set; }
        public string? Department { get; set; }
        public string? LeaveType { get; set; }
        public string? Status { get; set; }
        public string? FromDate { get; set; } // dd/MM/yyyy
        public string? ToDate { get; set; }   // dd/MM/yyyy

        // Dropdown options
        public List<string> DepartmentOptions { get; set; } = new();
        public List<string> LeaveTypeOptions { get; set; } = new();
        public List<string> StatusOptions { get; set; } = new();

        // Pagination
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / (PageSize > 0 ? PageSize : 10));
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
