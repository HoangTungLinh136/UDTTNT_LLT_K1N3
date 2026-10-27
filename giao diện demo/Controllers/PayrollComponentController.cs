using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Authorization;
using giao_dien_demo.Helpers;
using giao_dien_demo.Models;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.HROnly)]
    [AutoValidateAntiforgeryToken]
    public class PayrollComponentController : Controller
    {
        private bool CheckLogin()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("User"));
        }

        private bool CheckPermission()
        {
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            return role == "HR"; // Yêu cầu quyền
        }

        public IActionResult Index()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckPermission()) return Forbid();

            return View();
        }

        [HttpGet]
        public IActionResult GetComponents()
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            
            var data = PayrollComponentService.GetAllComponents().OrderBy(c => c.CalculationOrder).ToList();
            return Json(new { success = true, data = data });
        }

        [HttpGet]
        public IActionResult GetKPI()
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();

            var data = PayrollComponentService.GetAllComponents();
            
            var total = data.Count;
            var thuNhap = data.Count(c => c.Category == "Lương" || c.Category == "Phụ cấp" || c.Category == "Thưởng" || c.Category == "Điều chỉnh");
            var khauTru = data.Count(c => c.Category == "Khấu trừ");
            var active = data.Count(c => c.Status == "ACTIVE");

            return Json(new { success = true, data = new { total, thuNhap, khauTru, active } });
        }

        [HttpPost]
        public IActionResult SaveComponent([FromBody] PayrollComponentRecord input)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            if (input == null || string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name)) 
                return BadRequest("Thiếu thông tin bắt buộc (Mã, Tên).");

            string user = HttpContext.Session.GetString("User");
            try 
            {
                var saved = PayrollComponentService.SaveComponent(input, user);
                return Json(new { success = true, data = saved });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
