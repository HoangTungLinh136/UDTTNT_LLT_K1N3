using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Hubs;
using System;
using System.Globalization; 
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;
        private readonly giao_dien_demo.Services.IApprovalEngine _approvalEngine;
        private readonly giao_dien_demo.Services.LeaveWorkflowPolicy _leavePolicy;
        private readonly giao_dien_demo.Services.IManagerScopeService _managerScope;
        private readonly giao_dien_demo.Services.IInAppNotificationService _notifications;
        private readonly giao_dien_demo.Services.ILeaveService _leaveService;

        public LeaveController(
            IHubContext<DashboardHub> hubContext, 
            ApplicationDbContext context, 
            giao_dien_demo.Services.IAuditLogService auditLogService,
            giao_dien_demo.Services.IApprovalEngine approvalEngine,
            giao_dien_demo.Services.LeaveWorkflowPolicy leavePolicy,
            giao_dien_demo.Services.IManagerScopeService managerScope,
            giao_dien_demo.Services.IInAppNotificationService notifications,
            giao_dien_demo.Services.ILeaveService leaveService)
        {
            _hubContext = hubContext;
            _context = context;
            _auditLogService = auditLogService;
            _approvalEngine = approvalEngine;
            _leavePolicy = leavePolicy;
            _managerScope = managerScope;
            _notifications = notifications;
            _leaveService = leaveService;
        }

        private bool CheckLogin()
        {
            var user = HttpContext.Session.GetString("User");
            return !string.IsNullOrEmpty(user);
        }

        public async Task<IActionResult> Index(
            string? search,
            string? department,
            string? leaveType,
            string? status,
            string? fromDate,
            string? toDate,
            int page = 1,
            int pageSize = 10)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");

            var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role") ?? "";

            var model = new HRLeaveIndexViewModel
            {
                CurrentRole = role,
                Search = search,
                Department = department,
                LeaveType = leaveType,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                PageNumber = page < 1 ? 1 : page,
                PageSize = pageSize <= 0 ? 10 : (pageSize > 100 ? 100 : pageSize)
            };

            IQueryable<Leave> query = _context.Leaves.Include(l => l.Employee).AsNoTracking();

            // Enforce Actor Data Scope
            if (role == AppRoles.HR)
            {
                // HR monitors company-wide
            }
            else if (role == AppRoles.Manager)
            {
                var managedDepts = _managerScope.GetManagedOrganizationalUnits();
                query = query.Where(l => l.Employee != null && l.Employee.Department != null && managedDepts.Contains(l.Employee.Department));
            }
            else if (role == AppRoles.Leader)
            {
                var leaderScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                var leaderDepts = leaderScope.GetOverseenOrganizationalUnits();
                query = query.Where(l => l.Employee != null && l.Employee.Department != null && leaderDepts.Contains(l.Employee.Department));
            }
            else
            {
                if (currentUser?.EmployeeId == null) return Forbid();
                query = query.Where(l => l.EmployeeId == currentUser.EmployeeId.Value);
            }

            // Real-time Summary KPIs from Database
            var today = DateTime.Today;
            if (role == AppRoles.HR)
            {
                model.Summary.TotalRequests = await _context.Leaves.CountAsync();
                model.Summary.PendingRequests = await _context.Leaves.CountAsync(l => l.Status == "Chờ duyệt" || l.Status == "PENDING");
                model.Summary.ApprovedRequests = await _context.Leaves.CountAsync(l => l.Status == "Đã duyệt" || l.Status == "APPROVED");
                model.Summary.RejectedRequests = await _context.Leaves.CountAsync(l => l.Status == "Đã từ chối" || l.Status == "Từ chối" || l.Status == "REJECTED");
                model.Summary.OnLeaveToday = await _context.Leaves.CountAsync(l =>
                    (l.Status == "Đã duyệt" || l.Status == "APPROVED") &&
                    l.FromDate.Date <= today && l.ToDate.Date >= today);
            }
            else
            {
                model.Summary.TotalRequests = await query.CountAsync();
                model.Summary.PendingRequests = await query.CountAsync(l => l.Status == "Chờ duyệt" || l.Status == "PENDING");
                model.Summary.ApprovedRequests = await query.CountAsync(l => l.Status == "Đã duyệt" || l.Status == "APPROVED");
                model.Summary.RejectedRequests = await query.CountAsync(l => l.Status == "Đã từ chối" || l.Status == "Từ chối" || l.Status == "REJECTED");
                model.Summary.OnLeaveToday = await query.CountAsync(l =>
                    (l.Status == "Đã duyệt" || l.Status == "APPROVED") &&
                    l.FromDate.Date <= today && l.ToDate.Date >= today);
            }

            // Apply Search Filter (EmployeeCode, Name, Reason)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(l =>
                    (l.Employee != null && ((l.Employee.EmployeeCode != null && l.Employee.EmployeeCode.ToLower().Contains(s)) || (l.Employee.Name != null && l.Employee.Name.ToLower().Contains(s)))) ||
                    (l.EmployeeNameSnapshot != null && l.EmployeeNameSnapshot.ToLower().Contains(s)) ||
                    (l.Reason != null && l.Reason.ToLower().Contains(s)));
            }

            // Apply Department Filter
            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(l => l.Employee != null && l.Employee.Department == department);
            }

            // Apply Leave Type Filter
            if (!string.IsNullOrWhiteSpace(leaveType))
            {
                query = query.Where(l => l.LeaveType == leaveType);
            }

            // Apply Status Filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "Chờ duyệt")
                {
                    query = query.Where(l => l.Status == "Chờ duyệt" || l.Status == "PENDING");
                }
                else if (status == "Đã duyệt")
                {
                    query = query.Where(l => l.Status == "Đã duyệt" || l.Status == "APPROVED");
                }
                else if (status == "Đã từ chối")
                {
                    query = query.Where(l => l.Status == "Đã từ chối" || l.Status == "Từ chối" || l.Status == "REJECTED");
                }
                else if (status == "Đã hủy")
                {
                    query = query.Where(l => l.Status == "Đã hủy" || l.Status == "CANCELLED");
                }
                else
                {
                    query = query.Where(l => l.Status == status);
                }
            }

            // Apply Date Range Filter (Overlap semantics: From <= FilterTo and To >= FilterFrom)
            if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fDate))
            {
                query = query.Where(l => l.ToDate.Date >= fDate.Date);
            }

            if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tDate))
            {
                query = query.Where(l => l.FromDate.Date <= tDate.Date);
            }

            // Server-side Pagination
            model.TotalItems = await query.CountAsync();
            var pagedEntities = await query
                .OrderByDescending(l => l.CreatedDate)
                .Skip((model.PageNumber - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToListAsync();

            model.Items = pagedEntities.Select(l => new LeaveItemViewModel
            {
                Id = l.Id,
                EmployeeId = l.EmployeeId,
                EmployeeCode = l.Employee?.EmployeeCode ?? "---",
                EmployeeName = l.Employee?.Name ?? l.EmployeeNameSnapshot ?? "---",
                Department = l.Employee?.Department ?? "---",
                AvatarUrl = l.Employee?.AvatarPath,
                LeaveType = string.IsNullOrWhiteSpace(l.LeaveType) ? "Nghỉ phép năm" : l.LeaveType,
                FromDate = l.FromDate,
                ToDate = l.ToDate,
                DurationDays = (l.ToDate.Date - l.FromDate.Date).Days + 1,
                Reason = l.Reason,
                Status = l.Status ?? "Chờ duyệt",
                ReviewedBy = l.ReviewedBy,
                ReviewedAt = l.ReviewedAt,
                CreatedDate = l.CreatedDate
            }).ToList();

            // Populate Dropdown Options from DB
            model.DepartmentOptions = await _context.Employees
                .Where(e => !string.IsNullOrEmpty(e.Department))
                .Select(e => e.Department!)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var dbLeaveTypes = await _context.Leaves
                .Where(l => !string.IsNullOrEmpty(l.LeaveType))
                .Select(l => l.LeaveType!)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
            if (!dbLeaveTypes.Contains("Nghỉ phép năm")) dbLeaveTypes.Insert(0, "Nghỉ phép năm");
            model.LeaveTypeOptions = dbLeaveTypes;

            model.StatusOptions = new List<string> { "Chờ duyệt", "Đã duyệt", "Đã từ chối", "Đã hủy" };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Export(
            string? search,
            string? department,
            string? leaveType,
            string? status,
            string? fromDate,
            string? toDate)
        {
            if (!CheckLogin()) return Unauthorized();
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role != AppRoles.HR) return Forbid();

            IQueryable<Leave> query = _context.Leaves.Include(l => l.Employee).AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(l =>
                    (l.Employee != null && ((l.Employee.EmployeeCode != null && l.Employee.EmployeeCode.ToLower().Contains(s)) || (l.Employee.Name != null && l.Employee.Name.ToLower().Contains(s)))) ||
                    (l.EmployeeNameSnapshot != null && l.EmployeeNameSnapshot.ToLower().Contains(s)) ||
                    (l.Reason != null && l.Reason.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(l => l.Employee != null && l.Employee.Department == department);
            }

            if (!string.IsNullOrWhiteSpace(leaveType))
            {
                query = query.Where(l => l.LeaveType == leaveType);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "Chờ duyệt") query = query.Where(l => l.Status == "Chờ duyệt" || l.Status == "PENDING");
                else if (status == "Đã duyệt") query = query.Where(l => l.Status == "Đã duyệt" || l.Status == "APPROVED");
                else if (status == "Đã từ chối") query = query.Where(l => l.Status == "Đã từ chối" || l.Status == "Từ chối" || l.Status == "REJECTED");
                else if (status == "Đã hủy") query = query.Where(l => l.Status == "Đã hủy" || l.Status == "CANCELLED");
                else query = query.Where(l => l.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fDate))
            {
                query = query.Where(l => l.ToDate.Date >= fDate.Date);
            }

            if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tDate))
            {
                query = query.Where(l => l.FromDate.Date <= tDate.Date);
            }

            var list = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("STT,Mã NV,Họ tên,Phòng ban,Loại nghỉ,Từ ngày,Đến ngày,Số ngày,Lý do,Trạng thái,Người duyệt,Ngày duyệt,Ngày nộp");

            int idx = 1;
            foreach (var item in list)
            {
                var empCode = item.Employee?.EmployeeCode ?? "---";
                var empName = EscapeCsv(item.Employee?.Name ?? item.EmployeeNameSnapshot ?? "---");
                var dept = EscapeCsv(item.Employee?.Department ?? "---");
                var lType = EscapeCsv(item.LeaveType ?? "Nghỉ phép năm");
                var from = item.FromDate.ToString("dd/MM/yyyy");
                var to = item.ToDate.ToString("dd/MM/yyyy");
                var duration = (item.ToDate.Date - item.FromDate.Date).Days + 1;
                var reason = EscapeCsv(item.Reason ?? "");
                var st = EscapeCsv(item.Status ?? "Chờ duyệt");
                var reviewer = EscapeCsv(item.ReviewedBy ?? "Chưa duyệt");
                var reviewedAt = item.ReviewedAt?.ToString("dd/MM/yyyy HH:mm") ?? "";
                var created = item.CreatedDate.ToString("dd/MM/yyyy HH:mm");

                csv.AppendLine($"{idx},{empCode},{empName},{dept},{lType},{from},{to},{duration},{reason},{st},{reviewer},{reviewedAt},{created}");
                idx++;
            }

            var preamble = System.Text.Encoding.UTF8.GetPreamble();
            var dataBytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var resultBytes = new byte[preamble.Length + dataBytes.Length];
            Buffer.BlockCopy(preamble, 0, resultBytes, 0, preamble.Length);
            Buffer.BlockCopy(dataBytes, 0, resultBytes, preamble.Length, dataBytes.Length);

            return File(resultBytes, "text/csv; charset=utf-8", $"DanhSachNghiPhep_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        public async Task<IActionResult> Details(int id)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            var leave = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(x => x.Id == id);
            if (leave == null) return NotFound();
            var role = CurrentActor.Role(User);
            if (role == "Manager" && !_managerScope.CanAccessDepartment(leave.Employee?.Department)) return Forbid();
            if (role == "Leader")
            {
                var leaderScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                if (!leaderScope.CanAccessDepartment(leave.Employee?.Department)) return Forbid();
            }
            if (role != "Manager" && role != "HR" && role != "Leader")
            {
                var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
                if (currentUser?.EmployeeId == null || leave.EmployeeId != currentUser.EmployeeId.Value) return Forbid();
            }
            return View(leave);
        }

        public IActionResult Create()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Leave leave)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (leave == null) return View();

            // Đọc ngày tháng định dạng dd/MM/yyyy
            string fromDateStr = Request.Form["FromDate"];
            string toDateStr = Request.Form["ToDate"];

            if (!DateTime.TryParseExact(fromDateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate)
                || !DateTime.TryParseExact(toDateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            {
                ModelState.AddModelError("", "Ngày nghỉ không đúng định dạng dd/MM/yyyy.");
                return View(leave);
            }

            var actor = await CurrentActor.ResolveUserAsync(User, _context);
            var currentEmployee = actor?.EmployeeId == null ? null : await _context.Employees.FindAsync(actor.EmployeeId.Value);
            if (currentEmployee == null)
            {
                ModelState.AddModelError("", "Tài khoản chưa được liên kết với hồ sơ nhân viên.");
                return View(leave);
            }

            string? signature = Request.Form["EmployeeSignature"];
            string? leaveType = Request.Form["LeaveType"];

            var result = await _leaveService.CreateLeaveAsync(
                currentEmployee,
                fromDate,
                toDate,
                leave.Reason,
                leaveType,
                signature,
                actor!.Username);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.ErrorMessage ?? "Đã xảy ra lỗi trong quá trình xử lý.");
                return View(leave);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!CheckLogin()) return Unauthorized();
            var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
            if (currentUser?.EmployeeId == null) return Forbid();

            var result = await _leaveService.CancelLeaveAsync(id, currentUser.EmployeeId.Value, currentUser.Username);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            if (!CheckLogin()) return Unauthorized();
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            var username = HttpContext.Session.GetString("User") ?? string.Empty;
            // Business Rule: Chỉ Manager có active scope mới có quyền duyệt đơn nghỉ phép thông thường
            if (role != AppRoles.Manager) return Forbid();

            var item = await _context.Leaves.FindAsync(id);
            if (item == null) return NotFound();

            var (success, message) = await _approvalEngine.ProcessDecisionAsync(item, "APPROVE", username, role, null, _leavePolicy);
            if (success)
            {
                var recipient = item.EmployeeId.HasValue
                    ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.EmployeeId == item.EmployeeId.Value && u.CanLogin)
                    : null;
                if (recipient != null)
                {
                    await _hubContext.Clients.User(recipient.Username).SendAsync("LeaveApproved", item.EmployeeNameSnapshot);
                    await _notifications.SendAsync(recipient.Username, "Đơn nghỉ phép của bạn đã được duyệt.", "LEAVE_RESULT", $"/Leave/Details/{item.Id}", item.Id, deduplicationKey: $"leave-result:{item.Id}:approved");
                }
                return Ok();
            }
            return StatusCode(500, message);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            if (!CheckLogin()) return Unauthorized();
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            var username = HttpContext.Session.GetString("User") ?? string.Empty;
            // Business Rule: Chỉ Manager có active scope mới có quyền từ chối đơn nghỉ phép thông thường
            if (role != AppRoles.Manager) return Forbid();

            var item = await _context.Leaves.FindAsync(id);
            if (item == null) return NotFound();

            var (success, message) = await _approvalEngine.ProcessDecisionAsync(item, "REJECT", username, role, reason, _leavePolicy);
            if (success)
            {
                var recipient = item.EmployeeId.HasValue
                    ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.EmployeeId == item.EmployeeId.Value && u.CanLogin)
                    : null;
                if (recipient != null)
                {
                    await _hubContext.Clients.User(recipient.Username).SendAsync("LeaveRejected", item.EmployeeNameSnapshot);
                    await _notifications.SendAsync(recipient.Username, $"Đơn nghỉ phép của bạn đã bị từ chối. Lý do: {reason}", "LEAVE_RESULT", $"/Leave/Details/{item.Id}", item.Id, deduplicationKey: $"leave-result:{item.Id}:rejected");
                }
                return Ok();
            }
            return StatusCode(500, message);
        }
    }
}
