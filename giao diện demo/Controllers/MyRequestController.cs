using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    public class MyRequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.NotificationEngineService _notificationEngine;
        private readonly giao_dien_demo.Services.IInAppNotificationService _notifications;
        private readonly giao_dien_demo.Services.ILeaveService _leaveService;

        public MyRequestController(ApplicationDbContext context, 
            giao_dien_demo.Services.NotificationEngineService notificationEngine,
            giao_dien_demo.Services.IInAppNotificationService notifications,
            giao_dien_demo.Services.ILeaveService leaveService)
        {
            _context = context;
            _notificationEngine = notificationEngine;
            _notifications = notifications;
            _leaveService = leaveService;
        }

        private Employee? GetCurrentEmployee()
        {
            var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(username)) return null;
            var employeeId = _context.Users.AsNoTracking()
                .Where(u => u.Username == username && u.CanLogin)
                .Select(u => u.EmployeeId)
                .SingleOrDefault();
            return employeeId.HasValue ? _context.Employees.Find(employeeId.Value) : null;
        }

        public IActionResult Index(string keyword = "", string type = "", string status = "")
        {
            var username = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Account");
            
            var emp = GetCurrentEmployee();
            if (emp == null) return Forbid();

            var items = new List<MyRequestItemDto>();

            // 1. Fetch Leaves of this employee
            var leaves = _context.Leaves.Where(l => l.EmployeeId == emp.Id).ToList();
            foreach (var l in leaves)
            {
                items.Add(new MyRequestItemDto
                {
                    RequestItemId = $"LEAVE_{l.Id}",
                    RequestId = l.Id,
                    RequestType = "Nghỉ phép",
                    RequestTypeKey = "LEAVE",
                    RequestCode = $"NP{l.Id.ToString("D4")}",
                    DurationSummary = $"{(l.ToDate - l.FromDate).Days + 1} ngày ({l.FromDate:dd/MM} - {l.ToDate:dd/MM})",
                    Status = l.Status ?? "Chờ duyệt",
                    SubmittedAt = l.CreatedDate
                });
            }

            // 2. Fetch Adjustments of this employee
            if (!string.IsNullOrEmpty(emp.EmployeeCode))
            {
                var adjs = _context.AttendanceAdjustmentRequests.Where(a => a.EmployeeCode == emp.EmployeeCode).ToList();
                foreach (var a in adjs)
                {
                    items.Add(new MyRequestItemDto
                    {
                        RequestItemId = $"ADJ_{a.Id}",
                        RequestId = a.Id,
                        RequestType = "Giải trình công",
                        RequestTypeKey = "ADJUSTMENT",
                        RequestCode = $"GT{a.Id.ToString("D4")}",
                        DurationSummary = a.Reason,
                        Status = a.Status == "PENDING" ? "Chờ duyệt" : (a.Status == "APPROVED" ? "Đã duyệt" : (a.Status == "REJECTED" ? "Đã từ chối" : a.Status)),
                        SubmittedAt = a.CreatedAt,
                        RejectionReason = a.RejectionReason ?? ""
                    });
                }
            }

            // Calculate KPIs
            var vm = new MyRequestViewModel
            {
                TotalRequests = items.Count,
                PendingCount = items.Count(i => i.Status == "Chờ duyệt"),
                ApprovedCount = items.Count(i => i.Status == "Đã duyệt"),
                RejectedCount = items.Count(i => i.Status == "Đã từ chối" || i.Status == "Từ chối"),
                SearchKeyword = keyword,
                FilterType = type,
                FilterStatus = status,
                Items = items
            };

            // Apply Filters
            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.ToLower();
                vm.Items = vm.Items.Where(i => i.RequestCode.ToLower().Contains(keyword)).ToList();
            }
            if (!string.IsNullOrEmpty(type))
            {
                vm.Items = vm.Items.Where(i => i.RequestTypeKey == type).ToList();
            }
            if (!string.IsNullOrEmpty(status))
            {
                vm.Items = vm.Items.Where(i => i.Status == status).ToList();
            }

            vm.Items = vm.Items.OrderByDescending(i => i.SubmittedAt).ToList();

            return View(vm);
        }

        [HttpGet]
        public IActionResult GetDetail(string itemId)
        {
            var username = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(username)) return Unauthorized();
            
            var emp = GetCurrentEmployee();
            if (emp == null) return Forbid();

            var parts = itemId.Split('_');
            if (parts.Length != 2) return BadRequest();
            var type = parts[0];
            if (!int.TryParse(parts[1], out int id)) return BadRequest();

            // Lợi dụng ApprovalDetailDto nhưng lược bớt hoặc trả JSON custom
            var detail = new Dictionary<string, object>
            {
                { "requestItemId", itemId }
            };
            
            var requestContent = new Dictionary<string, string>();

            if (type == "LEAVE")
            {
                var l = _context.Leaves.FirstOrDefault(x => x.Id == id);
                if (l == null) return NotFound();
                
                // IDOR Protection
                if (l.EmployeeId != emp.Id) return Forbid();

                detail["requestTypeKey"] = "LEAVE";
                detail["requestCode"] = $"NP{l.Id.ToString("D4")}";
                detail["status"] = l.Status ?? "Chờ duyệt";
                detail["submittedAt"] = l.CreatedDate;

                requestContent.Add("Loại nghỉ", "Nghỉ phép thường");
                requestContent.Add("Từ ngày", l.FromDate.ToString("dd/MM/yyyy"));
                requestContent.Add("Đến ngày", l.ToDate.ToString("dd/MM/yyyy"));
                requestContent.Add("Số ngày", $"{(l.ToDate - l.FromDate).Days + 1}");
                requestContent.Add("Lý do", l.Reason ?? "");
            }
            else if (type == "ADJ")
            {
                var a = _context.AttendanceAdjustmentRequests.FirstOrDefault(x => x.Id == id);
                if (a == null) return NotFound();

                // IDOR Protection
                if (a.EmployeeCode != emp.EmployeeCode) return Forbid();

                detail["requestTypeKey"] = "ADJUSTMENT";
                detail["requestCode"] = $"GT{a.Id.ToString("D4")}";
                detail["status"] = a.Status == "PENDING" ? "Chờ duyệt" : (a.Status == "APPROVED" ? "Đã duyệt" : (a.Status == "REJECTED" ? "Đã từ chối" : a.Status));
                detail["submittedAt"] = a.CreatedAt;
                if (!string.IsNullOrEmpty(a.RejectionReason))
                {
                     detail["rejectionReason"] = a.RejectionReason;
                }

                requestContent.Add("Giờ Check-In đề xuất", a.RequestedCheckIn?.ToString(@"hh\:mm") ?? "N/A");
                requestContent.Add("Giờ Check-Out đề xuất", a.RequestedCheckOut?.ToString(@"hh\:mm") ?? "N/A");
                requestContent.Add("Lý do giải trình", a.Reason ?? "");
            }
            else
            {
                return BadRequest();
            }

            detail["requestContent"] = requestContent;
            return Json(detail);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLeave(string fromDate, string toDate, string reason)
        {
            var username = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(username)) return Unauthorized();
            
            var emp = GetCurrentEmployee();
            if (emp == null) return Forbid();

            if (!DateTime.TryParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fDate)
                || !DateTime.TryParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tDate))
            {
                return BadRequest(new { success = false, message = "Ngày nghỉ không đúng định dạng dd/MM/yyyy." });
            }

            var result = await _leaveService.CreateLeaveAsync(emp, fDate, tDate, reason, "Nghỉ phép năm", null, username);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Ok(new { success = true, id = result.Leave!.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAdjustment(string checkIn, string checkOut, string reason)
        {
            var username = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(username)) return Unauthorized();
            
            var emp = GetCurrentEmployee();
            if (emp == null || string.IsNullOrEmpty(emp.EmployeeCode)) return Unauthorized();

            var adj = new AttendanceAdjustmentRequest
            {
                EmployeeCode = emp.EmployeeCode,
                Reason = reason,
                Status = "PENDING",
                CreatedAt = DateTime.Now,
                ReviewedBy = null,
                ReviewedAt = null
            };

            if (TimeSpan.TryParse(checkIn, out TimeSpan inTime))
            {
                adj.RequestedCheckIn = inTime;
            }
            
            if (TimeSpan.TryParse(checkOut, out TimeSpan outTime))
            {
                adj.RequestedCheckOut = outTime;
            }

            _context.AttendanceAdjustmentRequests.Add(adj);
            _context.SaveChanges();

            // Gửi thông báo tới Manager(s) quản lý bộ phận của nhân viên
            if (!string.IsNullOrEmpty(emp.Department))
            {
                var now = DateTime.Now;
                var managerUserIds = _context.ManagerAssignments
                    .Where(a => a.OrganizationalUnit == emp.Department && a.EffectiveFrom <= now && (a.EffectiveTo == null || a.EffectiveTo > now))
                    .Select(a => a.UserId)
                    .Distinct()
                    .ToList();
                var managers = _context.Users.Where(u => managerUserIds.Contains(u.Id) && u.CanLogin).ToList();
                foreach (var mgr in managers)
                {
                    _ = _notifications.SendAsync(mgr.Username, $"🔔 Nhân viên {emp.Name} gửi yêu cầu giải trình/điều chỉnh công.", "ATTENDANCE_ADJUSTMENT_REQUEST", "/ManagerWorkspace/AttendanceAdjustments", adj.Id, deduplicationKey: $"adj-submitted:{adj.Id}:{mgr.Id}");
                }
            }

            return Ok(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequest(string itemId)
        {
            var username = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(username)) return Unauthorized();
            
            var emp = GetCurrentEmployee();
            if (emp == null) return Forbid();

            var parts = itemId.Split('_');
            if (parts.Length != 2) return BadRequest();
            var type = parts[0];
            if (!int.TryParse(parts[1], out int id)) return BadRequest();

            if (type == "LEAVE")
            {
                var result = await _leaveService.CancelLeaveAsync(id, emp.Id, username);
                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.ErrorMessage });
                }
                return Ok(new { success = true });
            }
            else if (type == "ADJ")
            {
                var a = await _context.AttendanceAdjustmentRequests.FirstOrDefaultAsync(x => x.Id == id);
                if (a != null)
                {
                    if (a.EmployeeCode != emp.EmployeeCode) return Forbid();
                    if (a.Status == "PENDING")
                    {
                        a.Status = "CANCELLED";
                        await _context.SaveChangesAsync();
                        return Ok(new { success = true });
                    }
                }
            }
            
            return BadRequest(new { success = false, message = "Không thể hủy đơn này." });
        }
    }
}
