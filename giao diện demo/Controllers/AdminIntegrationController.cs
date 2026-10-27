using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using System.Net.Mail;
using System.Net;
using System;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AdminIntegrationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationConfigService _notificationService;
        private readonly IntegrationConfigService _integrationConfigService;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;
        private readonly giao_dien_demo.Services.ICameraCredentialProtector _protector;

        public AdminIntegrationController(ApplicationDbContext context, NotificationConfigService notificationService, IntegrationConfigService integrationConfigService, giao_dien_demo.Services.IAuditLogService auditLogService, giao_dien_demo.Services.ICameraCredentialProtector protector)
        {
            _context = context;
            _notificationService = notificationService;
            _integrationConfigService = integrationConfigService;
            _auditLogService = auditLogService;
            _protector = protector;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var emailConfig = _notificationService.LoadSettings();
            if (!string.IsNullOrEmpty(emailConfig.Smtp.Password))
            {
                emailConfig.Smtp.Password = "********";
            }

            var aiConfig = _integrationConfigService.LoadSettings().AiProvider;
            if (!string.IsNullOrEmpty(aiConfig.ApiKey))
            {
                aiConfig.ApiKey = "********";
            }

            var cameras = await _context.CameraConfigs.AsNoTracking().ToListAsync();
            foreach (var cam in cameras)
            {
                cam.Password = "";
                var plainRtsp = _protector.Unprotect(cam.RtspUrl);
                if (!string.IsNullOrEmpty(plainRtsp))
                {
                    cam.RtspUrl = System.Text.RegularExpressions.Regex.Replace(plainRtsp, @"rtsp://[^@]+@", "rtsp://***:***@");
                }
            }

            var viewModel = new AdminIntegrationViewModel
            {
                EmailConfig = emailConfig,
                CameraConfigs = cameras,
                AiConfig = aiConfig
            };

            return View(viewModel);
        }

        // ==========================================
        // EMAIL INTEGRATION
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveEmailConfig(SmtpSettings smtp)
        {
            var settings = _notificationService.LoadSettings();
            if (smtp.Password == "********")
            {
                smtp.Password = settings.Smtp.Password;
            }
            settings.Smtp = smtp;
            _notificationService.SaveSettings(settings);

            _ = _auditLogService.LogAsync("UpdateIntegration", "Integration", "EmailConfig", "SMTP", "DataChange", "Success");

            TempData["Success"] = "Lưu cấu hình Email (SMTP) thành công.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TestEmailConnection(string testEmail)
        {
            var settings = _notificationService.LoadSettings().Smtp;
            if (string.IsNullOrEmpty(settings.Host) || string.IsNullOrEmpty(settings.Username))
            {
                TempData["Error"] = "Vui lòng cấu hình Host và Username trước khi kiểm tra.";
                return RedirectToAction("Index");
            }

            try
            {
                var fromAddress = new MailAddress(settings.SenderEmail ?? settings.Username, settings.SenderName ?? "HRMS System");
                var toAddress = new MailAddress(testEmail);
                var smtp = new SmtpClient
                {
                    Host = settings.Host,
                    Port = settings.Port,
                    EnableSsl = settings.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(settings.Username, settings.Password)
                };
                using var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = "Kết nối Email HRMS Thành Công",
                    Body = "Chào bạn,\n\nHệ thống HRMS đã kết nối thành công với máy chủ SMTP này.\n\nTrân trọng,"
                };
                smtp.Send(message);
                TempData["Success"] = $"Kiểm tra kết nối thành công! Đã gửi email tới {testEmail}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Kết nối thất bại: " + ex.Message;
            }
            return RedirectToAction("Index");
        }

        // ==========================================
        // AI PROVIDER INTEGRATION
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveAiConfig(AiProviderSettings aiConfig)
        {
            var settings = _integrationConfigService.LoadSettings();
            aiConfig.ApiKey = string.Empty;
            settings.AiProvider = aiConfig;
            _integrationConfigService.SaveSettings(settings);

            _ = _auditLogService.LogAsync("UpdateIntegration", "Integration", "AiConfig", "AI", "DataChange", "Success");

            TempData["Success"] = "Đã lưu cấu hình không nhạy cảm. API key phải được cấp qua cấu hình ngoài (Integrations__AI__ApiKey/user-secrets).";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestAiConnection([FromServices] AiService aiService)
        {
            try
            {
                // Gọi thử phương thức AskAsync
                var response = await aiService.AskAsync("Say 'Hello HRMS'");
                TempData["Success"] = $"Kiểm tra kết nối AI thành công! Phản hồi: {response}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi kết nối AI: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // ==========================================
        // THIẾT BỊ CHẤM CÔNG (CAMERA IMOU)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDevice(CameraConfig config)
        {
            if (config.Id > 0)
            {
                var existing = await _context.CameraConfigs.FindAsync(config.Id);
                if (existing != null)
                {
                    existing.Name = config.Name;
                    existing.IPAddress = config.IPAddress;
                    if (!string.IsNullOrEmpty(config.RtspUrl) && config.RtspUrl.Contains("***:***@"))
                    {
                        // Unchanged. Migrate if legacy.
                        if (_protector.IsProtected(existing.RtspUrl))
                        {
                            config.RtspUrl = existing.RtspUrl;
                        }
                        else
                        {
                            config.RtspUrl = _protector.Protect(existing.RtspUrl);
                        }
                    }
                    else
                    {
                        existing.RtspUrl = _protector.Protect(config.RtspUrl);
                    }
                    existing.Username = config.Username;
                    existing.IsActive = config.IsActive;
                    if (!string.IsNullOrEmpty(config.Password))
                    {
                        existing.Password = _protector.Protect(config.Password);
                    }
                    else if (!_protector.IsProtected(existing.Password) && !string.IsNullOrEmpty(existing.Password))
                    {
                        existing.Password = _protector.Protect(existing.Password);
                    }
                    _context.Update(existing);
                }
            }
            else
            {
                _context.CameraConfigs.Add(config);
            }
            await _context.SaveChangesAsync();
            
            _ = _auditLogService.LogAsync("UpdateIntegration", "Integration", "CameraConfig", config.Id.ToString(), "DataChange", "Success");

            TempData["Success"] = "Đã lưu cấu hình thiết bị thành công.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDevice(int id)
        {
            var device = await _context.CameraConfigs.FindAsync(id);
            if (device != null)
            {
                _context.CameraConfigs.Remove(device);
                await _context.SaveChangesAsync();
                
                _ = _auditLogService.LogAsync("UpdateIntegration", "Integration", "CameraConfig", id.ToString(), "DataChange", "Success");

                TempData["Success"] = "Đã xóa thiết bị thành công.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestDeviceConnection(int id)
        {
            var device = await _context.CameraConfigs.FindAsync(id);
            if (device == null)
            {
                TempData["Error"] = "Không tìm thấy thiết bị.";
                return RedirectToAction("Index");
            }

            // Extract Host and Port from RtspUrl if IPAddress isn't used directly for ping, 
            // but we'll just try to parse the host from IPAddress or RtspUrl and ping port 554
            string host = device.IPAddress;
            int port = 554; // default RTSP port

            if (string.IsNullOrEmpty(host))
            {
                try {
                    Uri uri = new Uri(device.RtspUrl);
                    host = uri.Host;
                    port = uri.Port > 0 ? uri.Port : 554;
                } catch { }
            }

            if (string.IsNullOrEmpty(host))
            {
                TempData["Error"] = "Cấu hình IP hoặc URL không hợp lệ.";
                return RedirectToAction("Index");
            }

            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect(host, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3)); // 3s timeout
                
                if (success)
                {
                    client.EndConnect(result);
                    TempData["Success"] = $"Kết nối tới thiết bị ({host}:{port}) thành công!";
                }
                else
                {
                    TempData["Error"] = $"Không thể kết nối tới {host}:{port} trong 3 giây (Timeout).";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}
