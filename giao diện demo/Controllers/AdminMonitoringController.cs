using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using giao_dien_demo.Data;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AdminMonitoringController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITelemetryStore _telemetryStore;

        public AdminMonitoringController(ApplicationDbContext context, ITelemetryStore telemetryStore)
        {
            _context = context;
            _telemetryStore = telemetryStore;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GetServerMetrics()
        {
            var metrics = _telemetryStore.GetServerMetrics();
            var latest = metrics.LastOrDefault();

            return Json(new {
                latest = latest,
                history = metrics
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetDatabaseHealth()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool canConnect = await _context.Database.CanConnectAsync();
            sw.Stop();

            long activeConnections = -1;
            long databaseSizeMb = -1;

            if (canConnect)
            {
                try
                {
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(session_id) FROM sys.dm_exec_sessions WHERE status = 'running'";
                        await _context.Database.OpenConnectionAsync();
                        var connResult = await command.ExecuteScalarAsync();
                        if (connResult != null && connResult != DBNull.Value)
                        {
                            activeConnections = Convert.ToInt64(connResult);
                        }

                        command.CommandText = "SELECT SUM(size)*8/1024 FROM sys.master_files WHERE database_id = DB_ID()";
                        var sizeResult = await command.ExecuteScalarAsync();
                        if (sizeResult != null && sizeResult != DBNull.Value)
                        {
                            databaseSizeMb = Convert.ToInt64(sizeResult);
                        }
                    }
                }
                catch
                {
                    // Fallback to -1 if permissions (VIEW SERVER STATE) are missing
                }
                finally
                {
                    await _context.Database.CloseConnectionAsync();
                }
            }

            return Json(new {
                status = canConnect ? "Healthy" : "Critical",
                responseTimeMs = sw.ElapsedMilliseconds,
                activeConnections = activeConnections,
                databaseSizeMb = databaseSizeMb
            });
        }

        [HttpGet]
        public IActionResult GetApiMetrics()
        {
            var metrics = _telemetryStore.GetApiMetrics();
            var totalRequests = metrics.Sum(x => x.TotalRequests);
            var totalErrors = metrics.Sum(x => x.TotalErrors);
            var avgLatency = metrics.Any() ? metrics.Average(x => x.AverageLatencyMs) : 0;
            var errorRate = totalRequests > 0 ? (double)totalErrors / totalRequests * 100 : 0;

            return Json(new {
                summary = new { totalRequests, avgLatency, errorRate, p95 = metrics.Any() ? metrics.Max(x => x.GetP95()) : 0 },
                endpoints = metrics.OrderByDescending(x => x.TotalRequests).Take(50).ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetErrors()
        {
            var errors = await _context.SystemErrorLogs
                .OrderByDescending(e => e.Timestamp)
                .Take(100)
                .Select(e => new {
                    e.Id,
                    e.Timestamp,
                    e.Level,
                    e.Service,
                    e.ExceptionType,
                    e.Message,
                    e.RequestId,
                    e.User
                })
                .ToListAsync();

            return Json(errors);
        }

        [HttpGet]
        public async Task<IActionResult> GetErrorDetail(int id)
        {
            var error = await _context.SystemErrorLogs.FindAsync(id);
            if (error == null) return NotFound();
            return Json(error);
        }

        [HttpGet]
        public async Task<IActionResult> GetAlerts()
        {
            var alerts = await _context.MonitoringAlerts
                .OrderByDescending(a => a.StartedAt)
                .Take(100)
                .ToListAsync();
            return Json(alerts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgeAlert(int id)
        {
            var user = User.Identity?.Name ?? "Admin";

            var alert = await _context.MonitoringAlerts.FindAsync(id);
            if (alert != null && alert.Status == "Active")
            {
                alert.Status = "Acknowledged";
                alert.AcknowledgedAt = DateTime.Now;
                alert.AcknowledgedBy = user;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Không tìm thấy hoặc trạng thái không hợp lệ." });
        }
    }
}
