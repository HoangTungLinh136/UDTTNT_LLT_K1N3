using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class LateReportViewModel
    {
        public string CurrentMonth { get; set; } = DateTime.Now.ToString("MM/yyyy");
        public string? SelectedDepartment { get; set; }
        public string? SearchEmployee { get; set; }

        public int TotalLateOccurrences { get; set; }
        public int TotalLateMinutes { get; set; }
        public int TotalEmployeesWithLate { get; set; }
        public string HighestRateDepartment { get; set; } = "—";

        public List<string> Departments { get; set; } = new List<string>();
        public List<LateEmployeeSummaryDto> EmployeeSummaries { get; set; } = new List<LateEmployeeSummaryDto>();
        public List<DailyLateTrendDto> DailyTrend { get; set; } = new List<DailyLateTrendDto>();
        public List<DepartmentLateRateDto> DepartmentRates { get; set; } = new List<DepartmentLateRateDto>();
    }

    public class LateEmployeeSummaryDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public int LateCount { get; set; }
        public int TotalLateMinutes { get; set; }
        public double AverageLateMinutes { get; set; }
        public DateTime? LatestLateDate { get; set; }
    }

    public class LateOccurrenceDto
    {
        public int AttendanceId { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan? ScheduledStart { get; set; } // Giả định có thể là 08:00
        public DateTime? ActualCheckIn { get; set; }
        public int LateMinutes { get; set; }
        public string? StatusNote { get; set; }
        public bool IsAdjusted { get; set; }
    }

    public class DailyLateTrendDto
    {
        public string DateLabel { get; set; } = "";
        public int LateOccurrences { get; set; }
    }

    public class DepartmentLateRateDto
    {
        public string DepartmentName { get; set; } = "";
        public double LateRate { get; set; }
    }
}
