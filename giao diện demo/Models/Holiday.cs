using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class Holiday
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string HolidayName { get; set; } = "";

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        [StringLength(50)]
        public string ScopeType { get; set; } = "GLOBAL"; // GLOBAL, DEPARTMENT, EMPLOYEE

        [StringLength(100)]
        public string ScopeValue { get; set; } = ""; // DepartmentName or EmployeeCode

        public bool IsPaidHoliday { get; set; } = true;

        [StringLength(50)]
        public string Status { get; set; } = "ACTIVE"; // ACTIVE, INACTIVE

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }

    [NotMapped]
    public class HolidayViewModel
    {
        public int TotalHolidays { get; set; }
        public int ActiveHolidays { get; set; }
        public int InactiveHolidays { get; set; }

        public int FilterYear { get; set; }
        public string FilterScope { get; set; } = "";
        public string FilterStatus { get; set; } = "";

        public System.Collections.Generic.List<Holiday> Holidays { get; set; } = new System.Collections.Generic.List<Holiday>();
        public System.Collections.Generic.List<int> AvailableYears { get; set; } = new System.Collections.Generic.List<int>();
        public System.Collections.Generic.List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Departments { get; set; } = new System.Collections.Generic.List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
        public System.Collections.Generic.List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Employees { get; set; } = new System.Collections.Generic.List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
    }

    [NotMapped]
    public class HolidayDto
    {
        public int Id { get; set; }
        public string HolidayName { get; set; } = "";
        public string FromDate { get; set; } = "";
        public string ToDate { get; set; } = "";
        public string ScopeType { get; set; } = "GLOBAL";
        public string ScopeValue { get; set; } = "";
        public bool IsPaidHoliday { get; set; } = true;
        public string Status { get; set; } = "ACTIVE";
        public string Notes { get; set; } = "";
    }
}
