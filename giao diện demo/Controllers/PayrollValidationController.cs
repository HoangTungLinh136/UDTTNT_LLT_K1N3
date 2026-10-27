using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Helpers;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader},{AppRoles.Manager}")]
    [AutoValidateAntiforgeryToken]
    public class PayrollValidationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PayrollValidationController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool CheckLogin()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("User"));
        }

        public async Task<IActionResult> Index(string? period, string? severity, string? source, string? employee)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role != AppRoles.HR && role != AppRoles.Leader && role != AppRoles.Manager) return Forbid();

            // Tính toán TargetDate dựa trên period
            DateTime? targetDate = null;
            if (!string.IsNullOrEmpty(period))
            {
                if (DateTime.TryParseExact("01/" + period, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    targetDate = parsedDate;
                }
            }
            
            var cycle = PayrollCycleHelper.GetCurrentCycle(targetDate ?? DateTime.Now);
            ViewBag.FilterPeriod = cycle.PeriodName;

            // Chạy Validation Service
            var summary = await PayrollValidationService.RunValidationAsync(_context, targetDate);

            // Filter danh sách Issues
            var issuesQuery = summary.Issues.AsEnumerable();

            if (role == AppRoles.Manager)
            {
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                var allowedEmployeeCodes = managerScope.GetScopedEmployeeQuery().Select(e => e.EmployeeCode).ToHashSet();
                issuesQuery = issuesQuery.Where(i => i.EmployeeCode != null && allowedEmployeeCodes.Contains(i.EmployeeCode));
            }
            else if (role == AppRoles.Leader)
            {
                var leaderScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                var allowedEmployeeCodes = leaderScope.GetScopedEmployeeQuery().Select(e => e.EmployeeCode).ToHashSet();
                issuesQuery = issuesQuery.Where(i => i.EmployeeCode != null && allowedEmployeeCodes.Contains(i.EmployeeCode));
            }

            if (!string.IsNullOrEmpty(severity))
            {
                issuesQuery = issuesQuery.Where(i => i.Severity == severity);
            }

            if (!string.IsNullOrEmpty(source))
            {
                issuesQuery = issuesQuery.Where(i => i.SourceModule == source);
            }

            if (!string.IsNullOrEmpty(employee))
            {
                issuesQuery = issuesQuery.Where(i => 
                    (!string.IsNullOrEmpty(i.EmployeeName) && i.EmployeeName.Contains(employee, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(i.EmployeeCode) && i.EmployeeCode.Contains(employee, StringComparison.OrdinalIgnoreCase))
                );
            }

            // Gán dữ liệu Filter
            ViewBag.FilterSeverity = severity;
            ViewBag.FilterSource = source;
            ViewBag.FilterEmployee = employee;

            // Gán các list chọn lọc cho Filter
            ViewBag.Sources = summary.Issues.Select(i => i.SourceModule).Distinct().ToList();

            // Trả về tuple DTO (Summary, FilteredIssues)
            return View(Tuple.Create(summary, issuesQuery.ToList()));
        }

        [HttpPost]
        public async Task<IActionResult> RunPayroll(string period)
        {
            if (!CheckLogin()) return Unauthorized();
            var role = HttpContext.Session.GetString("Role");
            if (role != "HR" && role != "Leader" && role != "Manager") return Forbid();

            DateTime? targetDate = null;
            if (!string.IsNullOrEmpty(period))
            {
                if (DateTime.TryParseExact("01/" + period, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    targetDate = parsedDate;
                }
            }

            // Kiểm tra lại lần cuối trước khi cho phép chạy
            var summary = await PayrollValidationService.RunValidationAsync(_context, targetDate);

            if (!summary.IsReadyForPayroll)
            {
                // Nếu chưa sẵn sàng (có lỗi Blocking), từ chối thực hiện tính lương
                TempData["ErrorMessage"] = "Không thể chạy tính lương! Dữ liệu vẫn còn các vấn đề lỗi nghiêm trọng (Blocking). Vui lòng xử lý và thử lại.";
                return RedirectToAction("Index", new { period = period });
            }

            // DO ACTUAL PAYROLL RUN HERE
            // Vì logic tính lương của project hiện tại nằm ở SalaryController, chúng ta chuyển hướng về đó
            TempData["SuccessMessage"] = "Dữ liệu hợp lệ! Đang điều hướng sang bảng tính lương...";
            
            return RedirectToAction("Index", "Salary");
        }
    }
}
