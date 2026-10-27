using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Hubs;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace giao_dien_demo.Controllers
{
    // Model t?m th?i cho thng bo (V b?n dang dng list tinh)
    public class ThongBaoTam
    {
        public int Id { get; set; }
        public string NoiDung { get; set; } = string.Empty;
        public string NguoiGui { get; set; } = string.Empty;
        public string NguoiNhan { get; set; } = string.Empty;
        public DateTime NgayTao { get; set; } = DateTime.Now;
        public string Type { get; set; } = "SYSTEM";
        public string? TargetUrl { get; set; }
        public bool IsRead { get; set; } = false;
        public int? ReferenceId { get; set; }
    }

    [Authorize]
    public class NotificationController : Controller
    {
        // Danh sch tinh luu tr? l?ch s? thng bo
        public static List<ThongBaoTam> list = new List<ThongBaoTam>();

        private readonly ApplicationDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;

        public NotificationController(ApplicationDbContext context, IHubContext<DashboardHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private bool CheckLogin()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("User"));
        }

        // --- 1. HI?N TH? DNH SCH THNG BO ---
        public async Task<IActionResult> Index(string? returnTo)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");

            var user = HttpContext.Session.GetString("User") ?? "";
            var role = HttpContext.Session.GetString("Role");
            var realName = HttpContext.Session.GetString("RealName") ?? "";

            // ??  S?A: Lo?i b? di?u ki?n e.Position == "Chnh th?c"
            // By gi? h? th?ng s? l?y T?T C? nhn vin trong Database d? n?p vo danh sch ch?n ngu?i nh?n.
            ViewBag.Employees = await _context.Employees
                                    .OrderBy(e => e.Name)
                                    .ToListAsync();

            ViewBag.ReturnTo = returnTo;

            List<ThongBaoTam> hienThi;

            // ?? LOGIC L?C THNG MINH:
            if (role == "HR" || role == "Leader" || role == "Manager")
            {
                // Qu?n l: Th?y ton b? thng bo d g?i, NGO?I TR? thng bo luong c nhn c?a nhn vin
                // V ch? th?y thng bo nh?c luong c?a chnh mnh
                hienThi = list.Where(x => 
                    x.Type != "SALARY_PAID" && 
                    (x.Type != "SALARY_PAYMENT_REMINDER" || 
                     x.NguoiNhan == user || 
                     (!string.IsNullOrEmpty(realName) && x.NguoiNhan.Trim().ToLower() == realName.Trim().ToLower()))
                ).OrderByDescending(x => x.NgayTao).ToList();
            }
            else
            {
                // Nhn vin: Th?y tin "T?t c?" HO?C tin g?i dng "Tn dang nh?p" HO?C "H? tn th?t"
                hienThi = list.Where(x =>
                    x.NguoiNhan == "T?t c?" ||
                    x.NguoiNhan == "T?t c? nhn vin" ||
                    x.NguoiNhan == user ||
                    (!string.IsNullOrEmpty(realName) && x.NguoiNhan.Trim().ToLower() == realName.Trim().ToLower())
                ).OrderByDescending(x => x.NgayTao).ToList();
            }

            return View(hienThi);
        }

        // --- 2. G?I THNG BO (T? HR/ADMIN) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiThongBao(string noiDung, string nguoiNhan, string? returnTo)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");

            // L?y tn ngu?i g?i (Uu tin tn th?t)
            var userGui = HttpContext.Session.GetString("RealName") ?? HttpContext.Session.GetString("User") ?? "H? th?ng";
            var roleGui = HttpContext.Session.GetString("Role");

            if (roleGui != "HR" && roleGui != "Admin" && roleGui != "Leader")
            {
                return Json(new { success = false, message = "B?n khng c quy?n g?i!" });
            }

            if (!string.IsNullOrWhiteSpace(noiDung))
            {
                var tbMoi = new ThongBaoTam
                {
                    Id = list.Any() ? list.Max(x => x.Id) + 1 : 1,
                    NoiDung = noiDung,
                    NguoiGui = userGui,
                    NguoiNhan = string.IsNullOrEmpty(nguoiNhan) ? "T?t c? nhn vin" : nguoiNhan,
                    NgayTao = DateTime.Now
                };
                list.Add(tbMoi);

                var realtimePayload = new
                {
                    content = tbMoi.NoiDung,
                    sender = tbMoi.NguoiGui,
                    receiver = tbMoi.NguoiNhan,
                    time = tbMoi.NgayTao.ToString("HH:mm")
                };
                var canonicalRecipient = await _context.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Username == tbMoi.NguoiNhan && u.CanLogin);
                if (canonicalRecipient != null)
                    await _hubContext.Clients.User(canonicalRecipient.Username).SendAsync("ReceiveNotification", realtimePayload);
                else if (string.IsNullOrWhiteSpace(nguoiNhan))
                    await _hubContext.Clients.Group("role:Employee").SendAsync("ReceiveNotification", realtimePayload);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Ok();
            }

            return RedirectToAction("Index", new { returnTo = returnTo });
        }

        // --- 3. THU H?I THNG BO ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecallByName(string noiDung)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "HR" && role != "Leader" && role != "Manager") return Forbid();

            var item = list.FirstOrDefault(x => x.NoiDung == noiDung);
            if (item != null)
            {
                list.Remove(item);
                var canonicalRecipient = await _context.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Username == item.NguoiNhan && u.CanLogin);
                if (canonicalRecipient != null)
                    await _hubContext.Clients.User(canonicalRecipient.Username).SendAsync("NotificationRecalled", noiDung);
                return Ok();
            }
            return NotFound();
        }

        // --- 4. XA THNG BO ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "HR" && role != "Leader" && role != "Manager") return Forbid();

            var item = list.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                list.Remove(item);
                return Ok();
            }
            return NotFound();
        }

        // --- 4.1. NH D?U  ?C ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAsRead(int id)
        {
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.IsRead = true;
                return Ok();
            }
            return NotFound();
        }

        // --- 5. L?Y S? LU?NG CHO CHUNG THNG BO (BADGE) ---
        public JsonResult GetNotifications()
        {
            var user = HttpContext.Session.GetString("User") ?? "";
            var role = HttpContext.Session.GetString("Role");
            var realName = HttpContext.Session.GetString("RealName") ?? "";

            int count;
            if (role == "HR" || role == "Leader" || role == "Manager")
            {
                // Qu?n l khng d?m thng bo luong c?a nhn vin
                // V ch? d?m thng bo nh?c luong c?a chnh mnh
                count = list.Count(x => 
                    x.Type != "SALARY_PAID" && 
                    (x.Type != "SALARY_PAYMENT_REMINDER" || 
                     x.NguoiNhan == user || 
                     (!string.IsNullOrEmpty(realName) && x.NguoiNhan.Trim().ToLower() == realName.Trim().ToLower()))
                );
            }
            else
            {
                count = list.Count(x =>
                    x.NguoiNhan == "T?t c?" ||
                    x.NguoiNhan == "T?t c? nhn vin" ||
                    x.NguoiNhan == user ||
                    (!string.IsNullOrEmpty(realName) && x.NguoiNhan.Trim().ToLower() == realName.Trim().ToLower())
                );
            }

            return Json(new { count = count });
        }
    }
}
