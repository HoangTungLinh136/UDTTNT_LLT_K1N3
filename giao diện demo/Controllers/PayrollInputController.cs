using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Authorization;
using giao_dien_demo.Helpers;
using giao_dien_demo.Models;
using System.Linq;
using System.Threading.Tasks;
using giao_dien_demo.Data;
using System;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.HROnly)]
    [AutoValidateAntiforgeryToken]
    public class PayrollInputController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PayrollInputController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool CheckLogin()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("User"));
        }

        private bool CheckPermission()
        {
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            return role == "HR"; // Yêu cầu quyền
        }

        public async Task<IActionResult> Index()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckPermission()) return Forbid();

            // Default Period = Current Month
            var currentCycle = PayrollCycleHelper.GetCurrentCycle(DateTime.Now);
            ViewBag.CurrentPeriod = currentCycle.PeriodName; // e.g. "09/2026"

            // Gửi danh sách kỳ lương gần đây (3 tháng)
            var periods = new[]
            {
                PayrollCycleHelper.GetCurrentCycle(DateTime.Now.AddMonths(-1)).PeriodName,
                PayrollCycleHelper.GetCurrentCycle(DateTime.Now).PeriodName,
                PayrollCycleHelper.GetCurrentCycle(DateTime.Now.AddMonths(1)).PeriodName
            };
            ViewBag.Periods = periods;

            var employees = await _context.Employees.Select(e => new { e.Id, e.Name }).ToListAsync();
            ViewBag.Employees = employees;

            return View();
        }

        [HttpGet]
        public IActionResult GetInputData(string period)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            
            var data = PayrollInputService.GetInputsByPeriod(period).OrderByDescending(d => d.UpdatedAt).ToList();
            return Json(new { success = true, data = data });
        }

        [HttpGet]
        public IActionResult GetKPI(string period)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();

            var kpi = PayrollInputService.GetKPI(period, _context);
            return Json(new { success = true, data = kpi });
        }

        [HttpGet]
        public IActionResult GetAllowedComponents()
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            return Json(PayrollInputService.GetAllowedComponents());
        }

        [HttpPost]
        public async Task<IActionResult> SyncAttendance(string period)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            if (string.IsNullOrEmpty(period)) return BadRequest("Thiếu kỳ lương.");

            string user = HttpContext.Session.GetString("User");
            await PayrollInputService.SyncAttendanceData(_context, period, user);

            return Json(new { success = true, message = "Đồng bộ chấm công thành công." });
        }

        [HttpPost]
        public IActionResult SaveManualInput([FromBody] PayrollInputRecord input)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            if (input == null || string.IsNullOrEmpty(input.PayrollPeriod) || string.IsNullOrEmpty(input.ComponentCode) || input.EmployeeId == 0) 
                return BadRequest("Thiếu thông tin bắt buộc.");

            string user = HttpContext.Session.GetString("User");
            try 
            {
                var saved = PayrollInputService.SaveManualInput(input, user, _context);
                return Json(new { success = true, data = saved });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
