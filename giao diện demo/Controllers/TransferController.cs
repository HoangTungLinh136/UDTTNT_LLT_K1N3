using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Authorization;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = "Manager,HR,Leader")]
    [AutoValidateAntiforgeryToken]
    public class TransferController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IApprovalEngine _approvalEngine;
        private readonly giao_dien_demo.Services.IWorkflowPolicy<TransferRecord> _policy;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;
        private readonly giao_dien_demo.Services.IManagerScopeService _managerScope;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _leaderScope;
        private readonly giao_dien_demo.Services.ITransferEffectiveService _transferEffectiveService;
        private readonly giao_dien_demo.Services.NotificationEngineService _notificationEngine;

        public TransferController(
            ApplicationDbContext context, 
            giao_dien_demo.Services.IApprovalEngine approvalEngine, 
            giao_dien_demo.Services.IWorkflowPolicy<TransferRecord> policy, 
            giao_dien_demo.Services.IAuditLogService auditLogService,
            giao_dien_demo.Services.IManagerScopeService managerScope,
            giao_dien_demo.Services.ILeadershipScopeService leaderScope,
            giao_dien_demo.Services.ITransferEffectiveService transferEffectiveService,
            giao_dien_demo.Services.NotificationEngineService notificationEngine)
        {
            _context = context;
            _approvalEngine = approvalEngine;
            _policy = policy;
            _auditLogService = auditLogService;
            _managerScope = managerScope;
            _leaderScope = leaderScope;
            _transferEffectiveService = transferEffectiveService;
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

        private bool HasAccessToRecord(TransferRecord record)
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

        // 1. INDEX — Danh sách thuyên chuyển
        public async Task<IActionResult> Index(
            string? searchName,
            string? department,
            string? status,
            DateTime? effectiveDate,
            int pageNumber = 1,
            int pageSize = 10)
        {
            var role = GetCurrentRole();
            var query = _context.TransferRecords
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
                query = query.Where(a => a.TargetDepartment == department);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = status == "EFFECTIVE" ? query.Where(a => a.ApprovalStatus == "EFFECTIVE" || a.AppliedAt != null) : query.Where(a => a.ApprovalStatus == status);
            }

            if (effectiveDate.HasValue)
            {
                query = query.Where(a => a.EffectiveDate.Date == effectiveDate.Value.Date);
            }

            var baseQuery = query;
            ViewBag.StatTotal = await baseQuery.CountAsync();
            ViewBag.StatPendingHR = await baseQuery.CountAsync(r => r.ApprovalStatus == "PENDING_HR_REVIEW");
            ViewBag.StatPendingLeader = await baseQuery.CountAsync(r => r.ApprovalStatus == "PENDING_LEADER_APPROVAL");
            ViewBag.StatApprovedOnly = await baseQuery.CountAsync(r => r.ApprovalStatus == "APPROVED" && r.AppliedAt == null);
            ViewBag.StatEffective = await baseQuery.CountAsync(r => r.ApprovalStatus == "EFFECTIVE" || r.AppliedAt != null);
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 5 || pageSize > 100) pageSize = 10;
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0 && pageNumber > totalPages) pageNumber = totalPages;
            var records = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            // Calculate Statistics
            ViewBag.StatPending = records.Count(r => r.ApprovalStatus == "PENDING_APPROVAL" || r.ApprovalStatus == "PENDING_HR_REVIEW" || r.ApprovalStatus == "PENDING_LEADER_APPROVAL");
            ViewBag.StatApproved = records.Count(r => r.ApprovalStatus == "APPROVED" || r.ApprovalStatus == "EFFECTIVE");
            ViewBag.StatUpcoming = records.Count(r => r.ApprovalStatus == "APPROVED" && r.EffectiveDate.Date > DateTime.Today && r.AppliedAt == null);
            ViewBag.StatRejected = records.Count(r => r.ApprovalStatus == "REJECTED");
            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = pageNumber;
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

            ViewBag.EligibleManagers = await _context.Employees
                .Where(e => e.Status == "Active")
                .OrderBy(e => e.Name)
                .ToListAsync();

            ViewBag.CurrentSearch = searchName;
            ViewBag.CurrentDept = department;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentEffectiveDate = effectiveDate?.ToString("yyyy-MM-dd");
            ViewBag.Role = role;

            return View(records);
        }

        // 2. DETAIL — Lấy chi tiết thuyên chuyển (AJAX)
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var record = await _context.TransferRecords
                .Include(a => a.Histories)
                .Include(a => a.TargetManager)
                .Include(a => a.TargetDepartmentEntity)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (record == null) return NotFound();

            if (!HasAccessToRecord(record))
                return StatusCode(403, new { message = "Bạn không có quyền truy cập quyết định này." });

            // Sắp xếp lịch sử mới nhất lên đầu
            record.Histories = record.Histories.OrderByDescending(h => h.Timestamp).ToList();

            return Json(new { success = true, data = record });
        }

        // 3. TÌM KIẾM NHÂN VIÊN ĐỂ TẠO MỚI (AJAX)
        [HttpGet]
        public async Task<IActionResult> SearchEmployee(string query)
        {
            var role = GetCurrentRole();
            var empQuery = _context.Employees.AsQueryable();

            if (role == AppRoles.Manager)
            {
                var allowed = _managerScope.GetManagedOrganizationalUnits();
                empQuery = empQuery.Where(e => allowed.Contains(e.Department));
            }
            else if (role != AppRoles.HR)
            {
                return Json(new List<object>());
            }

            if (string.IsNullOrWhiteSpace(query)) return Json(new List<object>());

            var employees = await empQuery
                .Where(e => e.Name.Contains(query) || e.EmployeeCode.Contains(query))
                .Select(e => new {
                    e.Id,
                    e.EmployeeCode,
                    e.Name,
                    e.Department,
                    e.Position,
                    e.JobTitle
                })
                .Take(10)
                .ToListAsync();

            return Json(employees);
        }

        // 4. CREATE — Tạo mới (Lưu nháp hoặc Trình duyệt)
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Create([FromBody] TransferRecord model, [FromQuery] string? actionType = null, [FromQuery] bool? isSubmit = null)
        {
            if (string.IsNullOrEmpty(actionType) && isSubmit == true)
            {
                actionType = "SUBMIT";
            }

            var employee = await _context.Employees.FindAsync(model.EmployeeId);
            if (employee == null) return Json(new { success = false, message = "Nhân viên không tồn tại." });

            var role = GetCurrentRole();
            if (role == AppRoles.Manager)
            {
                var allowed = _managerScope.GetManagedOrganizationalUnits();
                if (!allowed.Contains(employee.Department))
                {
                    return Json(new { success = false, message = "Bạn không có quyền thao tác trên nhân viên này." });
                }
            }

            // Kiểm tra xem có thực sự thay đổi không
            if (employee.Department == model.TargetDepartment &&
                employee.Position == model.TargetPosition &&
                employee.JobTitle == model.TargetJobTitle && employee.ManagerId == model.TargetManagerId)
            {
                return Json(new { success = false, message = "Không có thay đổi nào về phòng ban, vị trí hoặc chức danh để thực hiện thuyên chuyển." });
            }

            if (!model.TargetDepartmentId.HasValue && !string.IsNullOrEmpty(model.TargetDepartment))
            {
                var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == model.TargetDepartment);
                if (dept != null)
                {
                    model.TargetDepartmentId = dept.Id;
                }
            }

            model.EmployeeCodeSnapshot = employee.EmployeeCode;
            model.EmployeeNameSnapshot = employee.Name;
            model.CurrentDepartmentSnapshot = employee.Department;
            model.CurrentPositionSnapshot = employee.Position;
            model.CurrentJobTitleSnapshot = employee.JobTitle;

            model.CreatedBy = GetCurrentUser();
            model.CreatedAt = DateTime.Now;

            if (actionType == "SUBMIT")
            {
                model.ApprovalStatus = "PENDING_HR_REVIEW";
                model.SubmittedBy = GetCurrentUser();
                model.SubmittedAt = DateTime.Now;
            }
            else
            {
                model.ApprovalStatus = "DRAFT";
            }

            _context.TransferRecords.Add(model);
            await _context.SaveChangesAsync();

            // Log history
            var history = new TransferHistory
            {
                TransferRecordId = model.Id,
                Actor = GetCurrentUser(),
                Action = actionType == "SUBMIT" ? "SUBMIT" : "CREATE",
                Details = actionType == "SUBMIT" ? "Tạo mới và trình duyệt thuyên chuyển (chuyển qua HR)" : "Tạo mới bản nháp thuyên chuyển"
            };
            _context.TransferHistories.Add(history);
            await _context.SaveChangesAsync();

            if (actionType == "SUBMIT")
            {
                _notificationEngine.QueueEvent(_context, "TRANSFER_SUBMITTED", new { 
                    TransferId = model.Id, 
                    EmployeeName = model.EmployeeNameSnapshot 
                }, null, null, "TRA_SUBMIT_" + model.Id);
                await _context.SaveChangesAsync();
            }

            await _auditLogService.LogAsync("Create", "EmployeeLifecycle", "Transfer", model.Id.ToString(), "Activity", "Success");

            return Json(new { success = true });
        }

        // 5. UPDATE — Cập nhật bản nháp
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Update([FromBody] TransferRecord model, [FromQuery] string? actionType = null, [FromQuery] bool? isSubmit = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var record = await _context.TransferRecords.FindAsync(model.Id);
                if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ thuyên chuyển." });
                await _context.Entry(record).ReloadAsync();

                if (record.CreatedBy != GetCurrentUser())
                {
                    return Json(new { success = false, message = "Bạn không có quyền sửa quyết định này." });
                }

                if (GetCurrentRole() == AppRoles.Manager && !HasAccessToRecord(record))
                {
                    return Json(new { success = false, message = "Nhân viên không còn thuộc phạm vi quản lý của bạn." });
                }

                if (record.ApprovalStatus != "DRAFT") 
                    return Json(new { success = false, message = "Chỉ được phép sửa bản nháp." });

                record.TargetDepartment = model.TargetDepartment;
                record.TargetPosition = model.TargetPosition;
                record.TargetJobTitle = model.TargetJobTitle;
                record.TargetDepartmentId = model.TargetDepartmentId;
                record.TargetManagerId = model.TargetManagerId;
                record.AllowNullManagerException = model.AllowNullManagerException;
                record.NullManagerReason = model.NullManagerReason;

                if (!record.TargetDepartmentId.HasValue && !string.IsNullOrEmpty(record.TargetDepartment))
                {
                    var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == record.TargetDepartment);
                    if (dept != null)
                    {
                        record.TargetDepartmentId = dept.Id;
                    }
                }
                record.EffectiveDate = model.EffectiveDate;
                record.Reason = model.Reason;
                record.Notes = model.Notes;
                record.DecisionNumber = model.DecisionNumber;
                record.DecisionDate = model.DecisionDate;

                if (actionType == "SUBMIT")
                {
                    record.ApprovalStatus = "PENDING_HR_REVIEW";
                    record.SubmittedBy = GetCurrentUser();
                    record.SubmittedAt = DateTime.Now;

                    _notificationEngine.QueueEvent(_context, "TRANSFER_SUBMITTED", new { 
                        TransferId = record.Id, 
                        EmployeeName = record.EmployeeNameSnapshot 
                    }, null, null, "TRA_SUBMIT_" + record.Id);
                }

                // Log history
                var history = new TransferHistory
                {
                    TransferRecordId = record.Id,
                    Actor = GetCurrentUser(),
                    Action = actionType == "SUBMIT" ? "SUBMIT" : "UPDATE",
                    Details = actionType == "SUBMIT" ? "Trình duyệt thuyên chuyển (chuyển qua HR)" : "Cập nhật bản nháp"
                };
                _context.TransferHistories.Add(history);
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _auditLogService.LogAsync("Update", "EmployeeLifecycle", "Transfer", record.Id.ToString(), "Activity", "Success");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống.", details = ex.Message });
            }
        }

        // 6. APPROVE — Duyệt
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var record = await _context.TransferRecords.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (!HasAccessToRecord(record))
            {
                return Json(new { success = false, message = "Bạn không có quyền truy cập quyết định này." });
            }

            var approverRole = GetCurrentRole();
            var result = await _approvalEngine.ProcessDecisionAsync(record, "Approve", GetCurrentUser(), approverRole, null, _policy);

            if (result.Success)
            {
                var history = new TransferHistory
                {
                    TransferRecordId = record.Id,
                    Actor = GetCurrentUser(),
                    Action = approverRole == AppRoles.HR ? "HR_VERIFY" : "LEADER_APPROVE",
                    Details = approverRole == AppRoles.HR ? "HR xác minh quyết định thuyên chuyển" : "Lãnh đạo duyệt quyết định thuyên chuyển"
                };
                _context.TransferHistories.Add(history);
                await _context.SaveChangesAsync();

                await _auditLogService.LogAsync("Approve", "EmployeeLifecycle", "Transfer", record.Id.ToString(), "Activity", "Success");

                // P0-C3: If Leader approved and EffectiveDate is reached, apply immediately via EffectiveService
                if (approverRole == AppRoles.Leader && record.ApprovalStatus == "APPROVED" && record.EffectiveDate.Date <= DateTime.Today)
                {
                    await _transferEffectiveService.ApplyTransferAsync(record.Id, GetCurrentUser());
                }
            }

            return Json(new { success = result.Success, message = result.Message });
        }

        // 7. REJECT — Từ chối
        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return Json(new { success = false, message = "Vui lòng nhập lý do từ chối." });

            var record = await _context.TransferRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (!HasAccessToRecord(record))
            {
                return Json(new { success = false, message = "Bạn không có quyền truy cập quyết định này." });
            }

            var approverRole = GetCurrentRole();
            var result = await _approvalEngine.ProcessDecisionAsync(record, "Reject", GetCurrentUser(), approverRole, reason, _policy);

            if (result.Success)
            {
                var history = new TransferHistory
                {
                    TransferRecordId = record.Id,
                    Actor = GetCurrentUser(),
                    Action = "REJECT",
                    Details = $"Từ chối: {reason}"
                };
                _context.TransferHistories.Add(history);
                await _context.SaveChangesAsync();

                await _auditLogService.LogAsync("Reject", "EmployeeLifecycle", "Transfer", record.Id.ToString(), "Activity", "Success");
            }

            return Json(new { success = result.Success, message = result.Message });
        }

        // 8. CANCEL — Hủy
        [HttpPost]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            var record = await _context.TransferRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (record.CreatedBy != GetCurrentUser())
            {
                return Json(new { success = false, message = "Bạn không có quyền hủy quyết định này." });
            }

            if (GetCurrentRole() == AppRoles.Manager && !HasAccessToRecord(record))
            {
                return Json(new { success = false, message = "Nhân viên không còn thuộc phạm vi quản lý của bạn." });
            }

            if (record.ApprovalStatus != "DRAFT" && record.ApprovalStatus != "PENDING_HR_REVIEW")
                return Json(new { success = false, message = "Chỉ được hủy quyết định ở trạng thái Bản nháp hoặc Chờ HR xác minh." });

            // Chỉ cho phép hủy nếu chưa apply
            if (record.AppliedAt != null)
                return Json(new { success = false, message = "Không thể hủy hồ sơ đã được áp dụng hiệu lực." });

            record.ApprovalStatus = "CANCELLED";
            record.CancelledBy = GetCurrentUser();
            record.CancelledAt = DateTime.Now;
            record.CancellationReason = reason;

            var history = new TransferHistory
            {
                TransferRecordId = record.Id,
                Actor = GetCurrentUser(),
                Action = "CANCEL",
                Details = $"Hủy: {reason}"
            };
            _context.TransferHistories.Add(history);

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync("Cancel", "EmployeeLifecycle", "Transfer", record.Id.ToString(), "Activity", "Success", null, null, null, reason);

            return Json(new { success = true });
        }
    }
}
