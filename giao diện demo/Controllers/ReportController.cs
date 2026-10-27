using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace giao_dien_demo.Controllers;

[Authorize(Policy = AppPolicies.HROnly)]
public class ReportController : Controller
{
    private readonly ApplicationDbContext _context;
    public ReportController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? search, string? department, DateTime? from, DateTime? to, int page = 1)
    {
        page = Math.Max(1, page); const int pageSize = 50;
        var data = await BuildRows(search, department, from, to, page, pageSize);
        ViewBag.Search = search; ViewBag.Department = department; ViewBag.From = from; ViewBag.To = to;
        ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.Total = data.Total;
        ViewBag.Departments = await _context.Employees.Where(e => e.Department != null).Select(e => e.Department!).Distinct().OrderBy(x => x).ToListAsync();
        return View(data.Rows);
    }

    [HttpGet]
    public async Task<IActionResult> Export(string? search, string? department, DateTime? from, DateTime? to)
    {
        var data = await BuildRows(search, department, from, to, 1, int.MaxValue);
        var sb = new StringBuilder("Mã NV,Họ tên,Phòng ban,Khen thưởng,Chuyên cần,Nghỉ phép,KPI\r\n");
        foreach (var row in data.Rows) sb.AppendLine(string.Join(',', Csv(row.EmployeeCode), Csv(row.EmployeeName), Csv(row.Department), Csv(row.Reward), Csv(row.DiligenceBonus), row.LeaveDays, Csv(row.TaskStatus)));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv; charset=utf-8", $"BaoCaoNhanSu_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private async Task<(List<Report> Rows, int Total)> BuildRows(string? search, string? department, DateTime? from, DateTime? to, int page, int pageSize)
    {
        var employees = _context.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); employees = employees.Where(e => (e.Name != null && e.Name.Contains(s)) || (e.EmployeeCode != null && e.EmployeeCode.Contains(s))); }
        if (!string.IsNullOrWhiteSpace(department)) employees = employees.Where(e => e.Department == department);
        var total = await employees.CountAsync();
        var selected = await employees.OrderBy(e => e.EmployeeCode).Skip((page - 1) * pageSize).Take(pageSize).Select(e => new { e.Id, e.EmployeeCode, e.Name, e.Department, e.KPIScore }).ToListAsync();
        var ids = selected.Select(e => e.Id).ToList();
        var salaries = await _context.Salaries.AsNoTracking().Where(s => s.EmployeeId.HasValue && ids.Contains(s.EmployeeId.Value) && (!from.HasValue || s.SalaryMonth >= from.Value) && (!to.HasValue || s.SalaryMonth <= to.Value)).GroupBy(s => s.EmployeeId).Select(g => g.OrderByDescending(x => x.SalaryMonth).First()).ToListAsync();
        // Fetch only the current page's leave rows; calculate date spans in managed code
        // because SQL Server providers cannot translate DateTime subtraction reliably.
        var leaveRows = await _context.Leaves.AsNoTracking().Where(l => l.EmployeeId.HasValue && ids.Contains(l.EmployeeId.Value) && l.Status == "Đã duyệt" && (!from.HasValue || l.FromDate >= from.Value) && (!to.HasValue || l.ToDate <= to.Value)).Select(l => new { l.EmployeeId, l.FromDate, l.ToDate, l.LeaveType }).ToListAsync();
        var leaves = leaveRows.GroupBy(l => l.EmployeeId).Select(g => new { EmployeeId = g.Key, Days = g.Sum(x => (x.ToDate - x.FromDate).Days + 1), Type = g.Select(x => x.LeaveType).FirstOrDefault() }).ToList();
        var rows = selected.Select(e => { var sal = salaries.FirstOrDefault(x => x.EmployeeId == e.Id); var leave = leaves.FirstOrDefault(x => x.EmployeeId == e.Id); return new Report { Id = e.Id, EmployeeCode = e.EmployeeCode, EmployeeName = e.Name ?? "", Department = e.Department ?? "", HolidayBonus = sal?.HolidayBonus.ToString("N0", CultureInfo.InvariantCulture) ?? "0", DiligenceBonus = sal?.DiligenceBonus.ToString("N0", CultureInfo.InvariantCulture) ?? "0", Reward = sal?.Reward.ToString("N0", CultureInfo.InvariantCulture) ?? "0", Discipline = sal?.Discipline.ToString("N0", CultureInfo.InvariantCulture) ?? "0", LeaveDays = leave?.Days ?? 0, LeaveType = leave?.Type ?? "-", TaskStatus = e.KPIScore ?? "N/A" }; }).ToList();
        return (rows, total);
    }
}
