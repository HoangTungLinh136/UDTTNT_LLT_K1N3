using Microsoft.AspNetCore.Mvc;
using giao_dien_demo.Data;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AdminRoleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;

        public AdminRoleController(ApplicationDbContext context, giao_dien_demo.Services.IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
        }

        public IActionResult Index()
        {
            // Fetch real role data from Users table
            var rolesSummary = _context.Users
                .GroupBy(u => u.Role)
                .Select(g => new { RoleName = g.Key, UserCount = g.Count() })
                .ToList();

            var users = _context.Users
                .Select(u => new { u.Id, u.Username, u.FullName, u.Email, u.Role, u.CanLogin })
                .ToList();

            ViewBag.RolesSummary = rolesSummary;
            ViewBag.Users = users;
            ViewBag.AvailableRoles = AppRoles.All;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateUserPermission(int userId, string newRole, string canLoginStr)
        {
            if (!AppRoles.All.Contains(newRole)) return BadRequest("Vai trò không hợp lệ");

            var userToUpdate = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (userToUpdate == null) return NotFound("Không tìm thấy người dùng");

            bool canLogin = canLoginStr == "true" || canLoginStr == "on" || canLoginStr == "True";

            userToUpdate.Role = newRole;
            userToUpdate.CanLogin = canLogin;

            _context.SaveChanges();

            _ = _auditLogService.LogAsync("UpdatePermission", "User/RBAC", "User", userToUpdate.Username, "DataChange", "Success", null, null, $"Role:{newRole}, CanLogin:{canLogin}");

            TempData["SuccessMessage"] = $"Cập nhật phân quyền thành công cho {userToUpdate.Username}";
            return RedirectToAction(nameof(Index));
        }
    }
}
