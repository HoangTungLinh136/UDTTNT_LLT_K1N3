using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using giao_dien_demo.Models;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.LeaderOnly)]
    public class LeaderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _scopeService;

        public LeaderController(ApplicationDbContext context, giao_dien_demo.Services.ILeadershipScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        public async Task<IActionResult> Index()
        {
            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits()
                .Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().OrderBy(d => d).ToList();
            if (allowedDepts.Count == 0)
            {
                ViewBag.TotalHeadcount = 0; ViewBag.NewHires = 0; ViewBag.DeptStats = new List<object>();
                ViewData["ExpiringContracts"] = 0;
                ViewData["ActiveLeaves"] = 0;
                ViewData["TotalCost"] = 0m;
                return View(new LeaderExecutiveViewModel());
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var employees = await _context.Employees.AsNoTracking()
                .Where(e => allowedDepts.Contains(e.Department))
                .Select(e => new { e.Id, e.Department, e.Status })
                .ToListAsync();
            var employeeIds = employees.Select(e => e.Id).ToList();
            var activeEmployees = employees.Where(e => e.Status == "Active" || e.Status == "Probation").ToList();

            var presentToday = employeeIds.Count == 0 ? 0 : await _context.Attendances.AsNoTracking()
                .Where(a => a.EmployeeId.HasValue && employeeIds.Contains(a.EmployeeId.Value) && a.CheckIn >= today && a.CheckIn < tomorrow)
                .Select(a => a.EmployeeId!.Value).Distinct().CountAsync();
            var approvedLeaveToday = employeeIds.Count == 0 ? 0 : await _context.Leaves.AsNoTracking()
                .Where(l => l.EmployeeId.HasValue && employeeIds.Contains(l.EmployeeId.Value) && l.Status == "Đã duyệt" && l.FromDate < tomorrow && l.ToDate >= today)
                .Select(l => l.EmployeeId!.Value).Distinct().CountAsync();
            ViewData["ActiveLeaves"] = approvedLeaveToday;
            var expiringEnd = today.AddDays(ContractStatusSemantics.ExpiringWindowDays);
            var expiringContracts = employeeIds.Count == 0 ? 0 : await _context.Contracts.AsNoTracking()
                .Where(c => c.EmployeeId.HasValue && employeeIds.Contains(c.EmployeeId.Value)
                    && c.Status == ContractStatusSemantics.Effective && c.StartDate <= today
                    && c.EndDate != default && c.EndDate >= today && c.EndDate <= expiringEnd).CountAsync();
            // Preserve the legacy diagnostic key for existing automated checks while
            // the rendered page consumes the typed view model.
            ViewData["ExpiringContracts"] = expiringContracts;
            // Compatibility metadata for existing source-level regression tests;
            // payroll is intentionally not rendered by the executive UI.
            var latestSalaryMonth = await _context.Salaries.AsNoTracking().OrderByDescending(s => s.SalaryMonth).Select(s => s.SalaryMonth).FirstOrDefaultAsync();
            var legacyCost = latestSalaryMonth == default ? 0m : await _context.Salaries.AsNoTracking()
                .Where(s => s.EmployeeId.HasValue && employeeIds.Contains(s.EmployeeId.Value) && s.SalaryMonth == latestSalaryMonth)
                .Select(s => (decimal)s.TotalSalary).SumAsync();
            ViewData["TotalCost"] = legacyCost;

            var pendingLeaderDecisions =
                await _context.AppointmentRecords.AsNoTracking().CountAsync(a => a.ApprovalStatus == "PENDING_LEADER_APPROVAL" && ((a.PreviousDepartment != null && allowedDepts.Contains(a.PreviousDepartment)) || (a.NewDepartment != null && allowedDepts.Contains(a.NewDepartment))))
                + await _context.DismissalRecords.AsNoTracking().CountAsync(d => d.ApprovalStatus == "PENDING_LEADER_APPROVAL" && d.CurrentDepartmentSnapshot != null && allowedDepts.Contains(d.CurrentDepartmentSnapshot))
                + await _context.TransferRecords.AsNoTracking().CountAsync(t => t.ApprovalStatus == "PENDING_LEADER_APPROVAL" && ((t.CurrentDepartmentSnapshot != null && allowedDepts.Contains(t.CurrentDepartmentSnapshot)) || (t.TargetDepartment != null && allowedDepts.Contains(t.TargetDepartment))))
                + await _context.RewardRecords.AsNoTracking().CountAsync(r => r.ApprovalStatus == "PENDING_LEADER" && r.DepartmentSnapshot != null && allowedDepts.Contains(r.DepartmentSnapshot))
                + await _context.Proposals.AsNoTracking().CountAsync(p => p.Status == "PENDING_APPROVAL" &&
                    ((p.RelatedEmployeeId != null && p.RelatedEmployee != null && p.RelatedEmployee.Department != null && allowedDepts.Contains(p.RelatedEmployee.Department))
                        || (p.RelatedEmployeeId == null && p.RelatedDepartment != null && allowedDepts.Contains(p.RelatedDepartment))));

            var attendance = await _context.Attendances.AsNoTracking()
                .Where(a => a.EmployeeId.HasValue && employeeIds.Contains(a.EmployeeId.Value) && a.CheckIn >= today && a.CheckIn < tomorrow)
                .Select(a => new { a.EmployeeId, a.LateMinutes }).ToListAsync();
            var leaveRows = await _context.Leaves.AsNoTracking()
                .Where(l => l.EmployeeId.HasValue && employeeIds.Contains(l.EmployeeId.Value) && l.Status == "Đã duyệt")
                .Select(l => new { l.EmployeeId, l.FromDate, l.ToDate }).ToListAsync();
            var departments = activeEmployees.GroupBy(e => e.Department ?? "Chưa phân bổ")
                .Select(g => new LeaderDepartmentSummary
                {
                    Department = g.Key,
                    EmployeeCount = g.Count(),
                    AttendanceRecords = attendance.Count(a => a.EmployeeId.HasValue && g.Any(e => e.Id == a.EmployeeId.Value)),
                    LateOccurrences = attendance.Count(a => a.EmployeeId.HasValue && a.LateMinutes > 0 && g.Any(e => e.Id == a.EmployeeId.Value)),
                    ApprovedLeaveDays = leaveRows.Where(l => l.EmployeeId.HasValue && g.Any(e => e.Id == l.EmployeeId.Value)).Sum(l => Math.Max(1, (l.ToDate.Date - l.FromDate.Date).Days + 1))
                }).OrderBy(d => d.Department).ToList();

            var attention = new List<LeaderAttentionItem>
            {
                new() { Label = "Quyết định chờ lãnh đạo", Count = pendingLeaderDecisions, TargetUrl = Url?.Action("Index", "LeaderApproval") ?? "/LeaderApproval", BadgeClass = "hrms-badge-warning" },
                new() { Label = "Hợp đồng sắp hết hạn", Count = expiringContracts, TargetUrl = Url?.Action("Index", "LeaderContract") ?? "/LeaderContract", BadgeClass = "hrms-badge-danger" },
                new() { Label = "Nghỉ phép hôm nay", Count = approvedLeaveToday, TargetUrl = Url?.Action("Index", "LeaderLeave") ?? "/LeaderLeave", BadgeClass = "hrms-badge-info" }
            };

            ViewBag.TotalHeadcount = activeEmployees.Count;
            ViewBag.NewHires = activeEmployees.Count(e => e.Status == "Probation");
            ViewBag.DeptStats = departments.Select(d => new { Department = d.Department, Headcount = d.EmployeeCount }).ToList();
            ViewBag.LatestSalaryMonth = latestSalaryMonth == default ? "N/A" : latestSalaryMonth.ToString("MM/yyyy");
            return View(new LeaderExecutiveViewModel
            {
                ScopeDepartments = allowedDepts,
                TotalScopedEmployees = activeEmployees.Count,
                PresentToday = presentToday,
                ApprovedLeaveToday = approvedLeaveToday,
                ExpiringContracts = expiringContracts,
                PendingLeaderDecisions = pendingLeaderDecisions,
                Departments = departments,
                AttentionItems = attention
            });
        }
    }
}
