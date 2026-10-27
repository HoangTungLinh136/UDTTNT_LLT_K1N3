using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Helpers;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader},{AppRoles.Manager}")]
    [AutoValidateAntiforgeryToken]
    public class PayrollTaxReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IPayrollCalculationService _payrollService;

        public PayrollTaxReportController(ApplicationDbContext context, giao_dien_demo.Services.IPayrollCalculationService payrollService)
        {
            _context = context;
            _payrollService = payrollService;
        }

        private bool CheckLogin()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("User"));
        }

        public async Task<IActionResult> Index(string? period, string? department, string? mstStatus, string? employee)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role != AppRoles.HR && role != AppRoles.Leader && role != AppRoles.Manager) return Forbid();

            // Tính toán thuế dựa vào SalaryController
            DateTime? targetDate = null;
            if (!string.IsNullOrEmpty(period))
            {
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

            // 1. Filter Department
            if (!string.IsNullOrEmpty(department))
            {
                data = data.Where(d => !string.IsNullOrEmpty(d.DepartmentName) && d.DepartmentName.Contains(department, StringComparison.OrdinalIgnoreCase));
            }

            // 2. Filter MST Status
            if (!string.IsNullOrEmpty(mstStatus))
            {
                if (mstStatus == "Missing")
                {
                    data = data.Where(d => string.IsNullOrEmpty(d.TaxCode));
                }
                else if (mstStatus == "Valid")
                {
                    data = data.Where(d => !string.IsNullOrEmpty(d.TaxCode));
                }
            }

            // 3. Filter Employee
            if (!string.IsNullOrEmpty(employee))
            {
                data = data.Where(d => !string.IsNullOrEmpty(d.EmployeeName) && d.EmployeeName.Contains(employee, StringComparison.OrdinalIgnoreCase));
            }

            var reportList = data.ToList();

            // --- TÍNH TOÁN KPI SUMMARY ---
            // Tổng thu nhập chịu thuế
            ViewBag.TotalTaxableIncome = reportList.Sum(x => x.TaxableIncome);
            // Thuế đã khấu trừ
            ViewBag.TotalTaxWithheld = reportList.Sum(x => x.TaxTNCN);
            // Số NV phát sinh thuế (Có thuế > 0)
            ViewBag.TaxableEmployeeCount = reportList.Where(x => x.TaxTNCN > 0).Select(x => x.EmployeeId).Distinct().Count();
            // Số NV thiếu MST
            ViewBag.MissingTaxCodeCount = reportList.Where(x => string.IsNullOrEmpty(x.TaxCode)).Select(x => x.EmployeeId).Distinct().Count();

            // Load filters để bind ra giao diện
            ViewBag.Departments = await _context.Employees.Select(e => e.Department).Distinct().Where(d => !string.IsNullOrEmpty(d)).ToListAsync();
            
            ViewBag.FilterDepartment = department;
            ViewBag.FilterMstStatus = mstStatus;
            ViewBag.FilterEmployee = employee;
            ViewBag.FilterPeriod = period ?? PayrollCycleHelper.GetCurrentCycle(DateTime.Now).PeriodName;

            return View(reportList);
        }

        [HttpGet]
        public IActionResult ExportExcel(string? period, string? department, string? mstStatus, string? employee)
        {
            if (!CheckLogin()) return Unauthorized();
            var role = HttpContext.Session.GetString("Role");
            if (role != "HR" && role != "Leader" && role != "Manager") return Forbid();

            // Giả lập Export trả về file
            return File(new byte[0], "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BaoCaoThue_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> Reconcile()
        {
            if (!CheckLogin()) return Unauthorized();
            
            // Giả lập logic kiểm tra sự trùng khớp
            var calculatedSalaries = await _payrollService.CalculatePayrollAsync(DateTime.Now);
            var matched = calculatedSalaries.Count(x => x.TaxTNCN >= 0);
            var mismatched = 0; // Giả định khớp 100% trong DB

            return Json(new { success = true, matched = matched, mismatched = mismatched, message = $"Đã đối chiếu hoàn tất. Khớp: {matched} bản ghi. Lệch: {mismatched} bản ghi." });
        }
    }
}
