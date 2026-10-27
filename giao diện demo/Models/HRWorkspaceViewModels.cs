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
