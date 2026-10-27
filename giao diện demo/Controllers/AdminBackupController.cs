using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AdminBackupController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBackupService _backupService;

        public AdminBackupController(ApplicationDbContext context, IBackupService backupService)
        {
            _context = context;
            _backupService = backupService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSummary()
        {
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            
            var totalBackups = await _context.BackupRecords.CountAsync(b => !b.IsDeleted);
            var lastBackup = await _context.BackupRecords
                .Where(b => !b.IsDeleted && b.Status == "Success")
                .OrderByDescending(b => b.CompletedAt)
                .FirstOrDefaultAsync();
                
            var successThisMonth = await _context.BackupRecords.CountAsync(b => !b.IsDeleted && b.Status == "Success" && b.CreatedAt >= startOfMonth);
            var failedThisMonth = await _context.BackupRecords.CountAsync(b => !b.IsDeleted && b.Status == "Failed" && b.CreatedAt >= startOfMonth);

            return Json(new {
                total = totalBackups,
                lastBackupDate = lastBackup?.CompletedAt?.ToString("dd/MM/yyyy HH:mm"),
                lastBackupSize = lastBackup != null ? (lastBackup.SizeBytes / 1024 / 1024) + " MB" : "N/A",
                successThisMonth,
                failedThisMonth
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetConfig()
        {
            var config = await _context.BackupConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new BackupConfig { IsEnabled = false, Frequency = "Daily", ExecuteTime = new TimeSpan(2, 0, 0), RetentionDays = 7, BackupType = "Full" };
                _context.BackupConfigs.Add(config);
                await _context.SaveChangesAsync();
            }
            
            return Json(new {
                config.IsEnabled,
                config.Frequency,
                executeTime = config.ExecuteTime.ToString(@"hh\:mm"),
                config.RetentionDays,
                config.BackupType,
                nextRun = CalculateNextRun(config)
            });
        }

        private string CalculateNextRun(BackupConfig config)
        {
            if (!config.IsEnabled) return "Đã tắt";
            
            var now = DateTime.Now;
            var runDate = now.Date;
            if (now.TimeOfDay > config.ExecuteTime) runDate = runDate.AddDays(1);
            
            return runDate.Add(config.ExecuteTime).ToString("dd/MM/yyyy HH:mm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveConfig([FromBody] BackupConfig input)
        {
            var config = await _context.BackupConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new BackupConfig();
                _context.BackupConfigs.Add(config);
            }

            config.IsEnabled = input.IsEnabled;
            config.Frequency = input.Frequency;
            config.ExecuteTime = input.ExecuteTime;
            config.RetentionDays = input.RetentionDays;
            config.BackupType = input.BackupType;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory(int page = 1, int pageSize = 10, string status = "", string dateFrom = "", string dateTo = "", string type = "")
        {
            var query = _context.BackupRecords.Where(b => !b.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(status)) query = query.Where(b => b.Status == status);
            if (!string.IsNullOrEmpty(type)) query = query.Where(b => b.Type == type);
            if (DateTime.TryParse(dateFrom, out var dFrom)) query = query.Where(b => b.CreatedAt >= dFrom);
            if (DateTime.TryParse(dateTo, out var dTo)) query = query.Where(b => b.CreatedAt <= dTo.AddDays(1).AddTicks(-1));

            var total = await query.CountAsync();
            var items = await query.OrderByDescending(b => b.CreatedAt)
                                   .Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .Select(b => new {
                                       b.Id,
                                       b.FileName,
                                       b.Type,
                                       b.SizeBytes,
                                       b.Status,
                                       b.Checksum,
                                       CreatedAt = b.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                                       b.CreatedBy,
                                       CompletedAt = b.CompletedAt.HasValue ? b.CompletedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : ""
                                   })
                                   .ToListAsync();

            return Json(new { total, data = items });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TriggerBackup([FromBody] dynamic payload)
        {
            var user = User.Identity?.Name ?? "Admin";

            string type = payload?.type ?? "Full";
            try
            {
                var backupId = await _backupService.CreateBackupAsync(type, user);
                return Json(new { success = true, id = backupId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBackup(long id)
        {
            var user = User.Identity?.Name ?? "Admin";

            try
            {
                await _backupService.DeleteBackupAsync(id, user);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyBackup(long id)
        {
            try
            {
                bool isValid = await _backupService.VerifyBackupAsync(id);
                return Json(new { success = true, isValid });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TriggerRestore([FromBody] dynamic payload)
        {
            var user = User.Identity?.Name ?? "Admin";

            long id = payload?.id;
            string confirmText = payload?.confirmText;

            if (confirmText != "RESTORE")
                return Json(new { success = false, message = "Bạn phải nhập đúng chữ 'RESTORE' để xác nhận phục hồi." });

            try
            {
                await _backupService.RestoreBackupAsync(id, user);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Download(long id)
        {
            var user = User.Identity?.Name ?? "Admin";

            try
            {
                var record = await _context.BackupRecords.FindAsync(id);
                if (record == null) return NotFound();

                var stream = await _backupService.DownloadBackupAsync(id, user);
                return File(stream, "application/octet-stream", record.FileName);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }
    }
}
