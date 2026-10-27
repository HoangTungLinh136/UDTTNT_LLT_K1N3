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
    [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader},{AppRoles.Manager},{AppRoles.Employee}")]
    public class SalaryController : Controller
    {
        // Static field BangLuongDaChot removed to ensure thread-safety
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IPayrollCalculationService _payrollService;

        public SalaryController(ApplicationDbContext context, giao_dien_demo.Services.IPayrollCalculationService payrollService)
        {
            _context = context;
            _payrollService = payrollService;
        }

        private bool CheckLogin()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("User"));
        }

        // ==============================================================================
        // 🔥 HÀM TÍNH LƯƠNG: ĐÃ CẬP NHẬT TÍNH THÊM PHỤ CẤP BẰNG CẤP
        // ==============================================================================


        [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader}")]
        public async Task<IActionResult> Dashboard()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            
            var currentCycle = PayrollCycleHelper.GetCurrentCycle(DateTime.Now);
            ViewBag.CyclePeriodName = currentCycle.PeriodName;
            ViewBag.CycleStartDate = currentCycle.StartDate;
            ViewBag.CycleEndDate = currentCycle.EndDate;

            var salaries = await _payrollService.CalculatePayrollAsync(DateTime.Now);

            double totalFund = salaries.Sum(s => s.TotalSalary);
            double avgSalary = salaries.Any() ? salaries.Average(s => s.TotalSalary) : 0;
            int totalEmployees = salaries.Count;

            ViewBag.TotalFund = totalFund;
            ViewBag.AvgSalary = avgSalary;
            ViewBag.TotalEmployees = totalEmployees;

            return View();
        }

        public async Task<IActionResult> Index(string? searchName)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");

            // Chạy lệnh tính toán đồng bộ từ Database
            var calculatedSalaries = await _payrollService.CalculatePayrollAsync(DateTime.Now);

            var role = CurrentActor.Role(User);
            var currentCycle = PayrollCycleHelper.GetCurrentCycle(DateTime.Now);
            ViewBag.CycleStartDate = currentCycle.StartDate;
            ViewBag.CycleEndDate = currentCycle.EndDate;
            ViewBag.CyclePaymentDate = currentCycle.PaymentDate;
            ViewBag.CyclePeriodName = currentCycle.PeriodName;
            ViewBag.SearchName = searchName;

            // Lấy trạng thái thanh toán từ PayrollPayment (chỉ đọc, không ghi)
            var periodPayments = await _context.PayrollPayments
                .Where(p => p.PayrollPeriod == currentCycle.PeriodName)
                .ToListAsync();
            ViewBag.DaThanhToanCount = periodPayments.Count(p => p.Status == PaymentStatus.DaThanhToan);
            ViewBag.ChoThanhToanCount = periodPayments.Count(p => p.Status == PaymentStatus.ChoThanhToan || p.Status == PaymentStatus.ChuaThanhToan);
            // ===== END UI DATA =====

            if (role == "HR")
            {
                var list = calculatedSalaries.ToList();
                // Search theo tên nhân viên (chỉ ở UI, không thay đổi dữ liệu gốc)
                if (!string.IsNullOrWhiteSpace(searchName))
                    list = list.Where(s => !string.IsNullOrEmpty(s.EmployeeName) && s.EmployeeName.ToLower().Contains(searchName.Trim().ToLower())).ToList();
                return View(list);
            }
            else if (role == "Manager")
            {
                // P41 SECURITY: Manager only sees salaries within ManagerScope
                var scopeService = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                var managedDepts = scopeService.GetManagedOrganizationalUnits();
                var list = calculatedSalaries
                    .Where(s => !string.IsNullOrEmpty(s.DepartmentName) && managedDepts.Contains(s.DepartmentName))
                    .ToList();
                if (!string.IsNullOrWhiteSpace(searchName))
                    list = list.Where(s => !string.IsNullOrEmpty(s.EmployeeName) && s.EmployeeName.ToLower().Contains(searchName.Trim().ToLower())).ToList();
                return View(list);
            }
            else if (role == "Leader")
            {
                // P41 SECURITY: Leader only sees salaries within LeadershipScope
                var scopeService = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                var leaderDepts = scopeService.GetOverseenOrganizationalUnits();
                var list = calculatedSalaries
                    .Where(s => !string.IsNullOrEmpty(s.DepartmentName) && leaderDepts.Contains(s.DepartmentName))
                    .ToList();
                if (!string.IsNullOrWhiteSpace(searchName))
                    list = list.Where(s => !string.IsNullOrEmpty(s.EmployeeName) && s.EmployeeName.ToLower().Contains(searchName.Trim().ToLower())).ToList();
                return View(list);
            }
            else
            {
                var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
                if (currentUser?.EmployeeId == null) return Forbid();
                var mySalaryHistory = calculatedSalaries
                    .Where(s => s.EmployeeId == currentUser.EmployeeId.Value)
                    .ToList();
                return View(mySalaryHistory);
            }
        }

        // BIỂU THUẾ LŨY TIẾN 2026 (5 BẬC)
        private static double TinhThueLuyTien2026(double thuNhap)
        {
            if (thuNhap <= 0) return 0;
            double thue = 0;
            if (thuNhap <= 10000000) return thuNhap * 0.05;
            thue += 10000000 * 0.05; thuNhap -= 10000000;
            if (thuNhap <= 20000000) return thue + (thuNhap * 0.1);
            thue += 20000000 * 0.1; thuNhap -= 20000000;
            if (thuNhap <= 30000000) return thue + (thuNhap * 0.2);
            thue += 30000000 * 0.2; thuNhap -= 30000000;
            if (thuNhap <= 40000000) return thue + (thuNhap * 0.3);
            thue += 40000000 * 0.3; thuNhap -= 40000000;
            return thue + (thuNhap * 0.35);
        }
    }
}
