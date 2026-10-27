using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AdminSettingsController : Controller
    {
        private readonly ISystemConfigService _configService;
        private readonly IAuditLogService _auditLogService;

        public AdminSettingsController(ISystemConfigService configService, IAuditLogService auditLogService)
        {
            _configService = configService;
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Index()
        {
            var configs = await _configService.GetAllConfigsAsync();
            return View(configs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCompany([FromForm] Dictionary<string, string> data, IFormFile logoFile)
        {
            var user = User.Identity?.Name ?? "System";

            var updates = new Dictionary<string, string>();
            foreach(var kvp in data)
            {
                if(kvp.Key.StartsWith("Company."))
                {
                    updates[kvp.Key] = kvp.Value;
                }
            }

            if (logoFile != null && logoFile.Length > 0)
            {
                var allowedExts = new[] { ".png", ".jpg", ".jpeg" };
                var ext = Path.GetExtension(logoFile.FileName).ToLower();
                if (Array.IndexOf(allowedExts, ext) >= 0)
                {
                    var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "logo");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    
                    var fileName = "company_logo" + ext;
                    var filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await logoFile.CopyToAsync(stream);
                    }

                    updates["Company.LogoPath"] = "/uploads/logo/" + fileName;
                }
            }

            await _configService.UpdateConfigsAsync(updates, user);
            return Json(new { success = true, message = "Lưu thông tin Công ty thành công." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveLocalization([FromBody] Dictionary<string, string> data)
        {
            return await SaveCategory("Loc.", data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveStorage([FromBody] Dictionary<string, string> data)
        {
            return await SaveCategory("Storage.", data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSystem([FromBody] Dictionary<string, string> data)
        {
            return await SaveCategory("Sys.", data);
        }

        private async Task<IActionResult> SaveCategory(string prefix, Dictionary<string, string> data)
        {
            var user = User.Identity?.Name ?? "System";

            var updates = new Dictionary<string, string>();
            foreach(var kvp in data)
            {
                if(kvp.Key.StartsWith(prefix))
                {
                    // Validation for specific keys
                    if (kvp.Key == "Storage.MaxFileSizeMB")
                    {
                        if (!int.TryParse(kvp.Value, out int maxSize) || maxSize <= 0 || maxSize > 1024)
                        {
                            return Json(new { success = false, message = "Dung lượng upload tối đa phải là số nguyên dương từ 1 đến 1024." });
                        }
                    }

                    if (kvp.Key == "Sys.MaintenanceMode")
                    {
                        if (kvp.Value != "true" && kvp.Value != "false")
                        {
                            return Json(new { success = false, message = "Giá trị bảo trì không hợp lệ." });
                        }
                    }

                    updates[kvp.Key] = kvp.Value;
                }
            }

            await _configService.UpdateConfigsAsync(updates, user);
            return Json(new { success = true, message = "Lưu cấu hình thành công." });
        }
    }
}
