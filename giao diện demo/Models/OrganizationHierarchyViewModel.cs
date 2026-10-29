using System;
using System.Collections.Generic;
using System.Linq;

namespace giao_dien_demo.Models
{
    public class ReportingHistoryItemDto
    {
        public int Id { get; set; }
        public string? PreviousManagerName { get; set; }
        public string? PreviousManagerCode { get; set; }
        public string? NewManagerName { get; set; }
        public string? NewManagerCode { get; set; }
        public string ChangeReason { get; set; } = string.Empty;
        public DateTime EffectiveDate { get; set; }
        public string? ChangedByName { get; set; }
    }

    public class HierarchyNodeViewModel
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? Status { get; set; }
        public int? ManagerId { get; set; }
        public string? ManagerName { get; set; }
        public string? ManagerCode { get; set; }
        public string? AvatarPath { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? JoinDateFormatted { get; set; }
        public int Level { get; set; }
        public bool IsCycleDetected { get; set; }
        public bool IsCurrentUser { get; set; }
        public bool IsManagerOutsideScope { get; set; }
        public bool CanViewDetails { get; set; }
        public bool CanManageManager { get; set; }
        public List<HierarchyNodeViewModel> DirectReports { get; set; } = new();
        public List<ReportingHistoryItemDto> ReportingHistories { get; set; } = new();
        public int TotalDescendantsCount { get; set; }

        public string HierarchyState => !ManagerId.HasValue 
            ? "StateA_Unassigned" 
            : (IsManagerOutsideScope ? "StateB_ManagerOutsideScope" : "StateC_Connected");
    }

    public class OrganizationHierarchyViewModel
    {
        public List<HierarchyNodeViewModel> RootNodes { get; set; } = new();
        public List<HierarchyNodeViewModel> StandaloneNodes => RootNodes.Where(r => !r.ManagerId.HasValue && r.DirectReports.Count == 0).ToList();
        public List<HierarchyNodeViewModel> UnassignedNodes => StandaloneNodes;
        public int TotalEmployees { get; set; }
        public int TotalManagers { get; set; }
        public int TotalUnassigned { get; set; }
        public int ActualManagerNullCount { get; set; }
        public int ManagerOutsideScopeCount { get; set; }
        public int ConnectedRelationshipsCount { get; set; }
        public string? SelectedDepartment { get; set; }
        public List<string> AvailableDepartments { get; set; } = new();
        public string CurrentUserRole { get; set; } = string.Empty;
        public string DataScopeDescription { get; set; } = string.Empty;
        public int? CurrentEmployeeId { get; set; }
    }
}
