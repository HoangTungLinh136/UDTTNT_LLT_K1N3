using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Hubs;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System.Linq;
using System;
using System.Net;
using System.Net.Mail;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Security.Cryptography;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;
        private readonly giao_dien_demo.Services.IEmployeeCodeAssignmentService _employeeCodeAssignmentService;
        private readonly giao_dien_demo.Services.IAdminAccountNotificationService _adminAccountNotificationService;
        private readonly Microsoft.Extensions.Logging.ILogger<AccountController>? _logger;

        public AccountController(
            ApplicationDbContext context,
            IHubContext<DashboardHub> hubContext,
            giao_dien_demo.Services.IAuditLogService auditLogService,
            giao_dien_demo.Services.IEmployeeCodeAssignmentService? employeeCodeAssignmentService = null,
            giao_dien_demo.Services.IAdminAccountNotificationService? adminAccountNotificationService = null,
            Microsoft.Extensions.Logging.ILogger<AccountController>? logger = null)
        {
            _context = context;
            _hubContext = hubContext;
            _auditLogService = auditLogService;
            _employeeCodeAssignmentService = employeeCodeAssignmentService ?? new giao_dien_demo.Services.EmployeeCodeAssignmentService(context);
            _adminAccountNotificationService = adminAccountNotificationService
                ?? new giao_dien_demo.Services.AdminAccountNotificationService(hubContext);
            _logger = logger;
        }

        // ==========================================
        // 🔑 ĐĂNG NHẬP (LOGIN)
        // ==========================================
        [HttpGet]
        public IActionResult Login()
        {
            ViewBag.SavedUsername = Request.Cookies["HRMS_SavedUser"];
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string rememberMe)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "⚠️ Vui lòng nhập đầy đủ thông tin";
                return View();
            }

            // IP Whitelist Check
            var settings = giao_dien_demo.Services.SecuritySettingsService.GetSettings();
            if (settings.EnableIPWhitelist && settings.TrustedIPs.Count > 0)
            {
                var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                if (remoteIp == "::1") remoteIp = "127.0.0.1";
                if (!settings.TrustedIPs.Contains(remoteIp))
                {
                    _ = _auditLogService.LogLoginAsync(username, false, "IP blocked by whitelist");
                    ViewBag.Error = "❌ Địa chỉ IP của bạn không nằm trong danh sách được phép đăng nhập.";
                    return View();
                }
            }

            var user = _context.Users.FirstOrDefault(x => x.Username == username);

            if (user == null)
            {
                _ = _auditLogService.LogLoginAsync(username, false, "Invalid username or password");
                ViewBag.Error = "❌ Sai tài khoản hoặc mật khẩu";
                return View();
            }

            var hasher = new PasswordHasher<User>();
            bool isPasswordValid = false;

            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
                isPasswordValid = (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded);
            }
            else if (!string.IsNullOrEmpty(user.Password) && user.Password == password)
            {
                // Legacy plaintext migration
                isPasswordValid = true;
                user.PasswordHash = hasher.HashPassword(user, password);
                user.Password = null;
            }

            if (!isPasswordValid)
            {
                _ = _auditLogService.LogLoginAsync(username, false, "Invalid username or password");
                ViewBag.Error = "❌ Sai tài khoản hoặc mật khẩu";
                return View();
            }

            if (!user.CanLogin)
            {
                _ = _auditLogService.LogLoginAsync(username, false, "Account pending approval or locked");
                ViewBag.Error = "🔒 Tài khoản của bạn đang bị khóa hoặc chờ Admin duyệt.";
                return View();
            }

            if (rememberMe == "true")
            {
                CookieOptions options = new CookieOptions { Expires = DateTime.Now.AddDays(30) };
                Response.Cookies.Append("HRMS_SavedUser", username, options);
            }
            else
            {
                Response.Cookies.Delete("HRMS_SavedUser");
            }

            user.IsOnline = true;
            user.LastActive = DateTime.Now;
            _context.SaveChanges();

            // Setup Authentication Claims
            string displayName = string.IsNullOrEmpty(user.FullName) ? user.Username : user.FullName;
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Username),
                new Claim(ClaimTypes.Name, displayName),
                new Claim(ClaimTypes.Role, user.Role ?? "Employee")
            };

            if (user.EmployeeId.HasValue)
            {
                claims.Add(new Claim("EmployeeId", user.EmployeeId.Value.ToString()));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = (rememberMe == "true"),
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            // Maintain legacy session compatibility
            HttpContext.Session.SetString("User", user.Username);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString("RealName", displayName);
            if (user.EmployeeId.HasValue)
            {
                HttpContext.Session.SetInt32("EmployeeId", user.EmployeeId.Value);
            }

            _ = _auditLogService.LogLoginAsync(username, true);

            return user.Role switch
            {
                AppRoles.Admin => RedirectToAction("Index", "Admin"),
                AppRoles.HR => RedirectToAction("Index", "HR"),
                AppRoles.Employee => RedirectToAction("Index", "NhanVien"),
                AppRoles.Manager => RedirectToAction("Index", "ManagerWorkspace"),
                AppRoles.Leader => RedirectToAction("Index", "Leader"),
                _ => RedirectToAction("Login"),
            };
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try 
            {
                var username = HttpContext.Session.GetString("User") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(username))
                {
                    await _auditLogService.LogLogoutAsync(username);
                    var user = await _context.Users.FirstOrDefaultAsync(x => x.Username == username);
                    if (user != null)
                    {
                        user.IsOnline = false;
                        user.LastActive = DateTime.Now;
                        await _context.SaveChangesAsync();
                    }
                }
                
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();
            } 
            catch { }
            return RedirectToAction("Login", "Account");
        }

        // ==========================================
        // 📝 TẠO TÀI KHOẢN (REGISTER) - ĐÃ CẬP NHẬT ĐIỀU HƯỚNG
        // ==========================================
        [HttpGet]
public IActionResult Register()
{
    ViewBag.Employees = _context.Employees
        .Where(e => !_context.Users.Any(u => u.EmployeeId == e.Id))
        .OrderBy(e => e.EmployeeCode)
        .Select(e => new
        {
            e.Id,
            e.EmployeeCode,
            e.Name,
            e.Department
        })
        .ToList();

    return View(new User());
}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(User user)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "⚠️ Vui lòng kiểm tra lại thông tin tài khoản.";
                return View(user);
            }

            user.Username = user.Username?.Trim() ?? string.Empty;
            user.Email = user.Email?.Trim() ?? string.Empty;
            user.FullName = user.FullName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.Password) || string.IsNullOrWhiteSpace(user.Email))
            {
                ViewBag.Error = "⚠️ Vui lòng điền đầy đủ các thông tin bắt buộc!";
                return View(user);
            }

            var passwordError = giao_dien_demo.Services.SecuritySettingsService.ValidatePassword(user.Password);
            if (passwordError != null)
            {
                ViewBag.Error = "❌ " + passwordError;
                return View(user);
            }

            var checkUser = await _context.Users.FirstOrDefaultAsync(x => x.Username == user.Username);
            if (checkUser != null) { ViewBag.Error = "❌ Tên đăng nhập đã tồn tại"; return View(user); }

            // Register serves both public self-registration and the existing
            // Admin user-provisioning entry point. Keep the public flow intact,
            // but never authenticate or redirect an Admin into Login after
            // creating another account.
            if (User?.IsInRole(AppRoles.Admin) == true)
            {

                // Only authenticated Admin may link a User to an existing Employee.
                if (user.EmployeeId.HasValue)
                {
                    var employeeExists = await _context.Employees
                        .AsNoTracking()
                        .AnyAsync(e => e.Id == user.EmployeeId.Value);

                    if (!employeeExists)
                    {
                        ViewBag.Error = "❌ Hồ sơ nhân viên không tồn tại.";
                        return View(user);
                    }

                    var employeeAlreadyLinked = await _context.Users
                        .AsNoTracking()
                        .AnyAsync(u => u.EmployeeId == user.EmployeeId.Value);

                    if (employeeAlreadyLinked)
                    {
                        ViewBag.Error = "❌ Hồ sơ nhân viên này đã được liên kết với tài khoản khác.";
                        return View(user);
                    }
                }

                try
                {
                    var adminCreatedUser = new User
                    {
                        FullName = user.FullName?.Trim() ?? string.Empty,
                        Email = user.Email.Trim(),
                        Username = user.Username.Trim(),
                        EmployeeId = user.EmployeeId,
                        Role = AppRoles.Employee,
                        IsOnline = false,
                        CanLogin = true,
                        LastActive = DateTime.Now
                    };
                    var adminHasher = new PasswordHasher<User>();
                    adminCreatedUser.PasswordHash = adminHasher.HashPassword(adminCreatedUser, user.Password);
                    adminCreatedUser.Password = null;

                    _context.Users.Add(adminCreatedUser);
                    await _context.SaveChangesAsync();
                    _logger?.LogInformation("Admin account User {UserId} committed for username {Username}.", adminCreatedUser.Id, adminCreatedUser.Username);
                    await _auditLogService.LogAsync(
                        "Create", "Account", "User", adminCreatedUser.Id.ToString(),
                        "AdminAccountCreated", "Success",
                        newValues: $"Username:{adminCreatedUser.Username};Role:{adminCreatedUser.Role}");
                    _logger?.LogInformation("Admin account audit completed for User {UserId}.", adminCreatedUser.Id);

                    var totalUsers = await _context.Users.CountAsync();
                    _ = _adminAccountNotificationService.NotifyCreatedAsync(adminCreatedUser, totalUsers);
                    _logger?.LogInformation("Admin account response can complete for User {UserId}; notification dispatched asynchronously.", adminCreatedUser.Id);

                    TempData["SuccessMessage"] = "🎉 Tạo tài khoản thành công.";
                    return RedirectToAction(nameof(Register));
                }
                catch (DbUpdateException)
                {
                    ViewBag.Error = "❌ Tên đăng nhập hoặc thông tin tài khoản đã tồn tại.";
                    return View(user);
                }
            }

            try
            {
                user.Role = "Employee";
                user.EmployeeId = null; // Public self-registration must never choose employee identity.
                user.IsOnline = false;
                user.LastActive = DateTime.Now;
                user.CanLogin = false; // Admin vẫn phải duyệt thì mới đăng nhập được ở lần sau

                var hasher = new PasswordHasher<User>();
                user.PasswordHash = hasher.HashPassword(user, user.Password);
                user.Password = null;

                _context.Users.Add(user);

                var newEmployee = new Employee
                {
                    Name = user.FullName ?? user.Username,
                    Department = "Chưa xếp phòng",
                    Position = "Thử việc",
                    JobTitle = "Thực tập sinh (Intern)",
                    Salary = 6000000
                };
                user.Employee = newEmployee;
                _context.Employees.Add(newEmployee);

                await _context.SaveChangesAsync();
                await _employeeCodeAssignmentService.EnsureAssignedAsync(newEmployee);

                var totalUsers = await _context.Users.CountAsync();
                _ = _adminAccountNotificationService.NotifyCreatedAsync(user, totalUsers);

                // 🔥 ĐÃ ĐỔI CHỮ: Từ "Đăng ký" sang "Tạo tài khoản"
                TempData["SuccessMessage"] = "🎉 Tạo tài khoản thành công! Vui lòng chờ Admin cấp quyền.";

                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "❌ Lỗi hệ thống: " + ex.Message;
                return View(user);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        // ==========================================
        // 📩 QUÊN MẬT KHẨU & KHÔI PHỤC (GMAIL)
        // ==========================================
        [HttpGet] public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(string username, string email)
        {
            var user = _context.Users.FirstOrDefault(x => x.Username == username && x.Email == email);

            if (user == null)
            {
                ViewBag.Error = "❌ Thông tin Tên đăng nhập hoặc Email không chính xác!";
                return View();
            }

            string recoveryCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            try
            {
                var notifService = new giao_dien_demo.Services.NotificationConfigService();
                var smtpSettings = notifService.LoadSettings().Smtp;

                if (string.IsNullOrEmpty(smtpSettings.Host) || string.IsNullOrEmpty(smtpSettings.Username))
                {
                    ViewBag.Error = "⚠️ Tính năng gửi email chưa được cấu hình SMTP. Vui lòng liên hệ Admin.";
                    return View();
                }

                var fromAddress = new MailAddress(smtpSettings.SenderEmail ?? smtpSettings.Username, smtpSettings.SenderName ?? "HRMS Support System");

                var smtp = new SmtpClient
                {
                    Host = smtpSettings.Host,
                    Port = smtpSettings.Port,
                    EnableSsl = smtpSettings.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(smtpSettings.Username, smtpSettings.Password)
                };

                using var message = new MailMessage(fromAddress, new MailAddress(email))
                {
                    Subject = "Mã xác thực khôi phục mật khẩu - HRMS",
                    Body = $"Chào {user.FullName},\n\nBạn đang thực hiện khôi phục mật khẩu cho tài khoản: {user.Username}.\nMã xác nhận của bạn là: {recoveryCode}\n\nMã có hiệu lực trong phiên làm việc này."
                };
                smtp.Send(message);

                HttpContext.Session.SetString("RecoveryCode", recoveryCode);
                HttpContext.Session.SetString("RecoveryUser", user.Username);
                HttpContext.Session.SetString("RecoveryCodeExpiresAt", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString());
                HttpContext.Session.Remove("RecoveryVerified");
                HttpContext.Session.Remove("RecoveryCodeAttempts");

                return RedirectToAction("VerifyCode");
            }
            catch (Exception ex) { ViewBag.Error = "⚠️ Lỗi gửi mail: " + ex.Message; return View(); }
        }

        [HttpGet] public IActionResult VerifyCode() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyCode(string code)
        {
            var sessionCode = HttpContext.Session.GetString("RecoveryCode");
            var expiresAtValue = HttpContext.Session.GetString("RecoveryCodeExpiresAt");
            
            // Check if there is an active challenge
            if (string.IsNullOrEmpty(sessionCode))
            {
                ViewBag.Error = "❌ Yêu cầu xác nhận không hợp lệ hoặc đã bị hủy. Vui lòng thử lại.";
                return View();
            }
            
            var isUnexpired = long.TryParse(expiresAtValue, out var expiresAt)
                && DateTimeOffset.UtcNow.ToUnixTimeSeconds() <= expiresAt;

            if (!isUnexpired)
            {
                HttpContext.Session.Remove("RecoveryCode");
                HttpContext.Session.Remove("RecoveryCodeExpiresAt");
                HttpContext.Session.Remove("RecoveryUser");
                HttpContext.Session.Remove("RecoveryCodeAttempts");
                ViewBag.Error = "❌ Mã xác nhận đã hết hạn! Vui lòng gửi lại yêu cầu.";
                return View();
            }

            var attemptStr = HttpContext.Session.GetString("RecoveryCodeAttempts") ?? "0";
            int attempts = int.TryParse(attemptStr, out var a) ? a : 0;

            if (attempts >= 5)
            {
                HttpContext.Session.Remove("RecoveryCode");
                HttpContext.Session.Remove("RecoveryCodeExpiresAt");
                HttpContext.Session.Remove("RecoveryUser");
                HttpContext.Session.Remove("RecoveryCodeAttempts");
                ViewBag.Error = "❌ Bạn đã nhập sai quá số lần quy định. Yêu cầu khôi phục đã bị hủy. Vui lòng thực hiện lại.";
                return View();
            }

            if (!string.IsNullOrEmpty(code) && code.Trim() == sessionCode)
            {
                HttpContext.Session.SetString("RecoveryVerified", "true");
                HttpContext.Session.Remove("RecoveryCode");
                HttpContext.Session.Remove("RecoveryCodeExpiresAt");
                HttpContext.Session.Remove("RecoveryCodeAttempts");
                return RedirectToAction("ResetPassword");
            }

            attempts++;
            HttpContext.Session.SetString("RecoveryCodeAttempts", attempts.ToString());

            if (attempts >= 5)
            {
                HttpContext.Session.Remove("RecoveryCode");
                HttpContext.Session.Remove("RecoveryCodeExpiresAt");
                HttpContext.Session.Remove("RecoveryUser");
                HttpContext.Session.Remove("RecoveryCodeAttempts");
                ViewBag.Error = "❌ Bạn đã nhập sai quá số lần quy định. Yêu cầu khôi phục đã bị hủy. Vui lòng thực hiện lại.";
                return View();
            }

            ViewBag.Error = "❌ Mã xác nhận không chính xác!";
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            if (HttpContext.Session.GetString("RecoveryVerified") != "true"
                || string.IsNullOrEmpty(HttpContext.Session.GetString("RecoveryUser")))
            {
                return RedirectToAction("ForgotPassword");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string newPassword, string confirmPassword)
        {
            if (HttpContext.Session.GetString("RecoveryVerified") != "true"
                || string.IsNullOrEmpty(HttpContext.Session.GetString("RecoveryUser")))
            {
                return RedirectToAction("ForgotPassword");
            }

            if (string.IsNullOrEmpty(newPassword) || newPassword != confirmPassword)
            {
                ViewBag.Error = "❌ Mật khẩu không khớp!";
                return View();
            }

            var passwordError = giao_dien_demo.Services.SecuritySettingsService.ValidatePassword(newPassword);
            if (passwordError != null)
            {
                ViewBag.Error = "❌ " + passwordError;
                return View();
            }

            var username = HttpContext.Session.GetString("RecoveryUser");
            var user = _context.Users.FirstOrDefault(x => x.Username == username);

            if (user != null)
            {
                var hasher = new PasswordHasher<User>();
                user.PasswordHash = hasher.HashPassword(user, newPassword);
                user.Password = null;

                _context.SaveChanges();
                HttpContext.Session.Remove("RecoveryCode");
                HttpContext.Session.Remove("RecoveryCodeExpiresAt");
                HttpContext.Session.Remove("RecoveryVerified");
                HttpContext.Session.Remove("RecoveryUser");
                HttpContext.Session.Remove("RecoveryCodeAttempts");
                TempData["SuccessMessage"] = "✅ Mật khẩu đã được cập nhật!";
                return RedirectToAction("Login");
            }

            HttpContext.Session.Remove("RecoveryCode");
            HttpContext.Session.Remove("RecoveryCodeExpiresAt");
            HttpContext.Session.Remove("RecoveryVerified");
            HttpContext.Session.Remove("RecoveryUser");
            HttpContext.Session.Remove("RecoveryCodeAttempts");
            return RedirectToAction("ForgotPassword");
        }
    }
}



