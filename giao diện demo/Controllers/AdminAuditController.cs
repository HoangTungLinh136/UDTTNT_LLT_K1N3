using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Text;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AdminAuditController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminAuditController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetLogs(
            string tab = "activity", 
            DateTime? from = null, 
            DateTime? to = null, 
            string user = null, 
            string module = null, 
            string actionStr = null, 
            string status = null, 
            string search = null,
            int page = 1, 
            int pageSize = 20)
        {
            var query = _context.AuditLogs.AsQueryable();

            // Lọc theo Tab
            if (tab == "activity")
            {
                // Activity bao gồm tất cả trừ Login và Security (nếu muốn tách)
                // Hoặc activity = DataChange + Activity
                query = query.Where(x => x.EventType != "Login" && x.EventType != "Security");
            }
            else if (tab == "login")
            {
                query = query.Where(x => x.EventType == "Login");
            }
            else if (tab == "datachange")
            {
                query = query.Where(x => x.EventType == "DataChange");
            }
            else if (tab == "security")
            {
                query = query.Where(x => x.EventType == "Security");
            }

            // Lọc theo thời gian
            if (from.HasValue) query = query.Where(x => x.Timestamp >= from.Value);
            if (to.HasValue) 
            {
                var toDate = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.Timestamp <= toDate);
            }

            // Lọc các field khác
            if (!string.IsNullOrEmpty(user)) query = query.Where(x => x.Username.Contains(user) || x.UserId == user);
            if (!string.IsNullOrEmpty(module)) query = query.Where(x => x.Module == module);
            if (!string.IsNullOrEmpty(actionStr)) query = query.Where(x => x.Action == actionStr);
            if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);

            // Tìm kiếm chung
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x => 
                    x.Resource.Contains(search) || 
                    x.Action.Contains(search) || 
                    x.Module.Contains(search) ||
                    x.ResourceId.Contains(search)
                );
            }

            // Sắp xếp
            query = query.OrderByDescending(x => x.Timestamp);

            var total = await query.CountAsync();
            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Json(new { total, data, page, pageSize });
        }

        [HttpGet]
        public async Task<IActionResult> GetDetail(long id)
        {
            var log = await _context.AuditLogs.FirstOrDefaultAsync(x => x.Id == id);
            if (log == null) return NotFound();

            // Loại bỏ các trường nhạy cảm trong OldValues / NewValues
            if (!string.IsNullOrEmpty(log.OldValues)) log.OldValues = SanitizeJson(log.OldValues);
            if (!string.IsNullOrEmpty(log.NewValues)) log.NewValues = SanitizeJson(log.NewValues);

            return Json(log);
        }

        private string SanitizeJson(string json)
        {
            // Thay thế các field nhạy cảm
            var sensitiveKeys = new[] { "\"Password\"", "\"PasswordHash\"", "\"Token\"", "\"Secret\"" };
            foreach (var key in sensitiveKeys)
            {
                // Regex thay thế đơn giản (có thể dùng System.Text.Json để parse và xoá an toàn hơn)
                json = System.Text.RegularExpressions.Regex.Replace(json, key + @"\s*:\s*""[^""]*""", key + ": \"[REDACTED]\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            return json;
        }

        [HttpGet]
        public async Task<IActionResult> GetSummary()
        {
            var today = DateTime.Today;

            var totalToday = await _context.AuditLogs.CountAsync(x => x.Timestamp >= today);
            var loginFailedToday = await _context.AuditLogs.CountAsync(x => x.Timestamp >= today && x.EventType == "Login" && x.Status == "Failed");
            var dataChangeToday = await _context.AuditLogs.CountAsync(x => x.Timestamp >= today && x.EventType == "DataChange");
            var securityToday = await _context.AuditLogs.CountAsync(x => x.Timestamp >= today && x.EventType == "Security");

            return Json(new {
                totalToday,
                loginFailedToday,
                dataChangeToday,
                securityToday
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            string tab = "activity", 
            DateTime? from = null, 
            DateTime? to = null, 
            string user = null, 
            string module = null, 
            string actionStr = null, 
            string status = null, 
            string search = null)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (tab == "activity") query = query.Where(x => x.EventType != "Login" && x.EventType != "Security");
            else if (tab == "login") query = query.Where(x => x.EventType == "Login");
            else if (tab == "datachange") query = query.Where(x => x.EventType == "DataChange");
            else if (tab == "security") query = query.Where(x => x.EventType == "Security");

            if (from.HasValue) query = query.Where(x => x.Timestamp >= from.Value);
            if (to.HasValue) 
            {
                var toDate = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.Timestamp <= toDate);
            }

            if (!string.IsNullOrEmpty(user)) query = query.Where(x => x.Username.Contains(user) || x.UserId == user);
            if (!string.IsNullOrEmpty(module)) query = query.Where(x => x.Module == module);
            if (!string.IsNullOrEmpty(actionStr)) query = query.Where(x => x.Action == actionStr);
            if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x => 
                    x.Resource.Contains(search) || 
                    x.Action.Contains(search) || 
                    x.Module.Contains(search) ||
                    x.ResourceId.Contains(search)
                );
            }

            query = query.OrderByDescending(x => x.Timestamp);

            // Giới hạn max record export
            var data = await query.Take(10000).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("ID,Timestamp,Username,Role,Module,Action,Resource,ResourceId,EventType,Status,IPAddress");
            
            foreach (var item in data)
            {
                sb.AppendLine($"\"{item.Id}\",\"{item.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{item.Username}\",\"{item.Role}\",\"{item.Module}\",\"{item.Action}\",\"{item.Resource}\",\"{item.ResourceId}\",\"{item.EventType}\",\"{item.Status}\",\"{item.IPAddress}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"AuditLog_{DateTime.Now:yyyyMMddHHmmss}.csv");
        }
    }
}
