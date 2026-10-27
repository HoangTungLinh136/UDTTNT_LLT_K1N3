using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class Report
    {
        public int Id { get; set; }
        public string? EmployeeCode { get; set; }
        public string EmployeeName { get; set; }
        [NotMapped] public string Department { get; set; }
        public string HolidayBonus { get; set; }
        public string DiligenceBonus { get; set; }
        public string Reward { get; set; }
        public string Discipline { get; set; }
        public int LeaveDays { get; set; }
        public string LeaveType { get; set; }
        public string TaskStatus { get; set; }
    }
}
