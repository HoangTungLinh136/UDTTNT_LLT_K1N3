using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public sealed class ManagerAssignmentListItem
    {
        public int Id { get; set; }
        public string OrganizationalUnit { get; set; } = string.Empty;
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? AssignedBy { get; set; }
        public bool CanEdit { get; set; }
        public bool CanEnd { get; set; }
    }

    public sealed class ManagerAssignmentInput
    {
        public int EmployeeId { get; set; }
        public int? AssignmentId { get; set; }
        public string OrganizationalUnit { get; set; } = string.Empty;
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }

    public sealed class ManagerAssignmentDetailsViewModel
    {
        public int EmployeeId { get; set; }
        public bool IsManager { get; set; }
        public List<ManagerAssignmentListItem> Assignments { get; set; } = new();
        public List<string> AvailableUnits { get; set; } = new();
    }
}
