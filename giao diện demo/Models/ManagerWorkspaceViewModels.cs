using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class ManagerWorkspaceViewModel
    {
        public string? ErrorMessage { get; set; }
        public int Year { get; set; } = DateTime.Today.Year;
        public string Unit { get; set; } = "";
        public List<int> AvailableYears { get; set; } = new();
        public int WorkingEmployeeCount { get; set; }
        public int PendingApprovalCount => AssignedLeavePendingCount + AssignedAdjustmentPendingCount;
        public decimal WorkingPercent => ManagedEmployeeCount == 0 ? 0 : Math.Round(100m * WorkingEmployeeCount / ManagedEmployeeCount, 1);
        public decimal LeavePercent => ManagedEmployeeCount == 0 ? 0 : Math.Round(100m * OnLeaveTodayCount / ManagedEmployeeCount, 1);
        public List<ManagerPositionGroup> PositionGroups { get; set; } = new();
        public List<ManagerMovementMonth> Movement { get; set; } = new();
        public List<ManagerDashboardEmployee> RecentEmployees { get; set; } = new();
        public int MissingJoinDateCount { get; set; }
        public int ManagedEmployeeCount { get; set; }
        public int PresentTodayCount { get; set; }
        public int AssignedLeavePendingCount { get; set; }
        public int AssignedAdjustmentPendingCount { get; set; }
        public int OnLeaveTodayCount { get; set; }
        public int CompletedKpiCount { get; set; }
        public List<string> OrganizationalUnits { get; set; } = new();
        public List<ManagerApprovalRow> RecentApprovals { get; set; } = new();
    }

    public class ManagerPositionGroup
    {
        public string Label { get; set; } = "";
        public int Count { get; set; }
        public decimal Percent { get; set; }
    }

    public class ManagerMovementMonth
    {
        public int Month { get; set; }
        public int Joined { get; set; }
        public int Left { get; set; }
        public int Transferred { get; set; }
    }

    // Deliberately contains only the fields already visible in the scoped employee list,
    // plus the business join date. Never pass the full Employee entity to this dashboard.
    public class ManagerDashboardEmployee
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? EmployeeCode { get; set; }
        public string? JobTitle { get; set; }
        public string? Position { get; set; }
        public string? Department { get; set; }
        public string? Status { get; set; }
        public DateTime? JoinDate { get; set; }
    }

    public class ManagerListViewModel<T>
    {
        public string Search { get; set; } = "";
        public string Filter { get; set; } = "";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        /// <summary>True when the current manager has at least one active organizational assignment.</summary>
        public bool HasManagerScope { get; set; }
        /// <summary>Current active organizational units; populated only for screens that expose a unit filter.</summary>
        public List<string> OrganizationalUnits { get; set; } = new();
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
        public List<T> Items { get; set; } = new();
    }

    public class ManagerApprovalRow
    {
        public string Type { get; set; } = "";
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string EmployeeCode { get; set; } = "";
        public string Department { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime SubmittedAt { get; set; }
    }

    public class ManagerAttendanceRow
    {
        public int Id { get; set; }
        public int? EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "";
        public string EmployeeCode { get; set; } = "";
        public string Department { get; set; } = "";
        public DateTime CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public string Status { get; set; } = "";
    }

    public class ManagerReportRow
    {
        public string Department { get; set; } = "";
        public int Employees { get; set; }
        public int PresentToday { get; set; }
        public int LateToday { get; set; }
        public int PendingLeaves { get; set; }
        public int PendingAdjustments { get; set; }
    }
}
