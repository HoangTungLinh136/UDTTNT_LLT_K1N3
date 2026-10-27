using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Services;

using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AdminSecurityController : Controller
    {
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;

        public AdminSecurityController(giao_dien_demo.Services.IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GetSettings()
        {
            var settings = SecuritySettingsService.GetSettings();
            return Json(new { success = true, data = settings });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveSettings([FromBody] SecuritySettings settings)
        {
            if (settings == null) return BadRequest("Invalid settings data.");

            if (settings.MinPasswordLength < 4 || settings.MinPasswordLength > 64)
                return BadRequest(new { success = false, message = "Độ dài tối thiểu của mật khẩu phải từ 4 đến 64 ký tự." });

            if (settings.SessionIdleTimeoutMinutes < 1 || settings.SessionIdleTimeoutMinutes > 1440)
                return BadRequest(new { success = false, message = "Thời gian hết hạn phiên làm việc phải từ 1 đến 1440 phút." });

            if (settings.TrustedIPs != null)
            {
                foreach (var ip in settings.TrustedIPs)
                {
                    if (!System.Net.IPAddress.TryParse(ip, out _))
                    {
                        return BadRequest(new { success = false, message = $"Địa chỉ IP không hợp lệ: {ip}" });
                    }
                }
            }

            SecuritySettingsService.SaveSettings(settings);

            _ = _auditLogService.LogAsync("UpdateSecurityPolicy", "Security", "SecurityPolicy", "Global", "Security", "Success", null, null, System.Text.Json.JsonSerializer.Serialize(settings));

            return Json(new { success = true, message = "Chính sách bảo mật đã được cập nhật thành công." });
        }
    }
}
