using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = AppPolicies.LeaderOnly)]
    public class LeaderReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _scopeService;
        private readonly giao_dien_demo.Services.IPayrollCalculationService _payrollService;

        public LeaderReportController(ApplicationDbContext context, giao_dien_demo.Services.ILeadershipScopeService scopeService, giao_dien_demo.Services.IPayrollCalculationService payrollService) 
        { 
            _context = context; 
            _scopeService = scopeService; 
            _payrollService = payrollService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string reportType = "workforce", string? department = null, string? search = null, DateTime? from = null, DateTime? to = null, int page = 1)
        {
            var model = await BuildModel(reportType, department, search, from, to, page, 25);
            if (model.ErrorMessage == "FORBIDDEN") return Forbid();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Export(string reportType = "workforce", string? department = null, string? search = null, DateTime? from = null, DateTime? to = null)
        {
            var model = await BuildModel(reportType, department, search, from, to, 1, int.MaxValue);
            if (model.ErrorMessage == "FORBIDDEN") return Forbid();
            var csv = new StringBuilder();
            csv.AppendLine("EmployeeCode,EmployeeName,Department,Date,Metric,Status,Detail");
            foreach (var row in model.Rows)
                csv.AppendLine(string.Join(",", new[] { row.EmployeeCode, row.EmployeeName, row.Department, row.Date?.ToString("yyyy-MM-dd"), row.Metric, row.Status, row.Detail }.Select(Escape)));
            return File(Encoding.UTF8.GetBytes("\uFEFF" + csv), "text/csv", $"leader-{model.ReportType}-{DateTime.Today:yyyyMMdd}.csv");
        }

        private async Task<LeaderReportViewModel> BuildModel(string reportType, string? department, string? search, DateTime? from, DateTime? to, int page, int pageSize)
        {
            var scope = _scopeService.GetOverseenOrganizationalUnits().Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().OrderBy(d => d).ToList();
            var validTypes = new[] { "workforce", "attendance", "leave", "payroll", "kpi", "movement", "personnel_cost" };
            var normalizedType = validTypes.Contains(reportType?.ToLowerInvariant() ?? "") ? reportType.ToLowerInvariant() : "workforce";
            
            var start = (from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
            var end = (to ?? DateTime.Today).Date;
            if (end < start) end = start;
            
            var baseModel = new LeaderReportViewModel { ScopeDepartments = scope, ReportType = normalizedType, SelectedDepartment = department, Search = search, From = start, To = end, Page = Math.Max(page, 1), PageSize = pageSize };
            if (scope.Count == 0) return baseModel;
            
            var requested = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            if (requested != null && !scope.Contains(requested)) return new LeaderReportViewModel { ScopeDepartments = scope, ErrorMessage = "FORBIDDEN", ReportType = normalizedType, From = start, To = end, Page = page, PageSize = pageSize };

            var employeeQuery = _context.Employees.AsNoTracking().Where(e => scope.Contains(e.Department));
            if (requested != null) employeeQuery = employeeQuery.Where(e => e.Department == requested);
            if (!string.IsNullOrWhiteSpace(search)) employeeQuery = employeeQuery.Where(e => (e.EmployeeCode != null && e.EmployeeCode.Contains(search)) || (e.Name != null && e.Name.Contains(search)));
            var employees = await employeeQuery.Select(e => new { e.Id, e.EmployeeCode, e.Name, e.Department, e.Status }).ToListAsync();
            var ids = employees.Select(e => e.Id).ToList();
            var rows = new List<LeaderReportRow>();

            if (normalizedType == "workforce")
            {
                rows.AddRange(employees.Select(e => new LeaderReportRow { EmployeeCode = e.EmployeeCode, EmployeeName = e.Name, Department = e.Department, Date = DateTime.Today, Metric = "Nhân sự trong phạm vi", Status = e.Status ?? "", Detail = "Danh sách nhân sự hiện hành" }));
            }
            else if (normalizedType == "attendance")
            {
                var attendance = await _context.Attendances.AsNoTracking().Where(a => a.EmployeeId.HasValue && ids.Contains(a.EmployeeId.Value) && a.CheckIn >= start && a.CheckIn < end.AddDays(1)).OrderByDescending(a => a.CheckIn).Select(a => new { a.EmployeeId, a.CheckIn, a.LateMinutes, a.StatusNote }).ToListAsync();
                rows.AddRange(attendance.Join(employees, a => a.EmployeeId, e => e.Id, (a, e) => new LeaderReportRow { EmployeeCode = e.EmployeeCode, EmployeeName = e.Name, Department = e.Department, Date = a.CheckIn, Metric = "Chấm công", Status = a.LateMinutes > 0 ? "Đi muộn" : "Đúng giờ", Detail = a.StatusNote ?? $"Đi muộn {a.LateMinutes} phút" }));
            }
            else if (normalizedType == "leave")
            {
                var leaves = await _context.Leaves.AsNoTracking().Where(l => l.EmployeeId.HasValue && ids.Contains(l.EmployeeId.Value) && l.FromDate < end.AddDays(1) && l.ToDate >= start).OrderByDescending(l => l.FromDate).Select(l => new { l.EmployeeId, l.FromDate, l.ToDate, l.LeaveType, l.Reason, l.Status }).ToListAsync();
                rows.AddRange(leaves.Join(employees, l => l.EmployeeId, e => e.Id, (l, e) => new LeaderReportRow { EmployeeCode = e.EmployeeCode, EmployeeName = e.Name, Department = e.Department, Date = l.FromDate, Metric = "Nghỉ phép", Status = l.Status, Detail = $"{l.LeaveType}: {l.FromDate:dd/MM/yyyy} - {l.ToDate:dd/MM/yyyy}" }));
            }
            else if (normalizedType == "payroll" || normalizedType == "personnel_cost")
            {
                // Calculate payroll using service
                var salaries = await _payrollService.CalculatePayrollAsync(start);
                var scopedSalaries = salaries.Where(s => s.EmployeeId.HasValue && ids.Contains(s.EmployeeId.Value)).ToList();
                
                foreach (var s in scopedSalaries)
                {
                    var emp = employees.FirstOrDefault(e => e.Id == s.EmployeeId);
                    if (emp != null)
                    {
                        if (normalizedType == "payroll")
                        {
                            rows.Add(new LeaderReportRow { EmployeeCode = emp.EmployeeCode, EmployeeName = emp.Name, Department = emp.Department, Date = start, Metric = "Lương (Gross)", Status = "Đã tính", Detail = $"Gross: {s.TotalSalary:N0}, Phụ cấp: {s.BonusAndAllowance:N0}, Thực nhận: {s.NetSalary:N0}" });
                        }
                        else // personnel_cost
                        {
                            rows.Add(new LeaderReportRow { EmployeeCode = emp.EmployeeCode, EmployeeName = emp.Name, Department = emp.Department, Date = start, Metric = "Chi phí nhân sự", Status = "Phân bổ", Detail = $"Tổng chi phí: {s.TotalSalary:N0}" });
                        }
                    }
                }
            }
            else if (normalizedType == "kpi")
            {
                var kpis = await _context.KPIEvaluations.Include(k => k.Period).AsNoTracking().Where(k => ids.Contains(k.EmployeeId) && k.CreatedAt >= start && k.CreatedAt < end.AddDays(1)).ToListAsync();
                rows.AddRange(kpis.Join(employees, k => k.EmployeeId, e => e.Id, (k, e) => new LeaderReportRow { EmployeeCode = e.EmployeeCode, EmployeeName = e.Name, Department = e.Department, Date = k.CreatedAt, Metric = $"KPI ({k.Period?.Name ?? "N/A"})", Status = k.Status, Detail = $"Điểm: {k.FinalScore?.ToString("0.##") ?? "N/A"} - Xếp loại: {k.FinalRating ?? "Chưa có"}" }));
            }
            else if (normalizedType == "movement")
            {
                var transfers = await _context.TransferRecords.AsNoTracking().Where(t => ids.Contains(t.EmployeeId) && t.EffectiveDate >= start && t.EffectiveDate < end.AddDays(1)).ToListAsync();
                var offboardings = await _context.OffboardingRecords.AsNoTracking().Where(o => ids.Contains(o.EmployeeId) && o.EffectiveDate >= start && o.EffectiveDate < end.AddDays(1)).ToListAsync();
                
                rows.AddRange(transfers.Join(employees, t => t.EmployeeId, e => e.Id, (t, e) => new LeaderReportRow { EmployeeCode = e.EmployeeCode, EmployeeName = e.Name, Department = e.Department, Date = t.EffectiveDate, Metric = "Điều chuyển", Status = t.ApprovalStatus ?? "N/A", Detail = $"Từ: {t.CurrentDepartmentSnapshot} Sang: {t.TargetDepartment}" }));
                rows.AddRange(offboardings.Join(employees, o => o.EmployeeId, e => e.Id, (o, e) => new LeaderReportRow { EmployeeCode = e.EmployeeCode, EmployeeName = e.Name, Department = e.Department, Date = o.EffectiveDate, Metric = "Nghỉ việc", Status = o.ApprovalStatus ?? "N/A", Detail = o.ExitType ?? "Thôi việc" }));
            }

            var total = rows.Count;
            var pageRows = rows.Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToList();
            return new LeaderReportViewModel { ScopeDepartments = scope, ReportType = normalizedType, SelectedDepartment = requested, Search = search, From = start, To = end, Page = Math.Max(page, 1), PageSize = pageSize, TotalRows = total, Rows = pageRows };
        }

        private static string Escape(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
