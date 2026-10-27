using System.Security.Claims;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Controllers;

public sealed class ThongBaoTam
{
    public int Id { get; set; }
    public string NoiDung { get; set; } = string.Empty;
    public string NguoiGui { get; set; } = string.Empty;
    public string NguoiNhan { get; set; } = string.Empty;
    public DateTime NgayTao { get; set; }
    public string Type { get; set; } = "SYSTEM";
    public string? TargetUrl { get; set; }
    public bool IsRead { get; set; }
    public int? ReferenceId { get; set; }
}

[Authorize]
public sealed class NotificationController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IInAppNotificationService _notifications;
    private readonly ILeadershipScopeService _leadershipScope;

    public NotificationController(ApplicationDbContext context, IInAppNotificationService notifications, ILeadershipScopeService leadershipScope)
    {
        _context = context;
        _notifications = notifications;
        _leadershipScope = leadershipScope;
    }

    private string? CurrentUsername => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? HttpContext.Session.GetString("User");

    private Task<User?> GetCurrentUserAsync()
    {
        var username = CurrentUsername;
        return string.IsNullOrWhiteSpace(username)
            ? Task.FromResult<User?>(null)
            : _context.Users.FirstOrDefaultAsync(x => x.Username == username && x.CanLogin);
    }

    public async Task<IActionResult> Index(string? returnTo)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Challenge();

        IQueryable<Employee> employees = _context.Employees.AsNoTracking();
        if (currentUser.Role == "Leader") employees = _leadershipScope.GetScopedEmployeeQuery().AsNoTracking();
        ViewBag.Employees = await employees.OrderBy(x => x.Name).ToListAsync();
        ViewBag.ReturnTo = returnTo;

        var items = await _context.Notifications.AsNoTracking()
            .Where(x => x.RecipientUserId == currentUser.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ThongBaoTam
            {
                Id = x.Id,
                NoiDung = x.Message ?? string.Empty,
                NguoiGui = x.SenderName ?? "Hệ thống",
                NguoiNhan = x.NguoiNhan ?? currentUser.Username,
                NgayTao = x.CreatedAt,
                Type = x.NotificationType ?? "SYSTEM",
                TargetUrl = x.TargetUrl,
                IsRead = x.IsRead,
                ReferenceId = x.ReferenceId
            }).ToListAsync();
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HR,Leader")]
    public async Task<IActionResult> GuiThongBao(string noiDung, string nguoiNhan, string? returnTo)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Challenge();
        if (currentUser.Role is not ("HR" or "Admin" or "Leader")) return Forbid();

        IReadOnlyCollection<int>? allowedIds = null;
        if (currentUser.Role == "Leader")
        {
            var employeeIds = await _leadershipScope.GetScopedEmployeeQuery().Select(x => x.Id).ToListAsync();
            allowedIds = await _context.Users
                .Where(x => x.EmployeeId != null && employeeIds.Contains(x.EmployeeId.Value))
                .Select(x => x.Id).ToListAsync();
        }

        var created = await _notifications.SendAsync(
            nguoiNhan, noiDung, "MANUAL", null, null,
            currentUser.Id, currentUser.FullName, null, allowedIds);
        if (created.Count == 0)
            return BadRequest(new { success = false, message = "Không có người nhận hợp lệ trong phạm vi quyền." });
        return Ok(new { success = true, count = created.Count, returnTo });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Challenge();
        var item = await _context.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientUserId == currentUser.Id);
        if (item == null) return NotFound();
        _context.Notifications.Remove(item);
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Challenge();
        var item = await _context.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientUserId == currentUser.Id);
        if (item == null) return NotFound();
        if (!item.IsRead)
        {
            item.IsRead = true;
            await _context.SaveChangesAsync();
        }
        return Ok(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Challenge();
        var count = await _context.Notifications.CountAsync(x => x.RecipientUserId == currentUser.Id && !x.IsRead);
        return Json(new { count });
    }
}
