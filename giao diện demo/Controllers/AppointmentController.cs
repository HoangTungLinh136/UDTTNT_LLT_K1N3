using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = "Manager,HR,Leader")]
    [AutoValidateAntiforgeryToken]
    public class AppointmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IApprovalEngine _approvalEngine;
        private readonly giao_dien_demo.Services.IWorkflowPolicy<AppointmentRecord> _policy;
        private readonly giao_dien_demo.Services.IManagerScopeService _managerScope;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _leaderScope;
        private readonly giao_dien_demo.Services.IAppointmentEffectiveService _appointmentEffectiveService;
        private readonly giao_dien_demo.Services.NotificationEngineService _notificationEngine;

        public AppointmentController(
            ApplicationDbContext context, 
            giao_dien_demo.Services.IApprovalEngine approvalEngine, 
            giao_dien_demo.Services.IWorkflowPolicy<AppointmentRecord> policy,
            giao_dien_demo.Services.IManagerScopeService managerScope,
            giao_dien_demo.Services.ILeadershipScopeService leaderScope,
            giao_dien_demo.Services.IAppointmentEffectiveService appointmentEffectiveService,
            giao_dien_demo.Services.NotificationEngineService notificationEngine)
        {
            _context = context;
            _approvalEngine = approvalEngine;
            _policy = policy;
            _managerScope = managerScope;
            _leaderScope = leaderScope;
            _appointmentEffectiveService = appointmentEffectiveService;
            _notificationEngine = notificationEngine;
        }

        private string GetCurrentUser()
        {
            return CurrentActor.Username(User) ?? HttpContext.Session.GetString("User") ?? "System";
        }

        private bool IsCurrentUser(string? actor)
        {
            return !string.IsNullOrWhiteSpace(actor) &&
                   (string.Equals(actor, GetCurrentUser(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actor, User.Identity?.Name, StringComparison.OrdinalIgnoreCase));
        }

        private string GetCurrentRole()
        {
            if (User.IsInRole(AppRoles.HR)) return AppRoles.HR;
            if (User.IsInRole(AppRoles.Leader)) return AppRoles.Leader;
            if (User.IsInRole(AppRoles.Manager)) return AppRoles.Manager;
            if (User.IsInRole(AppRoles.Admin)) return AppRoles.Admin;
            return HttpContext.Session.GetString("Role") ?? "Unknown";
        }

        private bool HasAccessToRecord(AppointmentRecord record)
        {
            var role = GetCurrentRole();
            if (role == AppRoles.HR) return true;
            if (role == AppRoles.Manager)
            {
                var allowed = _managerScope.GetManagedOrganizationalUnits();
                return record.PreviousDepartment != null && allowed.Contains(record.PreviousDepartment);
            }
            if (role == AppRoles.Leader)
            {
                var allowed = _leaderScope.GetOverseenOrganizationalUnits();
                bool canAccessPrev = !string.IsNullOrEmpty(record.PreviousDepartment) && allowed.Contains(record.PreviousDepartment);
                bool canAccessNew = !string.IsNullOrEmpty(record.NewDepartment) && allowed.Contains(record.NewDepartment);
                return canAccessPrev || canAccessNew;
            }
            return false;
        }

        private async Task QueueScopedLeaderNotificationsAsync(AppointmentRecord record, string eventCode, string action)
        {
            var departments = new[] { record.PreviousDepartment, record.NewDepartment }
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToList();
            var now = DateTime.Now;
            var leaderIds = await _context.LeaderAssignments
                .Where(a => departments.Contains(a.OrganizationalUnit)
                    && a.EffectiveFrom <= now
                    && (a.EffectiveTo == null || a.EffectiveTo > now))
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync();
            var leaders = await _context.Users
                .Where(u => leaderIds.Contains(u.Id) && u.Role == AppRoles.Leader && u.CanLogin)
                .Select(u => new { u.Id, u.Username })
                .ToListAsync();

            foreach (var leader in leaders)
            {
                _notificationEngine.QueueEvent(_context, eventCode, new
                {
                    AppointmentId = record.Id,
                    EmployeeName = record.EmployeeNameSnapshot,
                    Action = action,
                    EffectiveDate = record.EffectiveDate
                }, relatedUsername: leader.Username,
                   idempotencyKey: $"appointment:{record.Id}:{eventCode}:{leader.Id}");
            }
        }

        private void QueueProposerNotification(AppointmentRecord record, string eventCode, string action)
        {
            var proposer = record.SubmittedBy ?? record.CreatedBy;
            if (string.IsNullOrWhiteSpace(proposer)) return;

            _notificationEngine.QueueEvent(_context, eventCode, new
            {
                AppointmentId = record.Id,
                EmployeeName = record.EmployeeNameSnapshot,
                Action = action,
                EffectiveDate = record.EffectiveDate,
                RejectionReason = record.RejectionReason
            }, relatedUsername: proposer,
               idempotencyKey: $"appointment:{record.Id}:{eventCode}:{proposer}");
        }

        // 1. INDEX — Danh sách bổ nhiệm (hỗ trợ phân trang, lọc server-side)
        public async Task<IActionResult> Index(
            string? searchName,
            string? department,
            string? previousDepartment,
            string? position,
            string? status,
            string? effectiveDate,
            int pageNumber = 1,
            int pageSize = 10)
        {
            var role = GetCurrentRole();
            var query = _context.AppointmentRecords
                .Include(a => a.Employee)
                .AsQueryable();

            if (role == AppRoles.Manager)
            {
                var allowedDepts = _managerScope.GetManagedOrganizationalUnits();
                query = query.Where(a => a.PreviousDepartment != null && allowedDepts.Contains(a.PreviousDepartment));
            }
            else if (role == AppRoles.Leader)
            {
                var allowedDepts = _leaderScope.GetOverseenOrganizationalUnits();
                query = query.Where(a => (a.PreviousDepartment != null && allowedDepts.Contains(a.PreviousDepartment)) ||
                                         (a.NewDepartment != null && allowedDepts.Contains(a.NewDepartment)));
            }

            // Summary counts calculated before search/filter
            var baseQuery = query;
            var summary = new HRAppointmentSummaryViewModel
            {
                TotalRecords = await baseQuery.CountAsync(),
                PendingHR = await baseQuery.CountAsync(r => r.ApprovalStatus == "PENDING_HR_REVIEW"),
                PendingLeader = await baseQuery.CountAsync(r => r.ApprovalStatus == "PENDING_LEADER_APPROVAL"),
                Approved = await baseQuery.CountAsync(r => r.ApprovalStatus == "APPROVED" && r.AppliedAt == null),
                Effective = await baseQuery.CountAsync(r => r.ApprovalStatus == "EFFECTIVE" || r.AppliedAt != null),
                Rejected = await baseQuery.CountAsync(r => r.ApprovalStatus == "REJECTED"),
                Cancelled = await baseQuery.CountAsync(r => r.ApprovalStatus == "CANCELLED")
            };

            // Search filter
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var s = searchName.Trim().ToLower();
                query = query.Where(a => 
                    (a.EmployeeNameSnapshot != null && a.EmployeeNameSnapshot.ToLower().Contains(s)) ||
                    (a.EmployeeCodeSnapshot != null && a.EmployeeCodeSnapshot.ToLower().Contains(s)) ||
                    (a.DecisionNumber != null && a.DecisionNumber.ToLower().Contains(s)) ||
                    (a.CreatedBy != null && a.CreatedBy.ToLower().Contains(s)));
            }

            // Department filter (target new department)
            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(a => a.NewDepartment == department);
            }

            // Previous Department filter
            if (!string.IsNullOrWhiteSpace(previousDepartment))
            {
                query = query.Where(a => a.PreviousDepartment == previousDepartment);
            }

            // Position filter (target new position)
            if (!string.IsNullOrWhiteSpace(position))
            {
                query = query.Where(a => a.NewPosition == position);
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "EFFECTIVE")
                {
                    query = query.Where(a => a.ApprovalStatus == "EFFECTIVE" || a.AppliedAt != null);
                }
                else if (status == "APPROVED")
                {
                    query = query.Where(a => a.ApprovalStatus == "APPROVED" && a.AppliedAt == null);
                }
                else
                {
                    query = query.Where(a => a.ApprovalStatus == status);
                }
            }

            // Effective Date filter
            if (!string.IsNullOrWhiteSpace(effectiveDate) && DateTime.TryParse(effectiveDate, out var parsedDate))
            {
                query = query.Where(a => a.EffectiveDate.Date == parsedDate.Date);
            }

            int totalItems = await query.CountAsync();
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 5) pageSize = 10;

            var pagedRecords = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AppointmentItemViewModel
                {
                    Id = a.Id,
                    EmployeeId = a.EmployeeId,
                    EmployeeCode = a.EmployeeCodeSnapshot ?? (a.Employee != null ? a.Employee.EmployeeCode : "---"),
                    EmployeeName = a.EmployeeNameSnapshot ?? (a.Employee != null ? a.Employee.Name : "---"),
                    AvatarPath = a.Employee != null ? a.Employee.AvatarPath : null,
                    PreviousDepartment = a.PreviousDepartment,
                    PreviousPosition = a.PreviousPosition,
                    PreviousJobTitle = a.PreviousJobTitle,
                    NewDepartment = a.NewDepartment,
                    NewPosition = a.NewPosition,
                    NewJobTitle = a.NewJobTitle,
                    EffectiveDate = a.EffectiveDate,
                    Reason = a.Reason,
                    Notes = a.Notes,
                    DecisionNumber = a.DecisionNumber,
                    DecisionDate = a.DecisionDate,
                    ApprovalStatus = a.ApprovalStatus,
                    ApprovalStatusLabel = a.ApprovalStatusLabel,
                    EffectiveStatus = a.EffectiveStatus,
                    EffectiveStatusLabel = a.EffectiveStatusLabel,
                    CreatedBy = a.CreatedBy,
                    CreatedAt = a.CreatedAt,
                    SubmittedBy = a.SubmittedBy,
                    SubmittedAt = a.SubmittedAt,
                    ApprovedBy = a.ApprovedBy,
                    ApprovedAt = a.ApprovedAt,
                    RejectedBy = a.RejectedBy,
                    RejectedAt = a.RejectedAt,
                    RejectionReason = a.RejectionReason,
                    AppliedBy = a.AppliedBy,
                    AppliedAt = a.AppliedAt
                })
                .ToListAsync();

            var deptOptions = await _context.Employees
                .Where(e => !string.IsNullOrEmpty(e.Department))
                .Select(e => e.Department!)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var posOptions = await _context.JobPositions
                .OrderBy(j => j.Name)
                .Select(j => j.Name)
                .ToListAsync();

            var viewModel = new HRAppointmentIndexViewModel
            {
                CurrentRole = role,
                Summary = summary,
                Items = pagedRecords,
                SearchName = searchName,
                Department = department,
                PreviousDepartment = previousDepartment,
                Position = position,
                Status = status,
                EffectiveDate = effectiveDate,
                DepartmentOptions = deptOptions,
                PositionOptions = posOptions,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            // Maintain legacy ViewBag values for backward compatibility
            ViewBag.StatPending = summary.PendingHR + summary.PendingLeader;
            ViewBag.StatApproved = summary.Approved + summary.Effective;
            ViewBag.StatUpcoming = summary.Approved;
            ViewBag.StatRejected = summary.Rejected;
            ViewBag.Departments = deptOptions;
            ViewBag.JobPositions = await _context.JobPositions.OrderBy(j => j.Name).ToListAsync();
            ViewBag.CurrentSearch = searchName;
            ViewBag.CurrentDept = department;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentEffectiveDate = effectiveDate;
            ViewBag.Role = role;

            return View(viewModel);
        }

        // EXPORT CSV — Xuất dữ liệu bổ nhiệm tôn trọng bộ lọc hiện tại
        [HttpGet]
        public async Task<IActionResult> Export(
            string? searchName,
            string? department,
            string? previousDepartment,
            string? position,
            string? status,
            string? effectiveDate)
        {
            var role = GetCurrentRole();
            if (role != AppRoles.HR && role != AppRoles.Admin && role != AppRoles.Leader)
            {
                return StatusCode(403, new { message = "Bạn không có quyền xuất dữ liệu này." });
            }

            var query = _context.AppointmentRecords
                .Include(a => a.Employee)
                .AsQueryable();

            if (role == AppRoles.Leader)
            {
                var allowedDepts = _leaderScope.GetOverseenOrganizationalUnits();
                query = query.Where(a => (a.PreviousDepartment != null && allowedDepts.Contains(a.PreviousDepartment)) ||
                                         (a.NewDepartment != null && allowedDepts.Contains(a.NewDepartment)));
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var s = searchName.Trim().ToLower();
                query = query.Where(a => 
                    (a.EmployeeNameSnapshot != null && a.EmployeeNameSnapshot.ToLower().Contains(s)) ||
                    (a.EmployeeCodeSnapshot != null && a.EmployeeCodeSnapshot.ToLower().Contains(s)) ||
                    (a.DecisionNumber != null && a.DecisionNumber.ToLower().Contains(s)) ||
                    (a.CreatedBy != null && a.CreatedBy.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(department)) query = query.Where(a => a.NewDepartment == department);
            if (!string.IsNullOrWhiteSpace(previousDepartment)) query = query.Where(a => a.PreviousDepartment == previousDepartment);
            if (!string.IsNullOrWhiteSpace(position)) query = query.Where(a => a.NewPosition == position);

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "EFFECTIVE") query = query.Where(a => a.ApprovalStatus == "EFFECTIVE" || a.AppliedAt != null);
                else if (status == "APPROVED") query = query.Where(a => a.ApprovalStatus == "APPROVED" && a.AppliedAt == null);
                else query = query.Where(a => a.ApprovalStatus == status);
            }

            if (!string.IsNullOrWhiteSpace(effectiveDate) && DateTime.TryParse(effectiveDate, out var parsedDate))
            {
                query = query.Where(a => a.EffectiveDate.Date == parsedDate.Date);
            }

            var list = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("STT,Mã NV,Họ và tên,Phòng ban cũ,Chức vụ cũ,Phòng ban mới,Chức vụ mới,Số QĐ,Ngày hiệu lực,Trạng thái duyệt,Hiệu lực,Người đề xuất,Ngày đề xuất,Người duyệt,Ngày duyệt");

            int idx = 1;
            foreach (var item in list)
            {
                string Escape(string? val) => $"\"{(val ?? "").Replace("\"", "\"\"")}\"";
                var empCode = item.EmployeeCodeSnapshot ?? item.Employee?.EmployeeCode ?? "---";
                var empName = Escape(item.EmployeeNameSnapshot ?? item.Employee?.Name ?? "---");
                var oldDept = Escape(item.PreviousDepartment ?? "---");
                var oldPos = Escape(item.PreviousPosition ?? "---");
                var newDept = Escape(item.NewDepartment ?? "---");
                var newPos = Escape(item.NewPosition ?? "---");
                var decNum = Escape(item.DecisionNumber ?? "---");
                var effDate = item.EffectiveDate.ToString("dd/MM/yyyy");
                var stLabel = Escape(item.ApprovalStatusLabel);
                var effLabel = Escape(item.EffectiveStatusLabel);
                var creator = Escape(item.CreatedBy ?? "---");
                var createDate = item.CreatedAt.ToString("dd/MM/yyyy HH:mm");
                var approver = Escape(item.ApprovedBy ?? "---");
                var approveDate = item.ApprovedAt?.ToString("dd/MM/yyyy HH:mm") ?? "---";

                csv.AppendLine($"{idx},{empCode},{empName},{oldDept},{oldPos},{newDept},{newPos},{decNum},{effDate},{stLabel},{effLabel},{creator},{createDate},{approver},{approveDate}");
                idx++;
            }

            var preamble = System.Text.Encoding.UTF8.GetPreamble();
            var dataBytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var resultBytes = new byte[preamble.Length + dataBytes.Length];
            Buffer.BlockCopy(preamble, 0, resultBytes, 0, preamble.Length);
            Buffer.BlockCopy(dataBytes, 0, resultBytes, preamble.Length, dataBytes.Length);

            return File(resultBytes, "text/csv; charset=utf-8", $"Danh_sach_bo_nhiem_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        // 2. DETAIL — Lấy chi tiết bổ nhiệm (AJAX)
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var record = await _context.AppointmentRecords
                .Include(a => a.Employee)
                .Include(a => a.Histories)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (record == null)
                return NotFound(new { message = "Không tìm thấy quyết định bổ nhiệm." });

            if (!HasAccessToRecord(record))
                return StatusCode(403, new { message = "Bạn không có quyền truy cập quyết định này." });

            return Json(new
            {
                id = record.Id,
                employeeId = record.EmployeeId,
                employeeCode = record.EmployeeCodeSnapshot ?? "---",
                employeeName = record.EmployeeNameSnapshot ?? "---",
                avatarPath = record.Employee?.AvatarPath,
                
                previousDepartment = record.PreviousDepartment ?? "---",
                previousPosition = record.PreviousPosition ?? "---",
                previousJobTitle = record.PreviousJobTitle ?? "---",

                newDepartment = record.NewDepartment ?? "---",
                newPosition = record.NewPosition ?? "---",
                newJobTitle = record.NewJobTitle ?? "---",

                effectiveDate = record.EffectiveDate.ToString("dd/MM/yyyy"),
                reason = record.Reason,
                notes = record.Notes,

                decisionNumber = record.DecisionNumber ?? "---",
                decisionDate = record.DecisionDate?.ToString("dd/MM/yyyy"),
                decisionFilePath = record.DecisionFilePath,

                approvalStatus = record.ApprovalStatus,
                approvalStatusLabel = record.ApprovalStatusLabel,
                effectiveStatus = record.EffectiveStatus,
                effectiveStatusLabel = record.EffectiveStatusLabel,

                createdBy = record.CreatedBy,
                createdAt = record.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                approvedBy = record.ApprovedBy,
                approvedAt = record.ApprovedAt?.ToString("dd/MM/yyyy HH:mm"),
                rejectedBy = record.RejectedBy,
                rejectedAt = record.RejectedAt?.ToString("dd/MM/yyyy HH:mm"),
                rejectionReason = record.RejectionReason,
                cancelledBy = record.CancelledBy,
                cancelledAt = record.CancelledAt?.ToString("dd/MM/yyyy HH:mm"),
                cancellationReason = record.CancellationReason,
                appliedAt = record.AppliedAt?.ToString("dd/MM/yyyy HH:mm"),

                histories = record.Histories.OrderByDescending(h => h.Timestamp).Select(h => new
                {
                    timestamp = h.Timestamp.ToString("dd/MM/yyyy HH:mm"),
                    actor = h.Actor,
                    action = h.Action,
                    details = h.Details
                })
            });
        }

        // 3. GET EMPLOYEES — Cho chức năng Tạo mới
        [HttpGet]
        public async Task<IActionResult> GetEmployees(string? q)
        {
            var role = GetCurrentRole();
            var query = _context.Employees.AsQueryable();

            if (role == AppRoles.Manager)
            {
                var allowed = _managerScope.GetManagedOrganizationalUnits();
                query = query.Where(e => allowed.Contains(e.Department));

                // Exclude the manager themselves from the dropdown to prevent self-nomination
                var currentActor = await CurrentActor.ResolveUserAsync(User, _context);
                if (currentActor?.EmployeeId.HasValue == true)
                {
                    query = query.Where(e => e.Id != currentActor.EmployeeId.Value);
                }
            }
            else if (role != AppRoles.HR)
            {
                return Json(new object[0]);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(e =>
                    (e.Name != null && e.Name.Contains(q)) ||
                    (e.EmployeeCode != null && e.EmployeeCode.Contains(q)));
            }

            var employees = await query
                .OrderBy(e => e.EmployeeCode)
                .Take(20)
                .Select(e => new
                {
                    e.Id,
                    e.EmployeeCode,
                    e.Name,
                    e.Department,
                    e.JobTitle,
                    e.Position,
                    e.AvatarPath
                })
                .ToListAsync();

            return Json(employees);
        }

        // 4. CREATE — Tạo bổ nhiệm mới (Lưu nháp hoặc Trình duyệt)
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Create([FromBody] AppointmentRecord model, [FromQuery] bool isSubmit = false)
        {
            try
            {
                if (model.EmployeeId <= 0)
                {
                    return BadRequest(new { message = "Vui lòng chọn nhân viên." });
                }

                if (string.IsNullOrWhiteSpace(model.NewDepartment) || string.IsNullOrWhiteSpace(model.NewPosition))
                {
                    return BadRequest(new { message = "Phòng ban mới và chức vụ mới không được để trống." });
                }

                if (string.IsNullOrWhiteSpace(model.Reason))
                {
                    return BadRequest(new { message = "Lý do bổ nhiệm không được để trống." });
                }

                var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == model.EmployeeId);
                if (employee == null) return BadRequest(new { message = "Không tìm thấy nhân viên." });

                var role = GetCurrentRole();
                if (role == AppRoles.Manager)
                {
                    // Prevent Manager self-nomination
                    var currentActor = await CurrentActor.ResolveUserAsync(User, _context);
                    if (currentActor?.EmployeeId.HasValue == true && currentActor.EmployeeId.Value == employee.Id)
                    {
                        return BadRequest(new { message = "Quản lý không được tự tạo quyết định bổ nhiệm cho chính mình." });
                    }

                    var allowed = _managerScope.GetManagedOrganizationalUnits();
                    if (string.IsNullOrEmpty(employee.Department) || !allowed.Contains(employee.Department))
                    {
                        return StatusCode(403, new { message = "Bạn không có quyền thao tác trên nhân viên này." });
                    }
                }

                // Check duplicates if unique decision number is required
                if (!string.IsNullOrWhiteSpace(model.DecisionNumber))
                {
                    bool exists = await _context.AppointmentRecords.AnyAsync(a => a.DecisionNumber == model.DecisionNumber);
                    if (exists) return BadRequest(new { message = "Số quyết định đã tồn tại." });
                }

                model.EmployeeCodeSnapshot = employee.EmployeeCode;
                model.EmployeeNameSnapshot = employee.Name;
                model.PreviousDepartment = employee.Department;
                model.PreviousPosition = employee.Position;
                model.PreviousJobTitle = employee.JobTitle;
                model.Employee = employee;

                // All workflow, audit and effective fields are server-owned.
                model.Id = 0;
                model.DecisionFilePath = null;
                model.CreatedBy = GetCurrentUser();
                model.CreatedAt = DateTime.Now;
                model.ApprovalStatus = isSubmit ? "PENDING_HR_REVIEW" : "DRAFT";
                model.ApprovedBy = null;
                model.ApprovedAt = null;
                model.RejectedBy = null;
                model.RejectedAt = null;
                model.RejectionReason = null;
                model.CancelledBy = null;
                model.CancelledAt = null;
                model.CancellationReason = null;
                model.AppliedBy = null;
                model.AppliedAt = null;
                model.Histories = new System.Collections.Generic.List<AppointmentHistory>();
                if (isSubmit)
                {
                    model.SubmittedBy = model.CreatedBy;
                    model.SubmittedAt = DateTime.Now;
                }
                else
                {
                    model.SubmittedBy = null;
                    model.SubmittedAt = null;
                }

                var history = new AppointmentHistory
                {
                    Actor = model.CreatedBy,
                    Action = isSubmit ? "SUBMIT" : "CREATE",
                    Details = isSubmit ? "Tạo và trình duyệt quyết định bổ nhiệm" : "Tạo bản nháp quyết định bổ nhiệm"
                };
                model.Histories.Add(history);

                _context.AppointmentRecords.Add(model);
                await _context.SaveChangesAsync();

                if (isSubmit)
                {
                    _notificationEngine.QueueEvent(_context, "APPOINTMENT_SUBMITTED", new { 
                        AppointmentId = model.Id, 
                        EmployeeName = model.EmployeeNameSnapshot 
                    }, null, null, "APP_SUBMIT_" + model.Id);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true, message = "Lưu hồ sơ thành công.", id = model.Id });
            }
            catch (Exception ex)
            {
                string errMsg = ex.Message;
                if (ex.InnerException != null) errMsg += " - Inner: " + ex.InnerException.Message;
                return StatusCode(500, new { success = false, message = errMsg });
            }
        }

        // 5. SUBMIT — Trình duyệt từ bản nháp
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Submit(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var record = await _context.AppointmentRecords
                    .Include(a => a.Employee)
                    .Include(a => a.Histories)
                    .FirstOrDefaultAsync(a => a.Id == id);
                if (record == null) return NotFound(new { message = "Không tìm thấy quyết định." });
                await _context.Entry(record).ReloadAsync();

                if (record.ApprovalStatus != "DRAFT") return BadRequest(new { message = "Chỉ được trình duyệt bản nháp." });

                if (!IsCurrentUser(record.CreatedBy))
                {
                    return StatusCode(403, new { message = "Bạn không có quyền trình duyệt quyết định này." });
                }

                // Re-verify Manager scope on target employee
                var allowed = _managerScope.GetManagedOrganizationalUnits();
                var targetDept = record.Employee?.Department ?? record.PreviousDepartment;
                if (string.IsNullOrEmpty(targetDept) || !allowed.Contains(targetDept))
                {
                    return StatusCode(403, new { message = "Nhân viên không còn thuộc phạm vi quản lý của bạn." });
                }

                record.ApprovalStatus = "PENDING_HR_REVIEW";
                record.SubmittedBy = GetCurrentUser();
                record.SubmittedAt = DateTime.Now;

                _notificationEngine.QueueEvent(_context, "APPOINTMENT_SUBMITTED", new { 
                    AppointmentId = record.Id, 
                    EmployeeName = record.EmployeeNameSnapshot 
                }, null, null, "APP_SUBMIT_" + record.Id);

                record.Histories.Add(new AppointmentHistory
                {
                    Actor = GetCurrentUser(),
                    Action = "SUBMIT",
                    Details = "Trình duyệt quyết định (chuyển qua HR)"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Trình duyệt thành công." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi hệ thống khi trình duyệt.", details = ex.Message });
            }
        }

        // 6. APPROVE — Duyệt
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var approverRole = GetCurrentRole();
            if (approverRole != AppRoles.HR && approverRole != AppRoles.Leader)
            {
                return StatusCode(403, new { message = "Bạn không có quyền thực hiện thao tác duyệt." });
            }

            var record = await _context.AppointmentRecords.Include(a => a.Histories).FirstOrDefaultAsync(a => a.Id == id);
            if (record == null) return NotFound(new { message = "Không tìm thấy quyết định." });

            if (!HasAccessToRecord(record))
            {
                return StatusCode(403, new { message = "Bạn không có quyền truy cập quyết định này." });
            }

            // Enforce expected workflow state per role
            if (approverRole == AppRoles.HR && record.ApprovalStatus != "PENDING_HR_REVIEW")
            {
                return BadRequest(new { message = "Hồ sơ không ở trạng thái chờ HR xác minh." });
            }

            if (approverRole == AppRoles.HR && record.ApprovalStatus == "PENDING_HR_REVIEW")
            {
                var targetDept = record.NewDepartment ?? record.PreviousDepartment;
                var hasLeader = await _context.LeaderAssignments.AnyAsync(la => la.OrganizationalUnit == targetDept && la.EffectiveFrom <= DateTime.Now && (la.EffectiveTo == null || la.EffectiveTo > DateTime.Now));
                if (!hasLeader)
                {
                    return BadRequest(new { message = $"Không thể chuyển Lãnh đạo vì phòng ban '{targetDept}' chưa được phân bổ Lãnh đạo phụ trách." });
                }
            }
            if (approverRole == AppRoles.Leader && record.ApprovalStatus != "PENDING_LEADER_APPROVAL")
            {
                return BadRequest(new { message = "Hồ sơ không ở trạng thái chờ Lãnh đạo duyệt." });
            }

            var result = await _approvalEngine.ProcessDecisionAsync(record, "Approve", GetCurrentUser(), approverRole, null, _policy);

            if (result.Success)
            {
                record.Histories.Add(new AppointmentHistory
                {
                    Actor = GetCurrentUser(),
                    Action = approverRole == AppRoles.HR ? "HR_VERIFY" : "LEADER_APPROVE",
                    Details = approverRole == AppRoles.HR ? "HR xác minh quyết định bổ nhiệm" : "Lãnh đạo phê duyệt quyết định bổ nhiệm"
                });
                await _context.SaveChangesAsync();

                if (approverRole == AppRoles.HR && record.ApprovalStatus == "PENDING_LEADER_APPROVAL")
                {
                    await QueueScopedLeaderNotificationsAsync(record, "APPOINTMENT_PENDING_LEADER", "PENDING_LEADER_APPROVAL");
                    await _context.SaveChangesAsync();
                }
                else if (approverRole == AppRoles.Leader && record.ApprovalStatus == "APPROVED")
                {
                    QueueProposerNotification(record, "APPOINTMENT_APPROVED", "APPROVED");
                    await _context.SaveChangesAsync();
                }

                // P0-C3: If Leader approved and EffectiveDate is reached, apply immediately via EffectiveService
                if (approverRole == AppRoles.Leader && record.ApprovalStatus == "APPROVED" && record.EffectiveDate.Date <= DateTime.Today)
                {
                    await _appointmentEffectiveService.ApplyAppointmentAsync(record.Id, GetCurrentUser());
                }

                return Ok(new { message = approverRole == AppRoles.HR ? "Đã xác minh và chuyển Lãnh đạo duyệt." : "Đã phê duyệt quyết định bổ nhiệm." });
            }
            return BadRequest(new { message = result.Message });
        }

        // 7. REJECT — Từ chối
        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var approverRole = GetCurrentRole();
            if (approverRole != AppRoles.HR && approverRole != AppRoles.Leader)
            {
                return StatusCode(403, new { message = "Bạn không có quyền thực hiện thao tác từ chối." });
            }

            if (string.IsNullOrWhiteSpace(reason)) return BadRequest(new { message = "Lý do từ chối không được để trống." });

            var record = await _context.AppointmentRecords.Include(a => a.Histories).FirstOrDefaultAsync(a => a.Id == id);
            if (record == null) return NotFound(new { message = "Không tìm thấy quyết định." });

            if (!HasAccessToRecord(record))
            {
                return StatusCode(403, new { message = "Bạn không có quyền truy cập quyết định này." });
            }

            if (approverRole == AppRoles.HR && record.ApprovalStatus != "PENDING_HR_REVIEW")
            {
                return BadRequest(new { message = "Hồ sơ không ở trạng thái chờ HR xác minh." });
            }
            if (approverRole == AppRoles.Leader && record.ApprovalStatus != "PENDING_LEADER_APPROVAL")
            {
                return BadRequest(new { message = "Hồ sơ không ở trạng thái chờ Lãnh đạo duyệt." });
            }

            var result = await _approvalEngine.ProcessDecisionAsync(record, "Reject", GetCurrentUser(), approverRole, reason, _policy);

            if (result.Success)
            {
                record.RejectedBy = GetCurrentUser();
                record.RejectedAt = DateTime.Now;
                record.ApprovedBy = null;
                record.ApprovedAt = null;
                record.Histories.Add(new AppointmentHistory
                {
                    Actor = GetCurrentUser(),
                    Action = "REJECT",
                    Details = $"Từ chối quyết định. Lý do: {reason}"
                });
                await _context.SaveChangesAsync();
                if (approverRole == AppRoles.Leader && record.ApprovalStatus == "REJECTED")
                {
                    QueueProposerNotification(record, "APPOINTMENT_REJECTED", "REJECTED");
                    await _context.SaveChangesAsync();
                }
                return Ok(new { message = "Đã từ chối quyết định." });
            }
            return BadRequest(new { message = result.Message });
        }

        // 8. CANCEL — Hủy
        [HttpPost]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return BadRequest(new { message = "Lý do hủy không được để trống." });

            var record = await _context.AppointmentRecords.Include(a => a.Histories).FirstOrDefaultAsync(a => a.Id == id);
            if (record == null) return NotFound(new { message = "Không tìm thấy quyết định." });

            if (!IsCurrentUser(record.CreatedBy))
            {
                return StatusCode(403, new { message = "Bạn không có quyền hủy quyết định này." });
            }

            if (GetCurrentRole() == AppRoles.Manager && !HasAccessToRecord(record))
            {
                return StatusCode(403, new { message = "Nhân viên không còn thuộc phạm vi quản lý của bạn." });
            }

            if (record.ApprovalStatus != "DRAFT" && record.ApprovalStatus != "PENDING_HR_REVIEW")
                return BadRequest(new { message = "Chỉ được hủy quyết định ở trạng thái Bản nháp hoặc Chờ HR xác minh." });

            if (record.AppliedAt != null) return BadRequest(new { message = "Không thể hủy quyết định đã áp dụng." });

            record.ApprovalStatus = "CANCELLED";
            record.CancelledBy = GetCurrentUser();
            record.CancelledAt = DateTime.Now;
            record.CancellationReason = reason;

            record.Histories.Add(new AppointmentHistory
            {
                Actor = GetCurrentUser(),
                Action = "CANCEL",
                Details = $"Hủy quyết định. Lý do: {reason}"
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã hủy quyết định." });
        }
    }
}
