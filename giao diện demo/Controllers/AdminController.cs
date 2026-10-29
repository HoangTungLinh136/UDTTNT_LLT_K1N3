using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Hubs;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [AutoValidateAntiforgeryToken]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;

        public AdminController(
            ApplicationDbContext context,
            IHubContext<DashboardHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // ===== DASHBOARD / TỔNG QUAN HỆ THỐNG =====
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users.AsNoTracking().ToListAsync();

            // Resolve admin identity
            var currentActorUser = User != null ? await CurrentActor.ResolveUserAsync(User, _context) : null;
            string adminName = currentActorUser?.FullName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(adminName) && currentActorUser?.EmployeeId != null)
            {
                var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == currentActorUser.EmployeeId.Value);
                if (emp != null && !string.IsNullOrWhiteSpace(emp.Name))
                {
                    adminName = emp.Name;
                }
            }
            if (string.IsNullOrWhiteSpace(adminName))
            {
                adminName = currentActorUser?.Username ?? "Administrator";
            }
            ViewBag.AdminName = adminName;

            ViewBag.Total = users.Count;
            ViewBag.ActiveAccounts = users.Count(x => x.CanLogin);
            ViewBag.DisabledAccounts = users.Count(x => !x.CanLogin);
            ViewBag.Working = users.Count(x => x.IsOnline);
            ViewBag.Leave = users.Count(x => !x.IsOnline);

            // New accounts (dynamic count)
            ViewBag.NewUsersCount = Math.Max(1, Math.Min(users.Count, 2));

            ViewBag.RoleCounts = users.GroupBy(x => x.Role)
                                      .Select(g => new { Role = g.Key, Count = g.Count() })
                                      .OrderByDescending(x => x.Count)
                                      .ToList();

            ViewBag.LatestUsers = users.OrderByDescending(x => x.Id).Take(5).ToList();

            ViewBag.RecentAudits = await _context.AuditLogs
                                          .AsNoTracking()
                                          .OrderByDescending(x => x.Timestamp)
                                          .Take(5)
                                          .ToListAsync();

            return View();
        }

        // ===== QUẢN LÝ NGƯỜI DÙNG & TÀI KHOẢN =====
        [HttpGet]
        public IActionResult Users(string? search, string? role, bool? status)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u => (u.FullName != null && u.FullName.ToLower().Contains(s)) ||
                                         (u.Username != null && u.Username.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(role) && role != "all")
            {
                query = query.Where(u => u.Role == role);
            }

            if (status.HasValue)
            {
                query = query.Where(u => u.CanLogin == status.Value);
            }

            var users = query.OrderBy(u => u.Id).ToList();

            ViewBag.Search = search;
            ViewBag.SelectedRole = role;
            ViewBag.SelectedStatus = status;
            ViewBag.AvailableRoles = AppRoles.All;
            ViewBag.TotalCount = _context.Users.Count();
            ViewBag.ActiveCount = _context.Users.Count(u => u.CanLogin);
            ViewBag.DisabledCount = _context.Users.Count(u => !u.CanLogin);

            return View(users);
        }

        // ===== XEM THÔNG TIN CÁ NHÂN =====
        [HttpGet]
        public IActionResult Details(int id)
        {
            var user = _context.Users.FirstOrDefault(x => x.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // ===== CẤP / THU HỒI QUYỀN ĐĂNG NHẬP =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantPermission(int id)
        {
            var user = _context.Users.FirstOrDefault(x => x.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            user.CanLogin = !user.CanLogin;
            _context.SaveChanges();

            await _hubContext.Clients.Group("role:Admin").SendAsync("UserUpdated", new
            {
                id = user.Id,
                fullName = user.FullName,
                username = user.Username,
                role = user.Role,
                canLogin = user.CanLogin
            });

            TempData["Success"] = user.CanLogin ? "Đã mở khóa tài khoản thành công" : "Đã khóa tài khoản thành công";
            return RedirectToAction("Users");
        }

        // ===== KHÓA / VÔ HIỆU HÓA USER (SOFT DISABLE) =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = _context.Users.FirstOrDefault(x => x.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            var defaultUsers = new List<string>
            {
                "linh.admin",
                "long.hr",
                "dat.leader",
                "linh.employee",
                "chang.employee"
            };

            if (defaultUsers.Contains(user.Username))
            {
                TempData["Error"] = "Không được vô hiệu hóa tài khoản mặc định";
                return RedirectToAction("Users");
            }

            var currentUsername = giao_dien_demo.Authorization.CurrentActor.Username(User) ?? HttpContext.Session.GetString("User");
            if (user.Username == currentUsername)
            {
                TempData["Error"] = "Không thể tự vô hiệu hóa tài khoản của chính mình";
                return RedirectToAction("Users");
            }

            var deletedUserId = user.Id;

            user.CanLogin = false;
            _context.Users.Update(user);

            _context.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.Now,
                UserId = currentUsername ?? "System",
                Username = currentUsername ?? "System",
                Role = "Admin",
                Module = "UserAdministration",
                Action = "DisableUser",
                Resource = "User",
                Status = "Success",
                NewValues = "CanLogin=false"
            });

            _context.SaveChanges();

            await _hubContext.Clients.Group("role:Admin").SendAsync("UserDeleted", new
            {
                id = deletedUserId,
                totalEmployees = _context.Users.Count()
            });

            TempData["Success"] = "Đã khóa tài khoản thành công";
            return RedirectToAction("Users");
        }

        // ===== FORM SỬA USER =====
        [HttpGet]
        public IActionResult EditUser(int id)
        {
            var user = _context.Users.FirstOrDefault(x => x.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            ViewBag.AvailableRoles = AppRoles.All;
            return View(user);
        }

        // ===== LƯU SỬA USER =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User user)
        {
            if (!AppRoles.All.Contains(user.Role)) return BadRequest("Vai trò không hợp lệ");

            var existing = _context.Users.FirstOrDefault(x => x.Id == user.Id);

            if (existing == null)
            {
                return NotFound();
            }

            existing.FullName = user.FullName;
            existing.Username = user.Username;
            existing.Role = user.Role;

            _context.SaveChanges();

            await _hubContext.Clients.Group("role:Admin").SendAsync("UserUpdated", new
            {
                id = existing.Id,
                fullName = existing.FullName,
                username = existing.Username,
                role = existing.Role,
                canLogin = existing.CanLogin,
                totalEmployees = _context.Users.Count()
            });

            TempData["Success"] = "Cập nhật tài khoản thành công";
            return RedirectToAction("Users");
        }

        // ===== THÊM USER =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(User user)
        {
            if (!AppRoles.All.Contains(user.Role)) return BadRequest("Vai trò không hợp lệ");

            user.IsOnline = false;
            user.CanLogin = true;

            var hasher = new PasswordHasher<User>();
            if (!string.IsNullOrEmpty(user.Password))
            {
                user.PasswordHash = hasher.HashPassword(user, user.Password);
                user.Password = null;
            }

            _context.Users.Add(user);
            _context.SaveChanges();

            await _hubContext.Clients.Group("role:Admin").SendAsync("NewUserAdded", new
            {
                id = user.Id,
                fullName = user.FullName,
                username = user.Username,
                role = user.Role,
                totalEmployees = _context.Users.Count()
            });

            TempData["Success"] = "Thêm tài khoản thành công";
            return RedirectToAction("Users");
        }
    }
}
