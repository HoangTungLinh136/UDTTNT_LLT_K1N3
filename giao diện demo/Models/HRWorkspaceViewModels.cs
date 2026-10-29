using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class HRWorkspaceViewModel
    {
        public List<string> ModuleCategories { get; set; } = new();
        public int ActiveEmployeeCount { get; set; }
        public int PresentTodayCount { get; set; }
        public int PendingActionCount { get; set; }
        public int ExpiringContractCount { get; set; }
        public string Search { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalModules { get; set; }
        public List<HRWorkspaceModule> Modules { get; set; } = new();
        public List<HRPendingWorkItem> PendingWork { get; set; } = new();
        public List<HRDepartmentSummary> Departments { get; set; } = new();

        // --- ENHANCEMENTS FOR MODERN HR DASHBOARD ---
        public int TotalEmployees { get; set; }
        public int MonthlyGrowth { get; set; }
        public string GrowthText { get; set; } = string.Empty;
        public double WorkingPercentage { get; set; }
        public int OnLeaveTodayCount { get; set; }
        public double OnLeavePercentage { get; set; }

        // Chart 1: Donut Departments
        public List<HRDepartmentChartItem> DepartmentChartItems { get; set; } = new();

        // Chart 2: Monthly Headcount Turnover
        public int SelectedYear { get; set; } = 2026;
        public List<int> AvailableYears { get; set; } = new();
        public int[] MonthlyNewHires { get; set; } = new int[12];
        public int[] MonthlyTerminations { get; set; } = new int[12];
        public int[] MonthlyTransfers { get; set; } = new int[12];

        // Today HR Tasks
        public int PendingOnboardingCount { get; set; }
        public int PendingLeaveCount { get; set; }
        public int PendingTransferCount { get; set; }

        // Recent Employees
        public List<HRRecentEmployeeItem> RecentEmployees { get; set; } = new();
    }

    public class HRDepartmentChartItem
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = "#2563eb";
    }

    public class HRRecentEmployeeItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; }
        public string Status { get; set; } = "Đang làm việc";
        public string AvatarLetter { get; set; } = "N";
    }

    public class HRWorkspaceModule
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = "Index";
        public string Icon { get; set; } = "fa-circle";
    }

    public class HRPendingWorkItem
    {
        public string Type { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = "Index";
        public string RouteQuery { get; set; } = string.Empty;
    }

    public class HRDepartmentSummary
    {
        public string Department { get; set; } = string.Empty;
        public int Employees { get; set; }
    }
}
