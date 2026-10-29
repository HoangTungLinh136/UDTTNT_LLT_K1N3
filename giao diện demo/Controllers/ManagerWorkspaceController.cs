using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.ManagerOnly)]
    public class ManagerWorkspaceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IManagerScopeService _scope;
        private readonly IApprovalEngine _approvalEngine;
        private readonly LeaveWorkflowPolicy _leavePolicy;
        private readonly AdjustmentWorkflowPolicy _adjustmentPolicy;
        private readonly IInAppNotificationService _notifications;

        public ManagerWorkspaceController(ApplicationDbContext context, IManagerScopeService scope,
            IApprovalEngine approvalEngine, LeaveWorkflowPolicy leavePolicy, AdjustmentWorkflowPolicy adjustmentPolicy,
            IInAppNotificationService notifications)
        {
            _context = context;
            _scope = scope;
            _approvalEngine = approvalEngine;
            _leavePolicy = leavePolicy;
            _adjustmentPolicy = adjustmentPolicy;
            _notifications = notifications;
        }

        private bool IsManager => (CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role")) == AppRoles.Manager;
        private string Username => CurrentActor.Username(User) ?? HttpContext.Session.GetString("User") ?? "";
        private IActionResult Denied() => string.IsNullOrEmpty(Username) ? RedirectToAction("Login", "Account") : Forbid();

        /// <summary>
        /// Shows the authenticated manager's linked Employee profile.  The
        /// EmployeeId is never accepted from the request; ManagerAssignment is
        /// used only to display the current management scope card.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (!IsManager) return Denied();

            var units = _scope.GetManagedOrganizationalUnits()
                .OrderBy(x => x)
                .ToList();
            var actor = await CurrentActor.ResolveUserAsync(User, _context);
            var model = new ManagerProfileViewModel
            {
                Username = actor?.Username ?? Username,
                AccountRole = actor?.Role ?? AppRoles.Manager,
                EmployeeId = actor?.EmployeeId,
                ActiveOrganizationalUnits = units
            };

            if (actor?.EmployeeId == null)
            {
                model.Message = "Tài khoản Manager chưa được liên kết hồ sơ nhân viên.";
                return View(model);
            }

            var employee = await _context.Employees
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == actor.EmployeeId.Value);
            if (employee == null)
            {
                model.Message = "Không tìm thấy hồ sơ nhân viên được liên kết.";
                return View(model);
            }

            model.HasLinkedEmployee = true;
            model.EmployeeCode = employee.EmployeeCode;
            model.Name = employee.Name;
            model.Email = employee.Email;
            model.PhoneNumber = employee.PhoneNumber;
            model.Address = employee.Address;
            model.Gender = employee.Gender;
            model.BirthDate = employee.BirthDate;
            model.AvatarPath = employee.AvatarPath;
            model.Department = employee.Department;
            model.Position = employee.Position;
            model.JobTitle = employee.JobTitle;
            model.EmploymentStatus = employee.Status;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? year = null, string unit = "")
        {
            if (!IsManager) return Denied();
            try
            {
                var actor = await CurrentActor.ResolveUserAsync(User, _context);
                if (actor == null || actor.Role != AppRoles.Manager) return Denied();
                return View(await new ManagerDashboardService(_context, _scope).LoadAsync(actor, year, unit));
            }
            catch (Exception ex) when (ex is System.Data.Common.DbException || ex is TimeoutException)
            {
                HttpContext.RequestServices.GetService<ILogger<ManagerWorkspaceController>>()?
                    .LogError(ex, "Unable to load Manager Dashboard");
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return View("DashboardUnavailable", new ManagerWorkspaceViewModel { ErrorMessage = "Không thể tải dữ liệu tổng quan. Vui lòng thử lại sau." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> EmployeeDetails(int id)
        {
            if (!IsManager) return Denied();
            var actor = await CurrentActor.ResolveUserAsync(User, _context);
            if (actor == null || actor.Role != AppRoles.Manager) return Denied();
            var employee = await new ManagerDashboardService(_context, _scope).FindEmployeeAsync(id);
            return employee == null ? NotFound() : View(employee);
        }

        public async Task<IActionResult> Employees(string search = "", string department = "", int page = 1)
        {
            if (!IsManager) return Denied();
            var hasManagerScope = _scope.GetManagedOrganizationalUnits().Any();
            var query = _scope.GetScopedEmployeeQuery().Include(e => e.Manager).AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(e => (e.Name ?? "").Contains(search) || (e.EmployeeCode ?? "").Contains(search));
            if (!string.IsNullOrWhiteSpace(department))
            {
                var allowed = _scope.GetManagedOrganizationalUnits();
                query = allowed.Contains(department) ? query.Where(e => e.Department == department) : query.Where(e => false);
            }
            const int pageSize = 10;
            page = Math.Max(1, page);
            var totalItems = await query.CountAsync();
            var items = await query.OrderBy(e => e.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return View(new ManagerListViewModel<Employee>
            {
                Search = search,
                Filter = department,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                HasManagerScope = hasManagerScope,
                Items = items
            });
        }

        public async Task<IActionResult> Attendance(string search = "", string department = "", int page = 1)
        {
            if (!IsManager) return Denied();
            var hasManagerScope = _scope.GetManagedOrganizationalUnits().Any();
            var employeeQuery = _scope.GetScopedEmployeeQuery().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(department))
            {
                var allowed = _scope.GetManagedOrganizationalUnits();
                employeeQuery = allowed.Contains(department) ? employeeQuery.Where(e => e.Department == department) : employeeQuery.Where(e => false);
            }
            if (!string.IsNullOrWhiteSpace(search))
                employeeQuery = employeeQuery.Where(e => (e.Name ?? "").Contains(search) || (e.EmployeeCode ?? "").Contains(search));

            var attendanceQuery = _context.Attendances.AsNoTracking()
                .Where(a => a.EmployeeId.HasValue && employeeQuery.Select(e => e.Id).Contains(a.EmployeeId.Value));
            const int pageSize = 10;
            page = Math.Max(1, page);
            var totalItems = await attendanceQuery.CountAsync();
            var rows = await attendanceQuery.OrderByDescending(a => a.CheckIn)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new ManagerAttendanceRow
                {
                    Id = a.Id,
                    EmployeeId = a.EmployeeId,
                    EmployeeName = a.Employee != null ? (a.Employee.Name ?? a.EmployeeNameSnapshot ?? "") : (a.EmployeeNameSnapshot ?? ""),
                    EmployeeCode = a.Employee != null ? (a.Employee.EmployeeCode ?? "") : "",
                    Department = a.Employee != null ? (a.Employee.Department ?? "") : "",
                    CheckIn = a.CheckIn,
                    CheckOut = a.CheckOut,
                    Status = a.StatusNote ?? ""
                }).ToListAsync();
            return View(new ManagerListViewModel<ManagerAttendanceRow>
            {
                Search = search,
                Filter = department,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                HasManagerScope = hasManagerScope,
                OrganizationalUnits = _scope.GetManagedOrganizationalUnits().OrderBy(x => x).ToList(),
                Items = rows
            });
        }

        public Task<IActionResult> LeaveApprovals(string search = "", int page = 1) => ApprovalList("LEAVE", search, page);
        public Task<IActionResult> AttendanceAdjustments(string search = "", int page = 1) => ApprovalList("ADJUSTMENT", search, page);

        private async Task<IActionResult> ApprovalList(string type, string search, int page)
        {
            if (!IsManager) return Denied();
            var employees = await _scope.GetScopedEmployeeQuery().AsNoTracking().ToListAsync();
            var ids = employees.Select(e => e.Id).ToList(); var codes = employees.Select(e => e.EmployeeCode).Where(x => x != null).Cast<string>().ToList();
            List<ManagerApprovalRow> rows = type == "LEAVE"
                ? (await AssignedLeaves(ids).AsNoTracking().ToListAsync()).Select(ToLeaveRow).ToList()
                : (await AssignedAdjustments(codes).AsNoTracking().ToListAsync()).Select(a => ToAdjustmentRow(a, employees)).ToList();
            if (!string.IsNullOrWhiteSpace(search)) rows = rows.Where(x => x.EmployeeName.Contains(search, StringComparison.OrdinalIgnoreCase) || x.EmployeeCode.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Code.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            ViewBag.ApprovalType = type;
            var model = Paginate(rows.OrderByDescending(x => x.SubmittedAt).ToList(), search, "", page);
            model.HasManagerScope = employees.Count > 0 || _scope.GetManagedOrganizationalUnits().Any();
            return View("Approvals", model);
        }

        [HttpGet]
        public async Task<IActionResult> ApprovalDetail(string type, int id)
        {
            if (!IsManager) return Forbid();
            var employees = await _scope.GetScopedEmployeeQuery().AsNoTracking().ToListAsync();
            if (type == "LEAVE") { var item = await AssignedLeaves(employees.Select(e => e.Id).ToList()).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id); return item == null ? NotFound() : Json(ToLeaveRow(item)); }
            if (type == "ADJUSTMENT") { var item = await AssignedAdjustments(employees.Select(e => e.EmployeeCode).Where(x => x != null).Cast<string>().ToList()).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id); return item == null ? NotFound() : Json(ToAdjustmentRow(item, employees)); }
            return BadRequest();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decide(string type, int id, string decision, string? reason)
        {
            if (!IsManager) return Forbid();
            var employees = await _scope.GetScopedEmployeeQuery().AsNoTracking().ToListAsync();
            (bool Success, string Message) result;
            if (type == "LEAVE") 
            { 
                var item = await AssignedLeaves(employees.Select(e => e.Id).ToList()).FirstOrDefaultAsync(x => x.Id == id); 
                if (item == null) return NotFound(); 
                result = await _approvalEngine.ProcessDecisionAsync(item, decision, Username, AppRoles.Manager, reason, _leavePolicy); 
                if (result.Success && item.EmployeeId.HasValue)
                {
                    var empUser = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == item.EmployeeId.Value && u.CanLogin);
                    if (empUser != null)
                    {
                        var normDec = decision?.Trim().ToUpperInvariant();
                        var actionText = normDec == "APPROVE" ? "đã được duyệt" : "đã bị từ chối";
                        await _notifications.SendAsync(empUser.Username, $"Đơn nghỉ phép của bạn {actionText}.", "LEAVE_RESULT", $"/Leave/Details/{item.Id}", item.Id, deduplicationKey: $"leave-result:{item.Id}:{normDec}");
                    }
                }
            }
            else if (type == "ADJUSTMENT") 
            { 
                var item = await AssignedAdjustments(employees.Select(e => e.EmployeeCode).Where(x => x != null).Cast<string>().ToList()).FirstOrDefaultAsync(x => x.Id == id); 
                if (item == null) return NotFound(); 
                result = await _approvalEngine.ProcessDecisionAsync(item, decision, Username, AppRoles.Manager, reason, _adjustmentPolicy); 
                if (result.Success)
                {
                    var targetEmp = employees.FirstOrDefault(e => e.EmployeeCode == item.EmployeeCode);
                    if (targetEmp != null)
                    {
                        var empUser = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == targetEmp.Id && u.CanLogin);
                        if (empUser != null)
                        {
                            var normDec = decision?.Trim().ToUpperInvariant();
                            var actionText = normDec == "APPROVE" ? "đã được duyệt" : "đã bị từ chối";
                            await _notifications.SendAsync(empUser.Username, $"Yêu cầu điều chỉnh công của bạn {actionText}.", "ATTENDANCE_ADJUSTMENT_RESULT", "/MyRequest", item.Id, deduplicationKey: $"adj-result:{item.Id}:{normDec}");
                        }
                    }
                }
            }
            else return BadRequest();
            return result.Success ? Ok(new { success = true }) : BadRequest(new { success = false, message = result.Message });
        }

        public async Task<IActionResult> Reports(string search = "", int page = 1)
        {
            if (!IsManager) return Denied();
            var employees = await _scope.GetScopedEmployeeQuery().AsNoTracking().ToListAsync(); var today = DateTime.Today;
            var employeeIds = employees.Select(e => e.Id).ToList();
            var employeeCodes = employees.Select(e => e.EmployeeCode).Where(code => code != null).Cast<string>().ToList();
            var attendance = await _context.Attendances.AsNoTracking()
                .Where(a => a.CheckIn >= today && a.CheckIn < today.AddDays(1)
                    && a.EmployeeId != null && employeeIds.Contains(a.EmployeeId.Value))
                .ToListAsync();
            var pendingLeaves = await _context.Leaves.AsNoTracking()
                .Where(l => l.EmployeeId != null && employeeIds.Contains(l.EmployeeId.Value) && (l.Status == "Chờ duyệt" || l.Status == "PENDING"))
                .Select(l => l.EmployeeId!.Value).ToListAsync();
            var pendingAdjustments = await _context.AttendanceAdjustmentRequests.AsNoTracking()
                .Where(a => employeeCodes.Contains(a.EmployeeCode) && (a.Status == "PENDING" || a.Status == "Chờ duyệt"))
                .Select(a => a.EmployeeCode).ToListAsync();
            var rows = employees.GroupBy(e => e.Department ?? "Chưa phân phòng").Select(g =>
            {
                var ids = g.Select(e => e.Id).ToHashSet();
                var codes = g.Select(e => e.EmployeeCode).Where(c => c != null).Cast<string>().ToHashSet();
                return new ManagerReportRow
                {
                    Department = g.Key,
                    Employees = g.Count(),
                    PresentToday = attendance.Count(a => a.EmployeeId.HasValue && ids.Contains(a.EmployeeId.Value)),
                    LateToday = attendance.Count(a => a.EmployeeId.HasValue && ids.Contains(a.EmployeeId.Value) && a.LateMinutes > 0),
                    PendingLeaves = pendingLeaves.Count(id => ids.Contains(id)),
                    PendingAdjustments = pendingAdjustments.Count(code => codes.Contains(code))
                };
            }).ToList();
            if (!string.IsNullOrWhiteSpace(search)) rows = rows.Where(x => x.Department.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            var model = Paginate(rows, search, "", page);
            model.HasManagerScope = _scope.GetManagedOrganizationalUnits().Any();
            return View(model);
        }

        public async Task<IActionResult> Notifications(string search = "", int page = 1)
        {
            if (!IsManager) return Denied();
            var currentUserId = await _context.Users.Where(x => x.Username == Username)
                .Select(x => (int?)x.Id).FirstOrDefaultAsync();
            if (!currentUserId.HasValue) return Denied();
            var query = _context.Notifications.AsNoTracking()
                .Where(n => n.RecipientUserId == currentUserId.Value);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(n => n.Message != null && n.Message.Contains(search));
            var rows = await query.OrderByDescending(n => n.CreatedAt)
                .Select(n => new ThongBaoTam
                {
                    Id = n.Id,
                    NoiDung = n.Message ?? "",
                    NguoiGui = n.SenderName ?? "Hệ thống",
                    NguoiNhan = n.NguoiNhan ?? "",
                    NgayTao = n.CreatedAt,
                    Type = n.NotificationType ?? "SYSTEM",
                    TargetUrl = n.TargetUrl,
                    IsRead = n.IsRead,
                    ReferenceId = n.ReferenceId
                }).ToListAsync();
            return View(Paginate(rows, search, "", page));
        }

        private IQueryable<Leave> AssignedLeaves(List<int> employeeIds) => _context.Leaves.Include(l => l.Employee).Where(l => (l.Status == "Chờ duyệt" || l.Status == "PENDING") && l.EmployeeId != null && employeeIds.Contains(l.EmployeeId.Value));
        private IQueryable<AttendanceAdjustmentRequest> AssignedAdjustments(List<string> employeeCodes) => _context.AttendanceAdjustmentRequests.Where(a => (a.Status == "PENDING" || a.Status == "Chờ duyệt") && employeeCodes.Contains(a.EmployeeCode));
        private static ManagerApprovalRow ToLeaveRow(Leave l) => new() { Type = "LEAVE", Id = l.Id, Code = $"NP{l.Id:D4}", EmployeeName = l.Employee?.Name ?? l.EmployeeNameSnapshot ?? "", EmployeeCode = l.Employee?.EmployeeCode ?? "", Department = l.Employee?.Department ?? "", Summary = $"{l.LeaveType}: {l.FromDate:dd/MM/yyyy} - {l.ToDate:dd/MM/yyyy}", Status = "Chờ duyệt", SubmittedAt = l.CreatedDate };
        private static ManagerApprovalRow ToAdjustmentRow(AttendanceAdjustmentRequest a, List<Employee> employees) { var e = employees.FirstOrDefault(x => x.EmployeeCode == a.EmployeeCode); return new() { Type = "ADJUSTMENT", Id = a.Id, Code = $"DC{a.Id:D4}", EmployeeName = e?.Name ?? "", EmployeeCode = a.EmployeeCode, Department = e?.Department ?? "", Summary = a.Reason, Status = "Chờ duyệt", SubmittedAt = a.CreatedAt }; }
        private static ManagerListViewModel<T> Paginate<T>(List<T> items, string search, string filter, int page) { const int size = 10; page = Math.Max(1, page); return new() { Search = search, Filter = filter, Page = page, PageSize = size, TotalItems = items.Count, Items = items.Skip((page - 1) * size).Take(size).ToList() }; }
    }
}
