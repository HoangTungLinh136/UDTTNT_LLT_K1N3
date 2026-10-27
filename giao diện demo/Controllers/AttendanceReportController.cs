using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = "HR,Leader")]
    public class AttendanceReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeadershipScopeService _leadershipScope;

        public AttendanceReportController(ApplicationDbContext context, ILeadershipScopeService leadershipScope)
        {
            _context = context;
            _leadershipScope = leadershipScope;
        }

        private (int Month, int Year) ResolveMonth(string month)
        {
            var now = DateTime.Now;
            var parts = (month ?? string.Empty).Split('/');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var parsedMonth) && parsedMonth is >= 1 and <= 12 &&
                int.TryParse(parts[1], out var parsedYear) && parsedYear is >= 1900 and <= 9999)
            {
                return (parsedMonth, parsedYear);
            }

            return (now.Month, now.Year);
        }

        private IQueryable<Employee> GetScopedEmployees(string department)
        {
            var employees = _context.Employees.AsQueryable();
            if (HttpContext.Session.GetString("Role") == "Leader")
            {
                var allowedDepartments = _leadershipScope.GetOverseenOrganizationalUnits();
                employees = employees.Where(e => e.Department != null && allowedDepartments.Contains(e.Department));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                employees = employees.Where(e => e.Department == department);
            }

            return employees;
        }

        public IActionResult Index(string month = "", string department = "", string keyword = "")
        {
            if (string.IsNullOrEmpty(month)) month = DateTime.Now.ToString("MM/yyyy");

            var (m, y) = ResolveMonth(month);
            month = $"{m:00}/{y:0000}";

            var employees = GetScopedEmployees(department).ToList();
            var attendances = _context.Attendances
                .Where(a => a.CheckIn.Month == m && a.CheckIn.Year == y)
                .ToList();
            
            var leaves = _context.Leaves.Where(l => l.FromDate.Month == m && l.FromDate.Year == y && (l.Status == "Approved" || l.Status == "Đã duyệt")).ToList();

            if (!string.IsNullOrEmpty(keyword))
            {
                employees = employees.Where(e => (e.Name != null && e.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) || (e.EmployeeCode != null && e.EmployeeCode.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList();
            }

            var vm = new AttendanceReportViewModel
            {
                CurrentMonth = month,
                SelectedDepartment = department,
                Departments = GetScopedEmployees(string.Empty).Where(e => !string.IsNullOrEmpty(e.Department)).Select(e => e.Department!).Distinct().ToList()
            };

            int daysInMonth = DateTime.DaysInMonth(y, m);
            // Assume 22 expected working days roughly for a month
            double expectedDays = 22;

            foreach (var emp in employees)
            {
                var empAtts = attendances.Where(a => a.EmployeeId == emp.Id).ToList();
                var empLeaves = leaves.Where(l => l.EmployeeId == emp.Id).ToList();
                
                var actualDays = empAtts.Select(a => a.CheckIn.Date).Distinct().Count();
                var leaveDays = empLeaves.Sum(l => (l.ToDate - l.FromDate).TotalDays + 1);
                var overtime = empAtts.Sum(a => a.OvertimeHours);
                var lateCount = empAtts.Count(a => a.LateMinutes > 0);
                
                var attendanceRate = expectedDays > 0 ? (actualDays / expectedDays) * 100 : 0;
                if (attendanceRate > 100) attendanceRate = 100;

                vm.EmployeeDetails.Add(new EmployeeAttendanceDetail
                {
                    EmployeeId = emp.Id,
                    EmployeeCode = emp.EmployeeCode ?? "",
                    EmployeeName = emp.Name ?? "",
                    Department = emp.Department ?? "Khác",
                    ExpectedWorkingDays = expectedDays,
                    ActualWorkingDays = actualDays,
                    LeaveDays = leaveDays,
                    UnauthorizedAbsenceDays = Math.Max(0, expectedDays - actualDays - leaveDays),
                    OvertimeHours = overtime,
                    LateCount = lateCount,
                    AttendanceRate = attendanceRate
                });
            }

            vm.TotalEmployees = vm.EmployeeDetails.Count;
            vm.TotalWorkingDays = vm.EmployeeDetails.Sum(e => e.ActualWorkingDays);
            vm.TotalOvertimeHours = vm.EmployeeDetails.Sum(e => e.OvertimeHours);
            vm.TotalLeaveDays = vm.EmployeeDetails.Sum(e => e.LeaveDays);
            vm.AverageAttendanceRate = vm.EmployeeDetails.Any() ? vm.EmployeeDetails.Average(e => e.AttendanceRate) : 0;

            vm.DepartmentSummaries = vm.EmployeeDetails.GroupBy(e => e.Department)
                .Select(g => new DepartmentAttendanceSummary
                {
                    DepartmentName = g.Key,
                    TotalWorkingDays = g.Sum(x => x.ActualWorkingDays),
                    AttendanceRate = g.Average(x => x.AttendanceRate),
                    TotalOvertimeHours = g.Sum(x => x.OvertimeHours)
                }).ToList();

            return View(vm);
        }

        public IActionResult Late(string month = "", string department = "", string keyword = "")
        {
            if (string.IsNullOrEmpty(month)) month = DateTime.Now.ToString("MM/yyyy");

            var (m, y) = ResolveMonth(month);
            month = $"{m:00}/{y:0000}";

            var attendances = _context.Attendances
                .Where(a => a.CheckIn.Month == m && a.CheckIn.Year == y && a.LateMinutes > 0)
                .ToList();

            var employees = GetScopedEmployees(department).ToList();

            var lateData = from a in attendances
                           join e in employees on a.EmployeeName equals e.Name
                           select new { a, e };

            if (!string.IsNullOrEmpty(keyword))
            {
                lateData = lateData.Where(x => (x.e.Name != null && x.e.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) || (x.e.EmployeeCode != null && x.e.EmployeeCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            }

            var vm = new LateReportViewModel
            {
                CurrentMonth = month,
                SelectedDepartment = department,
                SearchEmployee = keyword,
                Departments = employees.Where(e => !string.IsNullOrEmpty(e.Department)).Select(e => e.Department!).Distinct().ToList()
            };

            vm.TotalLateOccurrences = lateData.Count();
            vm.TotalLateMinutes = lateData.Sum(x => x.a.LateMinutes);
            vm.TotalEmployeesWithLate = lateData.Select(x => x.e.Id).Distinct().Count();

            var deptGroup = lateData.GroupBy(x => x.e.Department ?? "Khác")
                                    .Select(g => new DepartmentLateRateDto
                                    {
                                        DepartmentName = g.Key,
                                        LateRate = g.Count()
                                    }).ToList();
            vm.DepartmentRates = deptGroup;
            if (deptGroup.Any())
            {
                vm.HighestRateDepartment = deptGroup.OrderByDescending(x => x.LateRate).First().DepartmentName;
            }

            var empGroup = lateData.GroupBy(x => x.e)
                                   .Select(g => new LateEmployeeSummaryDto
                                   {
                                       EmployeeId = g.Key.Id,
                                       EmployeeCode = g.Key.EmployeeCode ?? "",
                                       EmployeeName = g.Key.Name ?? "",
                                       DepartmentName = g.Key.Department ?? "",
                                       LateCount = g.Count(),
                                       TotalLateMinutes = g.Sum(x => x.a.LateMinutes),
                                       AverageLateMinutes = g.Average(x => x.a.LateMinutes),
                                       LatestLateDate = g.Max(x => x.a.CheckIn)
                                   }).OrderByDescending(x => x.LateCount).ToList();
            vm.EmployeeSummaries = empGroup;

            var daysInMonth = DateTime.DaysInMonth(y, m);
            for (int i = 1; i <= daysInMonth; i++)
            {
                var date = new DateTime(y, m, i);
                if (date > DateTime.Now) break;
                vm.DailyTrend.Add(new DailyLateTrendDto
                {
                    DateLabel = date.ToString("dd/MM"),
                    LateOccurrences = lateData.Count(x => x.a.CheckIn.Date == date.Date)
                });
            }

            return View(vm);
        }
    }
}
