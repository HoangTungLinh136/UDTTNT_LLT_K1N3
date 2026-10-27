using System;
using System.Collections;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public sealed class EmployeeListViewModel : IEnumerable<Employee>
    {
        public List<Employee> Employees { get; set; } = new();
        public List<string> Departments { get; set; } = new();
        public List<string> Statuses { get; set; } = new();
        public string SearchName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int AllEmployeeCount { get; set; }
        public int ActiveEmployeeCount { get; set; }
        public int ProbationEmployeeCount { get; set; }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)Math.Max(1, PageSize)));
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        public IEnumerator<Employee> GetEnumerator() => Employees.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
