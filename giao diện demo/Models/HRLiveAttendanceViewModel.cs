using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class HRLiveAttendanceViewModel
    {
        public HRLiveSummaryDto Summary { get; set; } = new();
        public List<HRLiveAttendanceRowDto> Rows { get; set; } = new();
        public List<string> Departments { get; set; } = new();

        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public string? SelectedDepartment { get; set; }
        public string? SelectedStatus { get; set; }
        public string? SearchTerm { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
    }

    public class HRLiveSummaryDto
    {
        public int TotalEmployees { get; set; }
        public int CheckedInCount { get; set; }
        public int CurrentlyWorkingCount { get; set; }
        public int LateCount { get; set; }
        public int CheckedOutCount { get; set; }
        public int NotCheckedInCount { get; set; }
        public string ServerTime { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
    }

    public class HRLiveAttendanceRowDto
    {
        public int AttendanceId { get; set; }
        public int? EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public string CheckInStr => CheckIn?.ToString("HH:mm:ss") ?? "--:--:--";
        public string CheckOutStr => CheckOut?.ToString("HH:mm:ss") ?? "--:--:--";
        public string CheckInRaw => CheckIn?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "";
        public string CheckOutRaw => CheckOut?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "";
        public string StatusCode { get; set; } = "NOT_CHECKED_IN"; // NOT_CHECKED_IN, CHECKED_IN, CHECKED_OUT
        public string StatusText { get; set; } = "Chưa chấm công";
        public int LateMinutes { get; set; }
        public double WorkingHours { get; set; }
        public string? StatusNote { get; set; }
        public string? AdjustmentStatus { get; set; }
        public string UpdatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
    }
}
