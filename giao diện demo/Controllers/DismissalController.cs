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
    public class DismissalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IApprovalEngine _approvalEngine;
        private readonly giao_dien_demo.Services.IWorkflowPolicy<DismissalRecord> _policy;
        private readonly giao_dien_demo.Services.IManagerScopeService _managerScope;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _leaderScope;
        private readonly giao_dien_demo.Services.IDismissalEffectiveService _dismissalEffectiveService;
        private readonly giao_dien_demo.Services.NotificationEngineService _notificationEngine;

        public DismissalController(
            ApplicationDbContext context, 
            giao_dien_demo.Services.IApprovalEngine approvalEngine, 
            giao_dien_demo.Services.IWorkflowPolicy<DismissalRecord> policy,
            giao_dien_demo.Services.IManagerScopeService managerScope,
            giao_dien_demo.Services.ILeadershipScopeService leaderScope,
            giao_dien_demo.Services.IDismissalEffectiveService dismissalEffectiveService,
            giao_dien_demo.Services.NotificationEngineService notificationEngine)
        {
            _context = context;
            _approvalEngine = approvalEngine;
            _policy = policy;
            _managerScope = managerScope;
            _leaderScope = leaderScope;
            _dismissalEffectiveService = dismissalEffectiveService;
            _notificationEngine = notificationEngine;
        }

        private string GetCurrentUser()
        {
            return User.Identity?.Name ?? HttpContext.Session.GetString("User") ?? "System";
        }

        private string GetCurrentRole()
        {
            if (User.IsInRole(AppRoles.HR)) return AppRoles.HR;
            if (User.IsInRole(AppRoles.Leader)) return AppRoles.Leader;
            if (User.IsInRole(AppRoles.Manager)) return AppRoles.Manager;
            if (User.IsInRole(AppRoles.Admin)) return AppRoles.Admin;
            return HttpContext.Session.GetString("Role") ?? "Unknown";
        }

        private bool HasAccessToRecord(DismissalRecord record)
        {
            var role = GetCurrentRole();
            if (role == AppRoles.HR) return true;
            if (role == AppRoles.Manager)
            {
                var allowed = _managerScope.GetManagedOrganizationalUnits();
                return allowed.Contains(record.CurrentDepartmentSnapshot);
            }
            if (role == AppRoles.Leader)
            {
                var allowed = _leaderScope.GetOverseenOrganizationalUnits();
                return allowed.Contains(record.CurrentDepartmentSnapshot);
            }
            return false;
        }

        private async Task QueueScopedLeaderNotificationsAsync(DismissalRecord record)
        {
            var now = DateTime.Now;
            var leaderIds = await _context.LeaderAssignments
                .Where(a => a.OrganizationalUnit == record.CurrentDepartmentSnapshot
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
                _notificationEngine.QueueEvent(_context, "DISMISSAL_PENDING_LEADER", new
                {
                    DismissalId = record.Id,
                    EmployeeName = record.EmployeeNameSnapshot,
                    EffectiveDate = record.EffectiveDate
                }, relatedUsername: leader.Username,
                   idempotencyKey: $"dismissal:{record.Id}:pending-leader:{leader.Id}");
            }
        }

        private void QueueProposerNotification(DismissalRecord record, string eventCode, string action)
        {
            var proposer = record.SubmittedBy ?? record.CreatedBy;
            if (string.IsNullOrWhiteSpace(proposer)) return;

            _notificationEngine.QueueEvent(_context, eventCode, new
            {
                DismissalId = record.Id,
                EmployeeName = record.EmployeeNameSnapshot,
                Action = action,
                EffectiveDate = record.EffectiveDate,
                RejectionReason = record.RejectionReason
            }, relatedUsername: proposer,
               idempotencyKey: $"dismissal:{record.Id}:{eventCode}:{proposer}");
        }

        // 1. INDEX — Danh sách miễn nhiệm
        public async Task<IActionResult> Index(
            string? searchName,
            string? department,
            string? status,
            DateTime? effectiveDate,
            int pageNumber = 1,
            int pageSize = 10)
        {
            var role = GetCurrentRole();
            var query = _context.DismissalRecords
                .Include(a => a.Employee)
                .AsQueryable();

            if (role == AppRoles.Manager)
            {
                var allowedDepts = _managerScope.GetManagedOrganizationalUnits();
                query = query.Where(a => allowedDepts.Contains(a.CurrentDepartmentSnapshot));
            }
            else if (role == AppRoles.Leader)
            {
                var allowedDepts = _leaderScope.GetOverseenOrganizationalUnits();
                query = query.Where(a => allowedDepts.Contains(a.CurrentDepartmentSnapshot));
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(a => 
                    (a.EmployeeNameSnapshot != null && a.EmployeeNameSnapshot.Contains(searchName)) ||
                    (a.EmployeeCodeSnapshot != null && a.EmployeeCodeSnapshot.Contains(searchName)) ||
                    (a.DecisionNumber != null && a.DecisionNumber.Contains(searchName)) ||
                    (a.CurrentJobTitleSnapshot != null && a.CurrentJobTitleSnapshot.Contains(searchName)));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(a => a.CurrentDepartmentSnapshot == department);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = status == "EFFECTIVE"
                    ? query.Where(a => a.ApprovalStatus == "EFFECTIVE" || a.AppliedAt != null)
                    : query.Where(a => a.ApprovalStatus == status);
            }

            if (effectiveDate.HasValue)
            {
                query = query.Where(a => a.EffectiveDate.Date == effectiveDate.Value.Date);
            }

            var summaryQuery = query;
            ViewBag.StatTotal = await summaryQuery.CountAsync();
            ViewBag.StatPendingHR = await summaryQuery.CountAsync(r => r.ApprovalStatus == "PENDING_HR_REVIEW");
            ViewBag.StatPendingLeader = await summaryQuery.CountAsync(r => r.ApprovalStatus == "PENDING_LEADER_APPROVAL");
            ViewBag.StatApproved = await summaryQuery.CountAsync(r => r.ApprovalStatus == "APPROVED" && r.AppliedAt == null);
            ViewBag.StatCompleted = await summaryQuery.CountAsync(r => r.ApprovalStatus == "EFFECTIVE" || r.AppliedAt != null);

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 5 || pageSize > 100) pageSize = 10;
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0 && pageNumber > totalPages) pageNumber = totalPages;
            var records = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            // Calculate Statistics
            ViewBag.StatPending = ViewBag.StatPendingHR;
            ViewBag.StatUpcoming = ViewBag.StatApproved;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;

            // Departments and JobPositions for UI Filters and Forms
            ViewBag.Departments = await _context.Employees
                .Where(e => !string.IsNullOrEmpty(e.Department))
                .Select(e => e.Department)
                .Distinct()
                .ToListAsync();
                
            ViewBag.JobPositions = await _context.JobPositions
                .OrderBy(j => j.Name)
                .ToListAsync();

            ViewBag.CurrentSearch = searchName;
            ViewBag.CurrentDept = department;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentEffectiveDate = effectiveDate?.ToString("yyyy-MM-dd");
            ViewBag.Role = role;

            return View(records);
        }

        // 2. DETAIL — Lấy chi tiết miễn nhiệm (AJAX)
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var record = await _context.DismissalRecords
                .Include(a => a.Employee)
                .Include(a => a.Histories)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (record == null)
                return NotFound(new { message = "Không tìm thấy quyết định miễn nhiệm." });

            if (!HasAccessToRecord(record))
                return StatusCode(403, new { message = "Bạn không có quyền truy cập quyết định này." });

            return Json(new
            {
                id = record.Id,
                employeeId = record.EmployeeId,
                employeeCode = record.EmployeeCodeSnapshot ?? "---",
                employeeName = record.EmployeeNameSnapshot ?? "---",
                avatarPath = record.Employee?.AvatarPath,
                
                currentDepartment = record.CurrentDepartmentSnapshot ?? "---",
                currentPosition = record.CurrentPositionSnapshot ?? "---",
                currentJobTitle = record.CurrentJobTitleSnapshot ?? "---",

                postReliefOption = record.PostReliefOption,
                postReliefOptionLabel = record.PostReliefOptionLabel,

                targetDepartment = record.TargetDepartment ?? "---",
                targetPosition = record.TargetPosition ?? "---",

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

                histories = record.Histories.OrderBy(h => h.Timestamp).Select(h => new {
                    timestamp = h.Timestamp.ToString("dd/MM/yyyy HH:mm"),
                    actor = h.Actor,
                    action = h.Action,
                    details = h.Details
                })
            });
        }

        // 3. Lấy thông tin NV để đổ vào form (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetEmployees(string q)
        {
            var role = GetCurrentRole();
            var query = _context.Employees.AsQueryable();

            if (role == AppRoles.Manager)
            {
                var allowed = _managerScope.GetManagedOrganizationalUnits();
                query = query.Where(e => allowed.Contains(e.Department));
            }
            else if (role != AppRoles.HR)
            {
                return Json(new object[0]);
            }

            if (string.IsNullOrWhiteSpace(q)) return Json(new object[] { });

            var emps = await query
                .Where(e => (e.Name != null && e.Name.Contains(q)) || (e.EmployeeCode != null && e.EmployeeCode.Contains(q)))
                .Take(10)
                .Select(e => new {
                    id = e.Id,
                    code = e.EmployeeCode,
                    name = e.Name,
                    department = e.Department,
                    position = e.Position,
                    jobTitle = e.JobTitle,
                    avatar = e.AvatarPath
                })
                .ToListAsync();

            return Json(emps);
        }

        // 4. Lấy lịch sử công tác trước đó để hỗ trợ Về Vị trí cũ (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetPreviousAppointment(int employeeId)
        {
            var prevAppointment = await _context.AppointmentRecords
                .Where(a => a.EmployeeId == employeeId && a.ApprovalStatus == "APPROVED" && a.AppliedAt != null)
                .OrderByDescending(a => a.AppliedAt)
                .FirstOrDefaultAsync();
                
            if (prevAppointment == null) {
                return Json(null);
            }
            
            return Json(new {
                department = prevAppointment.PreviousDepartment,
                position = prevAppointment.PreviousPosition
            });
        }

        // 5. CREATE — Lưu nháp hoặc trình duyệt
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Create([FromBody] DismissalRecord model, [FromQuery] bool isDraft = false)
        {
            var emp = await _context.Employees.FindAsync(model.EmployeeId);
            if (emp == null) return BadRequest("Nhân viên không tồn tại.");

            var role = GetCurrentRole();
            if (role == AppRoles.Manager)
            {
                var allowed = _managerScope.GetManagedOrganizationalUnits();
                if (!allowed.Contains(emp.Department))
                {
                    return StatusCode(403, new { message = "Bạn không có quyền thao tác trên nhân viên này." });
                }
            }

            // Basic validation
            if (!isDraft)
            {
                if (model.EffectiveDate == default) return BadRequest("Vui lòng chọn ngày hiệu lực.");
                if (string.IsNullOrWhiteSpace(model.Reason)) return BadRequest("Vui lòng nhập lý do miễn nhiệm.");
                if (string.IsNullOrWhiteSpace(model.PostReliefOption)) return BadRequest("Vui lòng chọn phương án sau miễn nhiệm.");
                if (string.IsNullOrWhiteSpace(model.DecisionNumber)) return BadRequest("Vui lòng nhập số quyết định.");
                if (model.DecisionDate == null) return BadRequest("Vui lòng chọn ngày quyết định.");
                
                if (model.PostReliefOption == "TRANSFER_TO_OTHER_POSITION" || model.PostReliefOption == "RETURN_TO_PREVIOUS_POSITION")
                {
                    if (string.IsNullOrWhiteSpace(model.TargetDepartment)) return BadRequest("Vui lòng xác định phòng ban mới.");
                    if (string.IsNullOrWhiteSpace(model.TargetPosition)) return BadRequest("Vui lòng xác định vị trí mới.");
                }
            }

            // Capture Snapshot
            model.EmployeeCodeSnapshot = emp.EmployeeCode;
            model.EmployeeNameSnapshot = emp.Name;
            model.CurrentDepartmentSnapshot = emp.Department;
            model.CurrentPositionSnapshot = emp.Position;
            model.CurrentJobTitleSnapshot = emp.JobTitle;

            model.CreatedBy = GetCurrentUser();
            model.CreatedAt = DateTime.Now;
            model.ApprovalStatus = isDraft ? "DRAFT" : "PENDING_HR_REVIEW";

            if (!isDraft)
            {
                model.SubmittedBy = model.CreatedBy;
                model.SubmittedAt = DateTime.Now;
            }

            _context.DismissalRecords.Add(model);
            await _context.SaveChangesAsync();

            // Add history
            _context.DismissalHistories.Add(new DismissalHistory
            {
                DismissalRecordId = model.Id,
                Actor = GetCurrentUser(),
                Action = isDraft ? "CREATE" : "SUBMIT",
                Details = $"Hồ sơ được {(isDraft ? "tạo nháp" : "trình duyệt")}. Option: {model.PostReliefOptionLabel}"
            });
            await _context.SaveChangesAsync();

            if (!isDraft)
            {
                _notificationEngine.QueueEvent(_context, "DISMISSAL_SUBMITTED", new { 
                    DismissalId = model.Id, 
                    EmployeeName = model.EmployeeNameSnapshot 
                }, null, null, "DIS_SUBMIT_" + model.Id);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, id = model.Id });
        }

        // 6. SUBMIT (Từ DRAFT -> PENDING_HR_REVIEW)
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Submit(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var record = await _context.DismissalRecords.FindAsync(id);
                if (record == null) return NotFound("Không tìm thấy quyết định.");
                await _context.Entry(record).ReloadAsync();

                if (record.ApprovalStatus != "DRAFT") return BadRequest("Chỉ có thể trình duyệt từ bản nháp.");

                if (record.CreatedBy != GetCurrentUser())
                {
                    return StatusCode(403, new { message = "Bạn không có quyền trình duyệt quyết định này." });
                }

                if (GetCurrentRole() == AppRoles.Manager && !HasAccessToRecord(record))
                {
                    return StatusCode(403, new { message = "Nhân viên không còn thuộc phạm vi quản lý của bạn." });
                }

                record.ApprovalStatus = "PENDING_HR_REVIEW";
                record.SubmittedBy = GetCurrentUser();
                record.SubmittedAt = DateTime.Now;

                _context.DismissalHistories.Add(new DismissalHistory
                {
                    DismissalRecordId = record.Id,
                    Actor = GetCurrentUser(),
                    Action = "SUBMIT",
                    Details = "Gửi yêu cầu duyệt quyết định miễn nhiệm (chuyển qua HR)."
                });

                _notificationEngine.QueueEvent(_context, "DISMISSAL_SUBMITTED", new { 
                    DismissalId = record.Id, 
                    EmployeeName = record.EmployeeNameSnapshot 
                }, null, null, "DIS_SUBMIT_" + record.Id);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi hệ thống.", details = ex.Message });
            }
        }

        // 7. APPROVE
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var record = await _context.DismissalRecords.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (record == null) return NotFound("Không tìm thấy quyết định.");

            if (!HasAccessToRecord(record))
            {
                return StatusCode(403, new { message = "Bạn không có quyền truy cập quyết định này." });
            }

            var approverRole = GetCurrentRole();
            var result = await _approvalEngine.ProcessDecisionAsync(record, "Approve", GetCurrentUser(), approverRole, null, _policy);

            if (result.Success)
            {
                _context.DismissalHistories.Add(new DismissalHistory
                {
                    DismissalRecordId = record.Id,
                    Actor = GetCurrentUser(),
                    Action = approverRole == AppRoles.HR ? "HR_VERIFY" : "LEADER_APPROVE",
                    Details = approverRole == AppRoles.HR ? "HR xác minh quyết định miễn nhiệm." : "Quyết định miễn nhiệm đã được duyệt."
                });
                await _context.SaveChangesAsync();

                if (approverRole == AppRoles.HR && record.ApprovalStatus == "PENDING_LEADER_APPROVAL")
                {
                    await QueueScopedLeaderNotificationsAsync(record);
                    await _context.SaveChangesAsync();
                }
                else if (approverRole == AppRoles.Leader && record.ApprovalStatus == "APPROVED")
                {
                    QueueProposerNotification(record, "DISMISSAL_APPROVED", "APPROVED");
                    await _context.SaveChangesAsync();
                }

                // P0-C3: If Leader approved and EffectiveDate is reached, apply immediately via EffectiveService
                if (approverRole == AppRoles.Leader && record.ApprovalStatus == "APPROVED" && record.EffectiveDate.Date <= DateTime.Today)
                {
                    await _dismissalEffectiveService.ApplyDismissalAsync(record.Id, GetCurrentUser());
                }

                return Ok(new { success = true });
            }

            return BadRequest(result.Message);
        }

        // 8. REJECT
        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return BadRequest("Vui lòng nhập lý do từ chối.");

            var record = await _context.DismissalRecords.FindAsync(id);
            if (record == null) return NotFound("Không tìm thấy quyết định.");

            if (!HasAccessToRecord(record))
            {
                return StatusCode(403, new { message = "Bạn không có quyền truy cập quyết định này." });
            }

            var approverRole = GetCurrentRole();
            var result = await _approvalEngine.ProcessDecisionAsync(record, "Reject", GetCurrentUser(), approverRole, reason, _policy);

            if (result.Success)
            {
                _context.DismissalHistories.Add(new DismissalHistory
                {
                    DismissalRecordId = record.Id,
                    Actor = GetCurrentUser(),
                    Action = "REJECT",
                    Details = $"Từ chối duyệt. Lý do: {reason}"
                });
                await _context.SaveChangesAsync();
                if (approverRole == AppRoles.Leader && record.ApprovalStatus == "REJECTED")
                {
                    QueueProposerNotification(record, "DISMISSAL_REJECTED", "REJECTED");
                    await _context.SaveChangesAsync();
                }
                return Ok(new { success = true });
            }

            return BadRequest(result.Message);
        }

        // 9. CANCEL
        [HttpPost]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            var record = await _context.DismissalRecords.FindAsync(id);
            if (record == null) return NotFound("Không tìm thấy quyết định.");

            if (record.CreatedBy != GetCurrentUser())
            {
                return StatusCode(403, new { message = "Bạn không có quyền hủy quyết định này." });
            }

            if (GetCurrentRole() == AppRoles.Manager && !HasAccessToRecord(record))
            {
                return StatusCode(403, new { message = "Nhân viên không còn thuộc phạm vi quản lý của bạn." });
            }

            if (record.ApprovalStatus != "DRAFT" && record.ApprovalStatus != "PENDING_HR_REVIEW")
                return BadRequest("Chỉ được hủy quyết định ở trạng thái Bản nháp hoặc Chờ HR xác minh.");

            if (record.ApprovalStatus == "CANCELLED") return BadRequest("Hồ sơ đã bị hủy trước đó.");
            if (record.AppliedAt != null) return BadRequest("Quyết định đã có hiệu lực, không thể hủy.");

            record.ApprovalStatus = "CANCELLED";
            record.CancelledBy = GetCurrentUser();
            record.CancelledAt = DateTime.Now;
            record.CancellationReason = reason;

            _context.DismissalHistories.Add(new DismissalHistory
            {
                DismissalRecordId = record.Id,
                Actor = GetCurrentUser(),
                Action = "CANCEL",
                Details = string.IsNullOrWhiteSpace(reason) ? "Hủy quyết định." : $"Hủy quyết định. Lý do: {reason}"
            });

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
