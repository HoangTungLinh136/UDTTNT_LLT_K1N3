using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Authorization;
using giao_dien_demo.Helpers;
using giao_dien_demo.Models;
using System.Linq;
using System;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.HROnly)]
    [AutoValidateAntiforgeryToken]
    public class PayrollSettingController : Controller
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
        public IActionResult GetSettings()
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            
            var all = PayrollSettingService.GetAllVersions();
            var active = all.OrderByDescending(v => v.EffectiveFrom).FirstOrDefault(v => v.Status == "ACTIVE");
            var draft = all.FirstOrDefault(v => v.Status == "DRAFT");
            
            // Trả về cả danh sách lịch sử để render timeline
            var history = all.OrderByDescending(v => v.CreatedAt).ToList();
            
            return Json(new { success = true, active = active, draft = draft, history = history });
        }

        [HttpGet]
        public IActionResult GetVersionDetail(string versionId)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            
            var version = PayrollSettingService.GetVersion(versionId);
            if (version == null) return NotFound();
            
            return Json(new { success = true, data = version });
        }

        [HttpPost]
        public IActionResult CreateNewVersion()
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            string user = HttpContext.Session.GetString("User");
            
            try
            {
                var draft = PayrollSettingService.CreateDraft(user);
                return Json(new { success = true, data = draft });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult SaveVersion([FromBody] PayrollSettingVersion input)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            if (input == null || string.IsNullOrWhiteSpace(input.VersionId)) 
                return BadRequest("Dữ liệu không hợp lệ.");

            string user = HttpContext.Session.GetString("User");
            try 
            {
                var saved = PayrollSettingService.SaveDraft(input, user);
                return Json(new { success = true, data = saved });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult ActivateVersion(string versionId, DateTime effectiveFrom)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(versionId)) return BadRequest("Thiếu VersionId.");

            string user = HttpContext.Session.GetString("User");
            try 
            {
                var activated = PayrollSettingService.ActivateVersion(versionId, effectiveFrom, user);
                return Json(new { success = true, data = activated });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
