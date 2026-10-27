using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;
using giao_dien_demo.Services;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    public class TimesheetController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IManagerScopeService _managerScope;
        private readonly ILeadershipScopeService _leadershipScope;

        public TimesheetController(
            ApplicationDbContext context,
            IManagerScopeService managerScope,
            ILeadershipScopeService leadershipScope)
        {
            _context = context;
            _managerScope = managerScope;
            _leadershipScope = leadershipScope;
        }

        private async Task<IQueryable<Employee>?> GetScopedEmployeeQueryAsync()
        {
            var role = CurrentActor.Role(User);
            if (role == AppRoles.HR)
            {
                return _context.Employees.AsQueryable();
            }
            if (role == AppRoles.Manager)
            {
                return _managerScope.GetScopedEmployeeQuery();
            }
            if (role == AppRoles.Leader)
            {
                return _leadershipScope.GetScopedEmployeeQuery();
            }
            if (role == AppRoles.Employee)
            {
                var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
                if (currentUser?.EmployeeId == null) return null;
                return _context.Employees.Where(e => e.Id == currentUser.EmployeeId.Value);
            }
            return null; // Admin or unmapped roles have no business attendance scope
        }

        // 1. Giao diện chính của Bảng chấm công
        public async Task<IActionResult> Index()
        {
            var scopedQuery = await GetScopedEmployeeQueryAsync();
            if (scopedQuery == null) return Forbid();

            var role = CurrentActor.Role(User);
            ViewBag.Role = role;
            ViewBag.RealName = CurrentActor.Username(User);

            // Tải danh sách phòng ban trong scope cho Filter
            var departments = await scopedQuery
                .Where(e => !string.IsNullOrEmpty(e.Department))
                .Select(e => e.Department!)
                .Distinct()
                .ToListAsync();
            ViewBag.Departments = departments;

            return View();
        }

        // 2. Lấy dữ liệu thống kê
        [HttpGet]
        public async Task<IActionResult> GetStatistics(int year, int month, string department = "")
        {
            var queryEmp = await GetScopedEmployeeQueryAsync();
            if (queryEmp == null) return Forbid();

            if (!string.IsNullOrEmpty(department))
                queryEmp = queryEmp.Where(e => e.Department == department);

            var employees = await queryEmp.ToListAsync();
            var employeeIds = employees.Select(e => e.Id).ToList();

            var attendances = await _context.Attendances
                .Where(a => a.CheckIn.Year == year && a.CheckIn.Month == month && a.EmployeeId.HasValue && employeeIds.Contains(a.EmployeeId.Value))
                .ToListAsync();

            int totalEmployees = employees.Count;
            // Giả định: Đủ công nếu có >= 20 ngày đi làm (tuỳ logic thực tế)
            int fullAttendance = employees.Count(e => attendances.Count(a => a.EmployeeId == e.Id) >= 20);
            int abnormal = attendances.Count(a => (a.LateMinutes > 0) || (a.CheckOut == null) || (a.StatusNote != null && a.StatusNote.Contains("Bất thường")));
            
            var period = await _context.AttendancePeriods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month);
            string periodStatus = period != null ? period.Status : "OPEN";

            return Json(new {
                totalEmployees = totalEmployees,
                fullAttendance = fullAttendance,
                abnormal = abnormal,
                periodStatus = periodStatus
            });
        }

        // 3. Dữ liệu ma trận
        [HttpGet]
        public async Task<IActionResult> GetTimesheetMatrix(int year, int month, string department = "", string employeeName = "", string status = "")
        {
            var queryEmp = await GetScopedEmployeeQueryAsync();
            if (queryEmp == null) return Forbid();

            if (!string.IsNullOrEmpty(department))
                queryEmp = queryEmp.Where(e => e.Department == department);
            
            if (!string.IsNullOrEmpty(employeeName))
                queryEmp = queryEmp.Where(e => e.Name != null && e.Name.Contains(employeeName));

            var employees = await queryEmp.OrderBy(e => e.Name).ToListAsync();
            var employeeIds = employees.Select(e => e.Id).ToList();

            var attendances = await _context.Attendances
                .Where(a => a.CheckIn.Year == year && a.CheckIn.Month == month && a.EmployeeId.HasValue && employeeIds.Contains(a.EmployeeId.Value))
                .ToListAsync();

            var leaves = await _context.Leaves
                .Where(l => l.Status == "Đã duyệt" && ((l.FromDate.Year == year && l.FromDate.Month == month) || (l.ToDate.Year == year && l.ToDate.Month == month)))
                .ToListAsync();

            var result = new List<object>();

            int daysInMonth = DateTime.DaysInMonth(year, month);

            foreach (var emp in employees)
            {
                var empAttendances = attendances.Where(a => a.EmployeeId == emp.Id).ToList();
                var empLeaves = leaves.Where(l => l.EmployeeId == emp.Id).ToList();
                
                var dailyData = new Dictionary<int, object>();
                double totalWorkDays = 0;
                double totalOT = 0;
                bool hasAbnormal = false;

                for (int day = 1; day <= daysInMonth; day++)
                {
                    DateTime currentDate = new DateTime(year, month, day);
                    var att = empAttendances.FirstOrDefault(a => a.CheckIn.Date == currentDate);
                    var onLeave = empLeaves.Any(l => currentDate >= l.FromDate.Date && currentDate <= l.ToDate.Date);

                    if (onLeave)
                    {
                        dailyData[day] = new { status = "LEAVE", text = "Nghỉ phép" };
                    }
                    else if (att != null)
                    {
                        string sType = "PRESENT";
                        if (att.LateMinutes > 0) sType = "LATE";
                        if (att.CheckOut == null) sType = "MISSING_OUT";
                        if (att.StatusNote != null && att.StatusNote.Contains("Bất thường")) { sType = "ABNORMAL"; hasAbnormal = true; }

                        dailyData[day] = new { 
                            status = sType, 
                            text = att.StatusNote, 
                            checkIn = att.CheckIn.ToString("HH:mm"), 
                            checkOut = att.CheckOut?.ToString("HH:mm") ?? "--:--",
                            late = att.LateMinutes
                        };

                        totalWorkDays += (att.StandardHours >= 4 ? (att.StandardHours >= 8 ? 1 : 0.5) : 0);
                        totalOT += att.OvertimeHours;
                    }
                    else
                    {
                        if (currentDate.DayOfWeek == DayOfWeek.Sunday || currentDate.DayOfWeek == DayOfWeek.Saturday)
                        {
                            dailyData[day] = new { status = "OFF", text = "Nghỉ cuối tuần" };
                        }
                        else
                        {
                            if (currentDate <= DateTime.Today)
                            {
                                dailyData[day] = new { status = "ABSENT", text = "Vắng mặt" };
                                hasAbnormal = true;
                            }
                            else
                            {
                                dailyData[day] = new { status = "NONE", text = "" };
                            }
                        }
                    }
                }

                // Apply status filter
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "ABNORMAL" && !hasAbnormal) continue;
                    if (status == "FULL" && totalWorkDays < 20) continue; 
                }

                result.Add(new {
                    employeeId = emp.Id,
                    employeeCode = emp.EmployeeCode,
                    employeeName = emp.Name,
                    department = emp.Department,
                    dailyData = dailyData,
                    totalWorkDays = totalWorkDays,
                    totalOT = totalOT
                });
            }

            return Json(new { daysInMonth = daysInMonth, data = result });
        }

        // 4. Lấy chi tiết ngày (IDOR Protected)
        [HttpGet]
        public async Task<IActionResult> GetDailyDetail(int employeeId, string dateStr)
        {
            if (!DateTime.TryParse(dateStr, out DateTime date)) return BadRequest("Ngày không hợp lệ");

            var scopedQuery = await GetScopedEmployeeQueryAsync();
            if (scopedQuery == null) return Forbid();

            // Verify target employee is strictly within actor's data scope
            var targetEmp = await scopedQuery.FirstOrDefaultAsync(e => e.Id == employeeId);
            if (targetEmp == null) return Forbid();

            var att = await _context.Attendances.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.CheckIn.Date == date.Date);
            var leave = await _context.Leaves.FirstOrDefaultAsync(l => l.EmployeeId == employeeId && date.Date >= l.FromDate.Date && date.Date <= l.ToDate.Date);
            var reqs = await _context.AttendanceAdjustmentRequests
                                .Where(r => r.EmployeeCode == targetEmp.EmployeeCode && (r.RequestedCheckIn.HasValue || r.RequestedCheckOut.HasValue))
                                .OrderByDescending(r => r.CreatedAt).ToListAsync();

            return Json(new {
                attendance = att,
                leave = leave,
                requests = reqs
            });
        }

        // 5. Chốt công
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClosePeriod(int year, int month)
        {
            var period = await _context.AttendancePeriods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month);
            if (period != null && period.Status == "CLOSED")
            {
                return Json(new { success = false, message = "Kỳ công này đã được chốt từ trước." });
            }

            if (period == null)
            {
                period = new AttendancePeriod {
                    Year = year,
                    Month = month
                };
                _context.AttendancePeriods.Add(period);
            }

            period.Status = "CLOSED";
            period.LockedAt = DateTime.Now;
            period.LockedBy = CurrentActor.Username(User) ?? "HR";

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã chốt công thành công kỳ {month}/{year}" });
        }
    }
}
