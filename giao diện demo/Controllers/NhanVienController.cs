using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = "Employee")]
    public class NhanVienController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NhanVienController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
            if (currentUser?.EmployeeId == null) return Forbid();
            var employeeId = currentUser.EmployeeId.Value;

            var employee = await _context.Employees
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return Forbid();

            var now = DateTime.Now;
            var todayStart = now.Date;
            var tomorrow = todayStart.AddDays(1);
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var nextMonth = monthStart.AddMonths(1);

            var attendanceQuery = _context.Attendances
                .AsNoTracking()
                .Where(a => a.EmployeeId == employeeId);
            var todayAttendance = await attendanceQuery
                .Where(a => a.CheckIn >= todayStart && a.CheckIn < tomorrow)
                .OrderByDescending(a => a.CheckIn)
                .Select(a => new EmployeeDashboardAttendanceRow
                {
                    CheckIn = a.CheckIn,
                    CheckOut = a.CheckOut
                })
                .FirstOrDefaultAsync();
            var attendanceCountThisMonth = await attendanceQuery
                .CountAsync(a => a.CheckIn >= monthStart && a.CheckIn < nextMonth);
            var recentAttendance = await attendanceQuery
                .OrderByDescending(a => a.CheckIn)
                .Take(8)
                .Select(a => new EmployeeDashboardAttendanceRow
                {
                    CheckIn = a.CheckIn,
                    CheckOut = a.CheckOut
                })
                .ToListAsync();

            var pendingLeaveCount = await _context.Leaves
                .AsNoTracking()
                .CountAsync(l => l.EmployeeId == employeeId &&
                    (l.Status == "Chờ duyệt" || l.Status == "PENDING"));

            var contractQuery = _context.Contracts
                .AsNoTracking()
                .Where(c => c.EmployeeId == employeeId);
            var contractCount = await contractQuery.CountAsync();
            var contractsAwaitingSignature = await contractQuery
                .CountAsync(c => c.Status == "Chờ nhân viên ký");

            var unreadNotificationCount = await _context.Notifications
                .AsNoTracking()
                .CountAsync(n => n.RecipientUserId == currentUser.Id && !n.IsRead);

            var recentNotifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == currentUser.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new EmployeeDashboardNotificationItem
                {
                    Id = n.Id,
                    Message = n.Message ?? string.Empty,
                    CreatedAt = n.CreatedAt,
                    NotificationType = n.NotificationType,
                    IsRead = n.IsRead,
                    SenderName = n.SenderName
                })
                .ToListAsync();

            WorkShift? todayShift = null;
            try
            {
                var shiftAssignments = await _context.WorkShiftAssignments
                    .AsNoTracking()
                    .Include(a => a.WorkShift)
                    .Where(a => (!a.EffectiveFrom.HasValue || a.EffectiveFrom.Value.Date <= todayStart) &&
                                (!a.EffectiveTo.HasValue || a.EffectiveTo.Value.Date >= todayStart))
                    .ToListAsync();

                todayShift = shiftAssignments.FirstOrDefault(a => a.TargetType == "Employee" && a.TargetValue == employee.EmployeeCode)?.WorkShift
                          ?? shiftAssignments.FirstOrDefault(a => a.TargetType == "Department" && a.TargetValue == employee.Department)?.WorkShift
                          ?? shiftAssignments.FirstOrDefault(a => a.TargetType == "Company")?.WorkShift;
            }
            catch { }

            var jobTitle = !string.IsNullOrWhiteSpace(employee.JobTitle) ? employee.JobTitle : (employee.Position ?? "Nhân viên");
            var displayStatus = employee.Status switch
            {
                "Active" or "Đang làm việc" or "Hoạt động" => "Đang làm việc",
                "Probation" or "Thử việc" => "Thử việc",
                "Resigned" or "Đã nghỉ việc" => "Đã nghỉ việc",
                _ => employee.Status ?? "Đang làm việc"
            };

            var trainingQuery = _context.CourseEnrollments
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId);
            var trainingInProgressCount = await trainingQuery.CountAsync(e => e.ProgressStatus == "InProgress");
            var trainingCompletedCount = await trainingQuery.CountAsync(e => e.ProgressStatus == "Completed");
            var trainingCertificateCount = await _context.TrainingCertificates
                .AsNoTracking()
                .CountAsync(c => c.EmployeeId == employeeId);

            var rewardCount = await _context.RewardRecords
                .AsNoTracking()
                .CountAsync(r => r.EmployeeId == employeeId &&
                    (r.ApprovalStatus == "APPROVED" || r.ApprovalStatus == "ISSUED"));
            var activeBenefitCount = await _context.BenefitEntitlements
                .AsNoTracking()
                .CountAsync(b => b.EmployeeId == employeeId && b.Status == "Active");

            var latestKpi = await _context.KPIEvaluations
                .AsNoTracking()
                .Include(e => e.Period)
                .Where(e => e.EmployeeId == employeeId &&
                    (e.Status == "ACTIVE" || e.Status == "EVALUATING" || e.Status == "COMPLETED"))
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new
                {
                    Period = e.Period != null ? e.Period.Name : null,
                    e.Status,
                    e.FinalScore
                })
                .FirstOrDefaultAsync();

            var model = new EmployeeDashboardViewModel
            {
                EmployeeName = employee.Name ?? string.Empty,
                EmployeeCode = employee.EmployeeCode ?? string.Empty,
                Department = employee.Department ?? string.Empty,
                Position = string.IsNullOrWhiteSpace(employee.Position) ? employee.JobTitle ?? string.Empty : employee.Position,
                JobTitle = jobTitle,
                EmploymentStatus = displayStatus,
                AvatarPath = employee.AvatarPath,
                StandardWorkDaysThisMonth = 26,
                Today = todayStart,
                TodayAttendance = todayAttendance,
                TodayShift = todayShift,
                RecentNotifications = recentNotifications,
                AttendanceCountThisMonth = attendanceCountThisMonth,
                PendingLeaveCount = pendingLeaveCount,
                ContractCount = contractCount,
                ContractsAwaitingSignature = contractsAwaitingSignature,
                UnreadNotificationCount = unreadNotificationCount,
                TrainingInProgressCount = trainingInProgressCount,
                TrainingCompletedCount = trainingCompletedCount,
                TrainingCertificateCount = trainingCertificateCount,
                RewardCount = rewardCount,
                ActiveBenefitCount = activeBenefitCount,
                LatestKpiPeriod = latestKpi?.Period,
                LatestKpiStatus = latestKpi?.Status,
                LatestKpiScore = latestKpi?.FinalScore,
                RecentAttendances = recentAttendance
            };

            ViewBag.CurrentUsername = currentUser.Username;
            ViewBag.MyRealName = model.EmployeeName;
            ViewBag.CurrentRole = currentUser.Role;
            ViewBag.MyContractCount = model.ContractCount;
            ViewBag.MyAttendanceCount = model.AttendanceCountThisMonth;
            ViewBag.MyAttendances = await attendanceQuery.OrderByDescending(a => a.CheckIn).Take(8).ToListAsync();
            ViewBag.MyLeaveCount = model.PendingLeaveCount;
            ViewBag.NotificationCount = model.UnreadNotificationCount;
            return View(model);
        }
    }
}
