using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Authorization;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Helpers;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader},{AppRoles.Manager}")]
    [AutoValidateAntiforgeryToken]
    public class PayrollCostReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IPayrollCalculationService _payrollService;

        public PayrollCostReportController(ApplicationDbContext context, giao_dien_demo.Services.IPayrollCalculationService payrollService)
        {
            _context = context;
            _payrollService = payrollService;
        }

        private bool CheckLogin()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("User"));
        }

        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string? department, string? employee, string? period)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role != "HR" && role != "Leader" && role != "Manager") return Forbid();

            // Lấy dữ liệu lương thông qua Service tính toán gốc để lấy dữ liệu thực tế hiện tại
            // Có thể dùng fromDate nếu có logic kỳ theo khoảng ngày, ở đây tạm dùng mặc định/hiện tại
            DateTime? targetDate = null;
            if (!string.IsNullOrEmpty(period))
            {
                // Giả định period = MM/yyyy
                if (DateTime.TryParseExact("01/" + period, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    targetDate = parsedDate;
                }
            }

            var calculatedSalaries = await _payrollService.CalculatePayrollAsync(targetDate ?? DateTime.Now);

            // Bắt đầu filter từ bảng lương
            var data = calculatedSalaries.AsEnumerable();

            if (role == AppRoles.Manager)
            {
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                var allowedDepts = managerScope.GetManagedOrganizationalUnits();
                data = data.Where(d => d.DepartmentName != null && allowedDepts.Contains(d.DepartmentName));
            }
            else if (role == AppRoles.Leader)
            {
                var leaderScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                var allowedDepts = leaderScope.GetOverseenOrganizationalUnits();
                data = data.Where(d => d.DepartmentName != null && allowedDepts.Contains(d.DepartmentName));
            }

            if (!string.IsNullOrEmpty(department))
            {
                data = data.Where(d => !string.IsNullOrEmpty(d.DepartmentName) && d.DepartmentName.Contains(department, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(employee))
            {
                data = data.Where(d => !string.IsNullOrEmpty(d.EmployeeName) && d.EmployeeName.Contains(employee, StringComparison.OrdinalIgnoreCase));
            }

            // Tùy chọn: Filter fromDate / toDate theo SalaryMonth
            if (fromDate.HasValue)
            {
                data = data.Where(d => d.SalaryMonth >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                data = data.Where(d => d.SalaryMonth <= toDate.Value);
            }

            var reportList = data.ToList();

            // --- TÍNH TOÁN KPI SUMMARY ---
            ViewBag.TotalCost = reportList.Sum(x => x.TotalSalary);
            ViewBag.TotalPerformanceSalary = reportList.Sum(x => x.PerformanceSalary);
            ViewBag.TotalOT = reportList.Sum(x => x.OvertimeCost);
            ViewBag.TotalAllowance = reportList.Sum(x => x.AllowanceCost);
            ViewBag.TotalBonus = reportList.Sum(x => x.Reward);

            // --- TÍNH TOÁN DỮ LIỆU DEPARTMENT TABLE & CHART ---
            var groupedByDept = reportList
                .GroupBy(x => string.IsNullOrEmpty(x.DepartmentName) ? "Chưa phân bổ" : x.DepartmentName)
                .Select(g => new
                {
                    DepartmentName = g.Key,
                    EmployeeCount = g.Select(e => e.EmployeeId).Distinct().Count(),
                    SalaryCost = g.Sum(x => x.PerformanceSalary),
                    OvertimeCost = g.Sum(x => x.OvertimeCost),
                    AllowanceCost = g.Sum(x => x.AllowanceCost),
                    BonusCost = g.Sum(x => x.Reward),
                    TotalCost = g.Sum(x => x.TotalSalary)
                })
                .OrderByDescending(x => x.TotalCost)
                .ToList();

            ViewBag.DepartmentData = groupedByDept;

            // Load filters để bind ra giao diện
            ViewBag.Departments = await _context.Employees.Select(e => e.Department).Distinct().Where(d => !string.IsNullOrEmpty(d)).ToListAsync();
            
            // Giữ lại trạng thái bộ lọc
            ViewBag.FilterFromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.FilterToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.FilterDepartment = department;
            ViewBag.FilterEmployee = employee;
            ViewBag.FilterPeriod = period ?? PayrollCycleHelper.GetCurrentCycle(DateTime.Now).PeriodName;

            return View(reportList);
        }

        [HttpGet]
        public IActionResult ExportExcel(DateTime? fromDate, DateTime? toDate, string? department, string? employee, string? period)
        {
            if (!CheckLogin()) return Unauthorized();
            var role = HttpContext.Session.GetString("Role");
            if (role != "HR" && role != "Leader" && role != "Manager") return Forbid();

            // Logic giống Index nhưng dùng thư viện như EPPlus để ghi ra Excel.
            // Ở đây mock trả về file theo chuẩn bài toán không có module thừa.
            return File(new byte[0], "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BaoCaoChiPhi_{DateTime.Now:yyyyMMdd}.xlsx");
        }
    }
}
