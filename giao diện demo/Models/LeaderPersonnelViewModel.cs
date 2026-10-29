using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class LeaderPersonnelDashboardDto
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int OnLeaveEmployees { get; set; }
        public int NewEmployeesThisMonth { get; set; }
    }

    public class LeaderEmployeeDto
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; }
        public string Name { get; set; }
        public string AvatarPath { get; set; }
        public string Department { get; set; }
        public string JobTitle { get; set; }
        public string Position { get; set; } // Trạng thái: Chính thức, Thử việc...
        public DateTime? JoinDate { get; set; }
        public string? Status { get; set; }
        public string? ManagerName { get; set; }
    }

    public class LeaderEmployeeDetailDto
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; }
        public string Name { get; set; }
        public string AvatarPath { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }
        public string JobTitle { get; set; }
        public DateTime? JoinDate { get; set; }
        public string? Status { get; set; }
        public string? ManagerName { get; set; }
    }

    public class ProposeEmployeeChangeDto
    {
        public int? EmployeeId { get; set; }
        public string Type { get; set; } // "Create" or "Update"
        public string Department { get; set; }
        public string JobTitle { get; set; }
        public string Reason { get; set; }
        public string Notes { get; set; }
    }

    public class LeaderPersonnelPageResponse<T>
    {
        public List<T> Items { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<string> AllowedDepartments { get; set; }
    }
}

