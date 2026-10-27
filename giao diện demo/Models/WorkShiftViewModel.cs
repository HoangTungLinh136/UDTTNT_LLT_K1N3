using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace giao_dien_demo.Models
{
    public class WorkShiftViewModel
    {
        public List<WorkShift> Shifts { get; set; } = new List<WorkShift>();

        // KPIs
        public int TotalShifts { get; set; }
        public int ActiveShifts { get; set; }
        public int InactiveShifts { get; set; }
        public int OvernightShifts { get; set; }

        // Filters
        public string? SearchKeyword { get; set; }
        public string? FilterType { get; set; }
        public string? FilterStatus { get; set; }

        // Master Data for Selects
        public List<SelectListItem> ShiftTypes { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Departments { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Employees { get; set; } = new List<SelectListItem>();
    }

    public class WorkShiftDto
    {
        public int Id { get; set; }
        public string ShiftCode { get; set; } = "";
        public string ShiftName { get; set; } = "";
        public string? ShiftType { get; set; }
        public string StartTime { get; set; } = ""; // HH:mm
        public string EndTime { get; set; } = "";   // HH:mm
        public int BreakDurationMinutes { get; set; }
        public int LateGraceMinutes { get; set; }
        public int EarlyLeaveGraceMinutes { get; set; }
        public bool IsOvernight { get; set; }
        public bool IsOvertimeEnabled { get; set; }
        public string Status { get; set; } = "Active";
        public string? Notes { get; set; }
        
        public List<string> AssignedDepartments { get; set; } = new List<string>();
        public List<string> AssignedEmployees { get; set; } = new List<string>();
    }
}
