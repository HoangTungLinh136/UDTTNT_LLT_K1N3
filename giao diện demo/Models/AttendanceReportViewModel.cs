using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class AttendanceReportViewModel
    {
        public string CurrentMonth { get; set; } = DateTime.Now.ToString("MM/yyyy");
        public string? SelectedDepartment { get; set; }
        public string? SearchEmployee { get; set; }
        
        public int TotalEmployees { get; set; }
        public double TotalWorkingDays { get; set; }
        public double AverageAttendanceRate { get; set; }
        public double TotalOvertimeHours { get; set; }
        public double TotalLeaveDays { get; set; }

        public List<DepartmentAttendanceSummary> DepartmentSummaries { get; set; } = new List<DepartmentAttendanceSummary>();
        public List<EmployeeAttendanceDetail> EmployeeDetails { get; set; } = new List<EmployeeAttendanceDetail>();
        public List<string> Departments { get; set; } = new List<string>();
    }

    public class DepartmentAttendanceSummary
    {
        public string DepartmentName { get; set; } = "";
        public double TotalWorkingDays { get; set; }
        public double AttendanceRate { get; set; }
        public double TotalOvertimeHours { get; set; }
    }

    public class EmployeeAttendanceDetail
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string Department { get; set; } = "";
        
        public double ExpectedWorkingDays { get; set; }
        public double ActualWorkingDays { get; set; }
        public double LeaveDays { get; set; }
        public double UnauthorizedAbsenceDays { get; set; }
        public double OvertimeHours { get; set; }
        public int LateCount { get; set; }
        public double AttendanceRate { get; set; }
    }
}

