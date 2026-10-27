using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace giao_dien_demo.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = giao_dien_demo.Authorization.AppPolicies.LeaderOnly)]
    public class LeaderAttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IAttendanceCalculationService _attendanceCalc;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _scopeService;

        public LeaderAttendanceController(ApplicationDbContext context, giao_dien_demo.Services.IAttendanceCalculationService attendanceCalc, giao_dien_demo.Services.ILeadershipScopeService scopeService)
        {
            _context = context;
            _attendanceCalc = attendanceCalc;
            _scopeService = scopeService;
        }

        private (AttendanceReportViewModel ViewModel, List<Attendance> RawAttendances, List<Employee> FilteredEmployees, string PeriodStatus, int DaysInMonth) GetLeaderAttendanceDataset(string month, string department, string searchEmployee = "")
        {
            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits();
            
            if (string.IsNullOrEmpty(month)) month = DateTime.Now.ToString("MM/yyyy");
            else if (month == "MM/yyyy") month = DateTime.Now.ToString("MM/yyyy"); // Safe-guard against frontend placeholder bug
            
            var vm = new AttendanceReportViewModel
            {
                CurrentMonth = month,
                SelectedDepartment = department,
                SearchEmployee = searchEmployee,
                Departments = allowedDepts.OrderBy(d => d).ToList()
            };

            if (!allowedDepts.Any()) return (vm, new List<Attendance>(), new List<Employee>(), "OPEN", 0);

            // A requested department is only a narrowing filter. Returning an
            // empty dataset for an out-of-scope value prevents a crafted query
            // string from falling back to the leader's full organizational scope.
            var requestedDepartment = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            if (requestedDepartment != null && !allowedDepts.Contains(requestedDepartment))
            {
                return (vm, new List<Attendance>(), new List<Employee>(), "OPEN", 0);
            }

            int m = DateTime.Now.Month, y = DateTime.Now.Year;
            var parts = month.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[0], out int pm) && int.TryParse(parts[1], out int py))
            {
                m = pm; y = py;
            }

            int daysInMonth = DateTime.DaysInMonth(y, m);
            DateTime startDate = new DateTime(y, m, 1);
            DateTime endDate = new DateTime(y, m, daysInMonth, 23, 59, 59);

            var period = _context.AttendancePeriods.FirstOrDefault(p => p.Year == y && p.Month == m);
            string periodStatus = period?.Status ?? "OPEN";

            var employeesQuery = _context.Employees.Where(e => allowedDepts.Contains(e.Department));
            if (requestedDepartment != null)
            {
                employeesQuery = employeesQuery.Where(e => e.Department == requestedDepartment);
            }
            if (!string.IsNullOrEmpty(searchEmployee))
            {
                employeesQuery = employeesQuery.Where(e => 
                    (e.Name != null && e.Name.Contains(searchEmployee)) ||
                    (e.EmployeeCode != null && e.EmployeeCode.Contains(searchEmployee))
                );
            }
            var employees = employeesQuery.ToList();
            var scopedEmployeeIds = employees.Select(e => e.Id).ToHashSet();
            
            var leaderRole = "Leader"; // Enforced by the class-level LeaderOnly policy.

            // Apply the complete active multi-unit LeaderAssignment scope inside
            // IQueryable before date filters or materialization.
            var attendanceQuery = _attendanceCalc.GetScopedAttendanceQuery(
                leaderRole,
                null,
                allowedDepts,
                requestedDepartment);
            var attendances = attendanceQuery
                .Where(a => a.CheckIn >= startDate && a.CheckIn <= endDate)
                .ToList();

            var leaves = _context.Leaves
                // Correlate leave records by the canonical EmployeeId FK.
                // EmployeeNameSnapshot is historical display data only.
                .Where(l => l.EmployeeId.HasValue && scopedEmployeeIds.Contains(l.EmployeeId.Value)
                    && l.Status == "Đã duyệt"
                    && l.FromDate <= endDate
                    && l.ToDate >= startDate)
                .ToList();

            var holidays = _context.Holidays
                .Where(h => h.Status == "ACTIVE" && h.FromDate <= endDate && h.ToDate >= startDate)
                .ToList();

            vm.TotalEmployees = employees.Count;

            var deptGroups = employees.GroupBy(e => e.Department ?? "Khác").ToDictionary(g => g.Key, g => g.ToList());

            foreach (var dept in deptGroups)
            {
                double deptWorkingDays = 0;
                double deptOT = 0;
                double deptExpectedDays = 0;

                foreach (var emp in dept.Value)
                {
                    var empAttendances = attendances.Where(a => a.EmployeeId == emp.Id).ToList();
                    var empLeaves = leaves.Where(l => l.EmployeeId == emp.Id).ToList();

                    double expectedWorkingDays = 0;
                    for (int i = 1; i <= daysInMonth; i++)
                    {
                        DateTime date = new DateTime(y, m, i);
                        bool isRestDay = date.DayOfWeek == DayOfWeek.Sunday; 
                        
                        bool isHoliday = holidays.Any(h => date >= h.FromDate.Date && date <= h.ToDate.Date && 
                            (h.ScopeType == "GLOBAL" || (h.ScopeType == "DEPARTMENT" && h.ScopeValue == emp.Department) || (h.ScopeType == "EMPLOYEE" && h.ScopeValue == emp.EmployeeCode)));
                        
                        if (!isRestDay && !isHoliday) expectedWorkingDays++;
                    }

                    if (expectedWorkingDays == 0)
                    {
                        // Fallback in case of bad holiday data spanning the entire month
                        for (int i = 1; i <= daysInMonth; i++)
                        {
                            if (new DateTime(y, m, i).DayOfWeek != DayOfWeek.Sunday) expectedWorkingDays++;
                        }
                    }

                    deptExpectedDays += expectedWorkingDays;

                    double actualWorkingDays = empAttendances.Sum(a => a.StandardHours) / 8.0; 
                    double otHours = empAttendances.Sum(a => a.OvertimeHours);
                    int lateCount = empAttendances.Count(a => a.LateMinutes > 0);
                    
                    double leaveDays = 0;
                    foreach(var l in empLeaves)
                    {
                        var start = l.FromDate < startDate ? startDate : l.FromDate;
                        var end = l.ToDate > endDate ? endDate : l.ToDate;
                        for (DateTime d = start.Date; d <= end.Date; d = d.AddDays(1))
                        {
                            bool isRestDay = d.DayOfWeek == DayOfWeek.Sunday;
                            bool isHoliday = holidays.Any(h => d >= h.FromDate.Date && d <= h.ToDate.Date && 
                                (h.ScopeType == "GLOBAL" || (h.ScopeType == "DEPARTMENT" && h.ScopeValue == emp.Department) || (h.ScopeType == "EMPLOYEE" && h.ScopeValue == emp.EmployeeCode)));
                            
                            if (!isRestDay && !isHoliday) leaveDays++;
                        }
                    }

                    double unauthorizedDays = expectedWorkingDays - actualWorkingDays - leaveDays;
                    if (unauthorizedDays < 0) unauthorizedDays = 0;

                    double attRate = expectedWorkingDays > 0 ? (actualWorkingDays / expectedWorkingDays) * 100 : 0;
                    if (attRate > 100) attRate = 100;

                    deptWorkingDays += actualWorkingDays;
                    deptOT += otHours;

                    vm.EmployeeDetails.Add(new EmployeeAttendanceDetail
                    {
                        EmployeeId = emp.Id,
                        EmployeeCode = emp.EmployeeCode ?? "",
                        EmployeeName = emp.Name ?? "",
                        Department = emp.Department ?? "",
                        ExpectedWorkingDays = expectedWorkingDays,
                        ActualWorkingDays = Math.Round(actualWorkingDays, 2),
                        LeaveDays = leaveDays,
                        UnauthorizedAbsenceDays = Math.Round(unauthorizedDays, 2),
                        OvertimeHours = Math.Round(otHours, 2),
                        LateCount = lateCount,
                        AttendanceRate = Math.Round(attRate, 2)
                    });

                    vm.TotalWorkingDays += actualWorkingDays;
                    vm.TotalOvertimeHours += otHours;
                    vm.TotalLeaveDays += leaveDays;
                }

                double deptAttRate = deptExpectedDays > 0 ? (deptWorkingDays / deptExpectedDays) * 100 : 0;
                
                vm.DepartmentSummaries.Add(new DepartmentAttendanceSummary
                {
                    DepartmentName = dept.Key,
                    TotalWorkingDays = Math.Round(deptWorkingDays, 2),
                    AttendanceRate = Math.Round(deptAttRate, 2),
                    TotalOvertimeHours = Math.Round(deptOT, 2)
                });
            }

            double totalExpected = vm.EmployeeDetails.Sum(e => e.ExpectedWorkingDays);
            vm.AverageAttendanceRate = totalExpected > 0 ? (vm.TotalWorkingDays / totalExpected) * 100 : 0;
            vm.AverageAttendanceRate = Math.Round(vm.AverageAttendanceRate, 2);
            vm.TotalWorkingDays = Math.Round(vm.TotalWorkingDays, 2);
            vm.TotalOvertimeHours = Math.Round(vm.TotalOvertimeHours, 2);

            return (vm, attendances, employees, periodStatus, daysInMonth);
        }

        public IActionResult Index(string month = "", string department = "", string searchEmployee = "")
        {
            var result = GetLeaderAttendanceDataset(month, department, searchEmployee);
            ViewBag.PeriodStatus = result.PeriodStatus;

            return View(result.ViewModel);
        }
        
        public IActionResult ExportExcel(string month = "", string department = "", string searchEmployee = "")
        {
            var result = GetLeaderAttendanceDataset(month, department, searchEmployee);
            var vm = result.ViewModel;

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("Ma_NV,Ho_Ten,Phong_Ban,Cong_Chuan,Cong_Thuc_Te,Nghi_Phep,Khong_Phep,OT,Di_Muon,Ty_Le_CC");

            foreach (var emp in vm.EmployeeDetails)
            {
                csvBuilder.AppendLine($"{emp.EmployeeCode},{emp.EmployeeName},{emp.Department},{emp.ExpectedWorkingDays},{emp.ActualWorkingDays},{emp.LeaveDays},{emp.UnauthorizedAbsenceDays},{emp.OvertimeHours},{emp.LateCount},{emp.AttendanceRate}%");
            }

            string safeMonth = string.IsNullOrEmpty(month) ? DateTime.Now.ToString("MM_yyyy") : month.Replace("/", "_");
            string filename = $"BaoCaoChamCong_{safeMonth}.csv";
            return File(Encoding.UTF8.GetBytes(csvBuilder.ToString()), "text/csv", filename);
        }
        
        public IActionResult LateReport(string month = "", string department = "", string searchEmployee = "")
        {
            var result = GetLeaderAttendanceDataset(month, department, searchEmployee);
            var allAttendances = result.RawAttendances;
            var employeesList = result.FilteredEmployees;
            int daysInMonth = result.DaysInMonth;
            string actualMonth = result.ViewModel.CurrentMonth;

            var employeeIds = employeesList.Select(e => e.Id).ToHashSet();
            var filteredAttendances = allAttendances.Where(a => a.EmployeeId.HasValue && employeeIds.Contains(a.EmployeeId.Value)).ToList();
            var lateAttendances = filteredAttendances.Where(a => a.LateMinutes > 0).ToList();

            var vm = new LateReportViewModel
            {
                CurrentMonth = actualMonth,
                SelectedDepartment = department,
                SearchEmployee = searchEmployee,
                Departments = result.ViewModel.Departments,
                TotalLateOccurrences = lateAttendances.Count,
                TotalLateMinutes = lateAttendances.Sum(a => a.LateMinutes),
                TotalEmployeesWithLate = lateAttendances.Where(a => a.EmployeeId.HasValue).Select(a => a.EmployeeId).Distinct().Count()
            };

            var allDeptGroups = employeesList.GroupBy(e => e.Department ?? "Khác").ToList();
            double highestRate = -1;
            string highestRateDept = "—";

            foreach(var deptGroup in allDeptGroups)
            {
                var deptEmployeeIds = deptGroup.Select(e => e.Id).ToHashSet();
                var deptLates = allAttendances.Where(a => a.EmployeeId.HasValue && deptEmployeeIds.Contains(a.EmployeeId.Value) && a.LateMinutes > 0).Count();
                
                double deptExpectedDays = result.ViewModel.EmployeeDetails.Where(e => e.Department == deptGroup.Key).Sum(e => e.ExpectedWorkingDays);
                
                double rate = deptExpectedDays > 0 ? ((double)deptLates / deptExpectedDays) * 100 : 0;
                
                vm.DepartmentRates.Add(new DepartmentLateRateDto 
                {
                    DepartmentName = deptGroup.Key,
                    LateRate = Math.Round(rate, 2)
                });
            }

            foreach(var r in vm.DepartmentRates)
            {
                if(r.LateRate > highestRate)
                {
                    highestRate = r.LateRate;
                    highestRateDept = r.DepartmentName;
                }
            }
            if(highestRate >= 0)
            {
                vm.HighestRateDepartment = $"{highestRateDept} ({Math.Round(highestRate, 1)}%)";
            }

            var trendDict = new Dictionary<string, int>();
            for(int i = 1; i <= daysInMonth; i++)
            {
                trendDict[i.ToString("00")] = 0;
            }
            foreach(var late in lateAttendances)
            {
                string dayKey = late.CheckIn.Day.ToString("00");
                trendDict[dayKey]++;
            }
            foreach(var kvp in trendDict)
            {
                vm.DailyTrend.Add(new DailyLateTrendDto { DateLabel = kvp.Key, LateOccurrences = kvp.Value });
            }

            var employeesWithLate = employeesList.Where(e => lateAttendances.Any(a => a.EmployeeId == e.Id)).ToList();
            foreach(var emp in employeesWithLate)
            {
                var empLates = lateAttendances.Where(a => a.EmployeeId == emp.Id).ToList();
                var totalMins = empLates.Sum(a => a.LateMinutes);
                
                vm.EmployeeSummaries.Add(new LateEmployeeSummaryDto
                {
                    EmployeeId = emp.Id,
                    EmployeeCode = emp.EmployeeCode ?? "",
                    EmployeeName = emp.Name ?? "",
                    DepartmentName = emp.Department ?? "",
                    LateCount = empLates.Count,
                    TotalLateMinutes = totalMins,
                    AverageLateMinutes = empLates.Count > 0 ? Math.Round((double)totalMins / empLates.Count, 1) : 0,
                    LatestLateDate = empLates.OrderByDescending(a => a.CheckIn).FirstOrDefault()?.CheckIn
                });
            }

            vm.EmployeeSummaries = vm.EmployeeSummaries.OrderByDescending(x => x.LateCount).ToList();

            ViewBag.PeriodStatus = result.PeriodStatus;
            return View(vm);
        }

        [HttpGet]
        public IActionResult GetLateDetails(int employeeId, string month)
        {
            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits();
            var emp = _context.Employees.FirstOrDefault(e => e.Id == employeeId
                && !string.IsNullOrEmpty(e.Department)
                && allowedDepts.Contains(e.Department));
            if(emp == null) return NotFound();

            int m = DateTime.Now.Month, y = DateTime.Now.Year;
            var parts = month.Split('/');
            if (parts.Length == 2)
            {
                int.TryParse(parts[0], out m);
                int.TryParse(parts[1], out y);
            }
            int daysInMonth = DateTime.DaysInMonth(y, m);
            DateTime startDate = new DateTime(y, m, 1);
            DateTime endDate = new DateTime(y, m, daysInMonth, 23, 59, 59);

            var lates = _context.Attendances
                .Where(a => a.EmployeeId == emp.Id && a.CheckIn >= startDate && a.CheckIn <= endDate && a.LateMinutes > 0)
                .OrderByDescending(a => a.CheckIn)
                .Select(a => new LateOccurrenceDto
                {
                    AttendanceId = a.Id,
                    Date = a.CheckIn.Date,
                    ScheduledStart = new TimeSpan(8, 0, 0),
                    ActualCheckIn = a.CheckIn,
                    LateMinutes = a.LateMinutes,
                    IsAdjusted = a.AdjustmentStatus == "APPLIED" || a.AdjustmentStatus == "APPROVED"
                })
                .ToList();

            return Json(lates);
        }
    }
}
