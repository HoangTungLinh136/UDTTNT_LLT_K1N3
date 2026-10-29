using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader}")]
    [AutoValidateAntiforgeryToken]
    public class OffboardingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IApprovalEngine _approvalEngine;
        private readonly giao_dien_demo.Services.IWorkflowPolicy<OffboardingRecord> _policy;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _leadershipScope;

        public OffboardingController(
            ApplicationDbContext context,
            giao_dien_demo.Services.IApprovalEngine approvalEngine,
            giao_dien_demo.Services.IWorkflowPolicy<OffboardingRecord> policy,
            giao_dien_demo.Services.IAuditLogService auditLogService,
            giao_dien_demo.Services.ILeadershipScopeService leadershipScope)
        {
            _context = context;
            _approvalEngine = approvalEngine;
            _policy = policy;
            _auditLogService = auditLogService;
            _leadershipScope = leadershipScope;
        }

        private string GetCurrentUser()
        {
            return CurrentActor.Username(User) ?? "System";
        }

        // 1. INDEX — Danh sách nghỉ việc
        public async Task<IActionResult> Index(
            string? searchName,
            string? department,
            string? status,
            DateTime? lastWorkingDate)
        {
            var role = CurrentActor.Role(User);
            var isLeader = role == AppRoles.Leader;
            var leaderUnits = isLeader ? _leadershipScope.GetOverseenOrganizationalUnits() : new List<string>();

            var query = _context.OffboardingRecords
                .Include(a => a.Employee)
                .AsQueryable();

            if (isLeader)
            {
                if (leaderUnits.Count == 0)
                {
                    query = query.Where(a => false);
                }
                else
                {
                    query = query.Where(a =>
                        (a.DepartmentSnapshot != null && leaderUnits.Contains(a.DepartmentSnapshot)) ||
                        (a.DepartmentSnapshot == null && a.Employee != null && a.Employee.Department != null && leaderUnits.Contains(a.Employee.Department)));
                }
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(a => 
                    (a.EmployeeNameSnapshot != null && a.EmployeeNameSnapshot.Contains(searchName)) ||
                    (a.EmployeeCodeSnapshot != null && a.EmployeeCodeSnapshot.Contains(searchName)));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(a => a.DepartmentSnapshot == department);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.OffboardingStatus == status);
            }

            if (lastWorkingDate.HasValue)
            {
                query = query.Where(a => a.LastWorkingDate.Date == lastWorkingDate.Value.Date);
            }

            var records = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

            // Tính số liệu cho Checklist hiển thị ở Table
            var recordIds = records.Select(r => r.Id).ToList();
            var tasks = await _context.OffboardingChecklistTasks
                .Where(t => recordIds.Contains(t.OffboardingRecordId) && t.IsMandatory)
                .ToListAsync();

            ViewBag.TasksDict = tasks.GroupBy(t => t.OffboardingRecordId)
                .ToDictionary(g => g.Key, g => new {
                    Total = g.Count(),
                    Completed = g.Count(t => t.Status == "DONE" || t.Status == "SKIPPED")
                });

            // Calculate Statistics according to scope
            var allRecordsQuery = _context.OffboardingRecords.AsQueryable();
            if (isLeader)
            {
                if (leaderUnits.Count == 0)
                {
                    allRecordsQuery = allRecordsQuery.Where(a => false);
                }
                else
                {
                    allRecordsQuery = allRecordsQuery.Where(a =>
                        (a.DepartmentSnapshot != null && leaderUnits.Contains(a.DepartmentSnapshot)) ||
                        (a.DepartmentSnapshot == null && a.Employee != null && a.Employee.Department != null && leaderUnits.Contains(a.Employee.Department)));
                }
                ViewBag.Departments = leaderUnits;
            }
            else
            {
                ViewBag.Departments = await _context.Employees
                    .Where(e => !string.IsNullOrEmpty(e.Department))
                    .Select(e => e.Department!)
                    .Distinct()
                    .ToListAsync();
            }

            var allRecords = await allRecordsQuery.ToListAsync();
            
            ViewBag.StatProcessing = allRecords.Count(r => r.OffboardingStatus == "PROCESSING");
            ViewBag.StatHandoverPending = allRecords.Count(r => r.OffboardingStatus == "HANDOVER_PENDING");
            ViewBag.StatSettlementPending = allRecords.Count(r => r.OffboardingStatus == "SETTLEMENT_PENDING");
            ViewBag.StatCompleted = allRecords.Count(r => r.OffboardingStatus == "COMPLETED");

            ViewBag.CurrentSearch = searchName;
            ViewBag.CurrentDept = department;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentLastWorkingDate = lastWorkingDate?.ToString("yyyy-MM-dd");

            return View(records);
        }

        // 2. DETAIL — Lấy chi tiết
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var record = await _context.OffboardingRecords
                .Include(a => a.Histories)
                .Include(a => a.ChecklistTasks)
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (record == null) return NotFound();

            var role = CurrentActor.Role(User);
            if (role == AppRoles.Leader)
            {
                var targetDept = record.DepartmentSnapshot ?? record.Employee?.Department;
                if (!_leadershipScope.CanAccessDepartment(targetDept))
                {
                    return Forbid();
                }
            }

            // Sắp xếp lịch sử
            record.Histories = record.Histories.OrderByDescending(h => h.Timestamp).ToList();
            
            // Tìm người phụ trách nếu có
            string responsiblePersonName = "N/A";
            if (record.ResponsiblePersonId.HasValue)
            {
                var responsible = await _context.Employees.FindAsync(record.ResponsiblePersonId.Value);
                if (responsible != null) responsiblePersonName = responsible.Name;
            }

            // Tính số ngày phép hiện tại (đơn giản, giả định 12 ngày / năm - số ngày đã nghỉ)
            decimal leaveBalance = 0;
            if (record.Employee != null)
            {
                int usedLeaves = await _context.Leaves
                    .Where(l => l.EmployeeId == record.EmployeeId && l.Status == "Đã duyệt" && l.FromDate.Year == DateTime.Now.Year)
                    .SumAsync(l => (l.ToDate.Date - l.FromDate.Date).Days + 1);
                leaveBalance = 12 - usedLeaves;
            }

            return Json(new { 
                success = true, 
                data = record, 
                responsiblePersonName,
                leaveBalance
            });
        }

        // 3. TÌM KIẾM NHÂN VIÊN ĐỂ TẠO MỚI (AJAX)
        [HttpGet]
        public async Task<IActionResult> SearchEmployee(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Json(new List<object>());

            var role = CurrentActor.Role(User);
            var employeesQuery = role == AppRoles.Leader
                ? _leadershipScope.GetScopedEmployeeQuery()
                : _context.Employees.AsQueryable();

            var employees = await employeesQuery
                .Where(e => (e.Name != null && e.Name.Contains(query)) || (e.EmployeeCode != null && e.EmployeeCode.Contains(query)))
                .Where(e => e.Position != "Đã nghỉ" && e.Status != "Offboarded") // Không hiển thị NV đã nghỉ hoặc offboarded
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
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Create(OffboardingRecord model, string actionType)
        {
            var employee = await _context.Employees.FindAsync(model.EmployeeId);
            if (employee == null) return Json(new { success = false, message = "Nhân viên không tồn tại." });

            // Check if there's already an active offboarding
            bool existingActive = await _context.OffboardingRecords.AnyAsync(o => 
                o.EmployeeId == model.EmployeeId && 
                o.ApprovalStatus != "REJECTED" && 
                o.ApprovalStatus != "CANCELLED" && 
                o.OffboardingStatus != "COMPLETED");
            if (existingActive)
            {
                return Json(new { success = false, message = "Nhân viên này đang có một hồ sơ nghỉ việc chưa hoàn tất." });
            }

            model.EmployeeCodeSnapshot = employee.EmployeeCode;
            model.EmployeeNameSnapshot = employee.Name;
            model.DepartmentSnapshot = employee.Department;
            model.PositionSnapshot = employee.Position;

            model.CreatedBy = GetCurrentUser();
            model.CreatedAt = DateTime.Now;

            if (actionType == "SUBMIT")
            {
                model.ApprovalStatus = "PENDING_APPROVAL";
                model.SubmittedBy = GetCurrentUser();
                model.SubmittedAt = DateTime.Now;
            }
            else
            {
                model.ApprovalStatus = "DRAFT";
            }

            // Generate default checklists
            var defaultTasks = new List<OffboardingChecklistTask>
            {
                new OffboardingChecklistTask { TaskName = "Bàn giao tài liệu, source code, data", Category = "Công việc", IsMandatory = true },
                new OffboardingChecklistTask { TaskName = "Bàn giao thiết bị, máy tính, thẻ từ", Category = "Tài sản", IsMandatory = true },
                new OffboardingChecklistTask { TaskName = "Hủy quyền truy cập hệ thống (VPN, Email, ERP)", Category = "Quyền truy cập", IsMandatory = true },
                new OffboardingChecklistTask { TaskName = "Thanh toán/thu hồi công nợ tạm ứng", Category = "Tài chính", IsMandatory = false }
            };
            
            foreach (var t in defaultTasks)
            {
                model.ChecklistTasks.Add(t);
            }

            _context.OffboardingRecords.Add(model);
            await _context.SaveChangesAsync();

            // Log history
            var history = new OffboardingHistory
            {
                OffboardingRecordId = model.Id,
                Actor = GetCurrentUser(),
                Action = "CREATE",
                Details = actionType == "SUBMIT" ? "Tạo mới và trình duyệt hồ sơ nghỉ việc" : "Tạo mới bản nháp nghỉ việc"
            };
            _context.OffboardingHistories.Add(history);
            await _context.SaveChangesAsync();

            _ = _auditLogService.LogAsync("Create", "EmployeeLifecycle", "Offboarding", model.Id.ToString(), "Activity", "Success");

            return Json(new { success = true });
        }

        // 5. UPDATE — Cập nhật bản nháp
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Update(OffboardingRecord model, string actionType)
        {
            var record = await _context.OffboardingRecords.FindAsync(model.Id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (record.ApprovalStatus != "DRAFT") 
                return Json(new { success = false, message = "Chỉ được phép sửa bản nháp." });

            record.ExitType = model.ExitType;
            record.NoticeDate = model.NoticeDate;
            record.LastWorkingDate = model.LastWorkingDate;
            record.EffectiveDate = model.EffectiveDate;
            record.Reason = model.Reason;
            record.Notes = model.Notes;
            record.ResponsiblePersonId = model.ResponsiblePersonId;

            if (actionType == "SUBMIT")
            {
                record.ApprovalStatus = "PENDING_APPROVAL";
                record.SubmittedBy = GetCurrentUser();
                record.SubmittedAt = DateTime.Now;
            }

            // Log history
            var history = new OffboardingHistory
            {
                OffboardingRecordId = record.Id,
                Actor = GetCurrentUser(),
                Action = actionType == "SUBMIT" ? "SUBMIT" : "UPDATE",
                Details = actionType == "SUBMIT" ? "Trình duyệt hồ sơ" : "Cập nhật bản nháp"
            };
            _context.OffboardingHistories.Add(history);
            
            await _context.SaveChangesAsync();
            
            _ = _auditLogService.LogAsync("Update", "EmployeeLifecycle", "Offboarding", record.Id.ToString(), "Activity", "Success");

            return Json(new { success = true });
        }

        // 6. APPROVE — Duyệt
        [HttpPost]
        [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader}")]
        public async Task<IActionResult> Approve(int id)
        {
            var record = await _context.OffboardingRecords.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            var approverRole = CurrentActor.Role(User);
            if (approverRole == AppRoles.Leader)
            {
                var targetDept = record.DepartmentSnapshot ?? record.Employee?.Department;
                if (!_leadershipScope.CanAccessDepartment(targetDept))
                {
                    return Forbid();
                }
            }

            var result = await _approvalEngine.ProcessDecisionAsync(record, "Approve", GetCurrentUser(), approverRole ?? "Unknown", null, _policy);

            if (result.Success)
            {
                var history = new OffboardingHistory
                {
                    OffboardingRecordId = record.Id,
                    Actor = GetCurrentUser(),
                    Action = "APPROVE",
                    Details = "Duyệt hồ sơ nghỉ việc"
                };
                _context.OffboardingHistories.Add(history);
                await _context.SaveChangesAsync();
                _ = _auditLogService.LogAsync("Approve", "EmployeeLifecycle", "Offboarding", record.Id.ToString(), "Activity", "Success");
                return Json(new { success = true });
            }

            return Json(new { success = false, message = result.Message });
        }

        // 7. REJECT — Từ chối
        [HttpPost]
        [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader}")]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return Json(new { success = false, message = "Vui lòng nhập lý do từ chối." });

            var record = await _context.OffboardingRecords.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            var approverRole = CurrentActor.Role(User);
            if (approverRole == AppRoles.Leader)
            {
                var targetDept = record.DepartmentSnapshot ?? record.Employee?.Department;
                if (!_leadershipScope.CanAccessDepartment(targetDept))
                {
                    return Forbid();
                }
            }

            var result = await _approvalEngine.ProcessDecisionAsync(record, "Reject", GetCurrentUser(), approverRole ?? "Unknown", reason, _policy);

            if (result.Success)
            {
                var history = new OffboardingHistory
                {
                    OffboardingRecordId = record.Id,
                    Actor = GetCurrentUser(),
                    Action = "REJECT",
                    Details = $"Từ chối: {reason}"
                };
                _context.OffboardingHistories.Add(history);
                await _context.SaveChangesAsync();
                _ = _auditLogService.LogAsync("Reject", "EmployeeLifecycle", "Offboarding", record.Id.ToString(), "Activity", "Success");
                return Json(new { success = true });
            }

            return Json(new { success = false, message = result.Message });
        }

        // 8. CANCEL — Hủy
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            var record = await _context.OffboardingRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (record.OffboardingStatus == "COMPLETED")
                return Json(new { success = false, message = "Không thể hủy hồ sơ đã hoàn tất." });

            record.ApprovalStatus = "CANCELLED";
            record.CancelledBy = GetCurrentUser();
            record.CancelledAt = DateTime.Now;
            record.CancellationReason = reason;

            var history = new OffboardingHistory
            {
                OffboardingRecordId = record.Id,
                Actor = GetCurrentUser(),
                Action = "CANCEL",
                Details = $"Hủy: {reason}"
            };
            _context.OffboardingHistories.Add(history);

            await _context.SaveChangesAsync();
            _ = _auditLogService.LogAsync("Cancel", "EmployeeLifecycle", "Offboarding", record.Id.ToString(), "Activity", "Success", null, null, null, reason);
            return Json(new { success = true });
        }

        // 9. CHECKLIST ACTIONS
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> UpdateChecklistStatus(int taskId, string status, string notes)
        {
            var task = await _context.OffboardingChecklistTasks
                .Include(t => t.OffboardingRecord)
                .FirstOrDefaultAsync(t => t.Id == taskId);
            
            if (task == null) return Json(new { success = false, message = "Task không tồn tại." });

            task.Status = status;
            task.Notes = notes;
            if (status == "DONE" || status == "SKIPPED")
            {
                task.CompletedAt = DateTime.Now;
                task.CompletedBy = GetCurrentUser();
            }

            // Update OffboardingStatus if all required tasks are done
            var allTasks = await _context.OffboardingChecklistTasks
                .Where(t => t.OffboardingRecordId == task.OffboardingRecordId)
                .ToListAsync();

            bool allRequiredDone = allTasks.Where(t => t.IsMandatory).All(t => t.Status == "DONE" || t.Status == "SKIPPED");
            if (allRequiredDone && task.OffboardingRecord.OffboardingStatus == "PROCESSING")
            {
                task.OffboardingRecord.OffboardingStatus = "HANDOVER_PENDING"; // Move to next logically
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // 10. CLOSING ATTENDANCE
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> CloseAttendance(int id)
        {
            var record = await _context.OffboardingRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            record.AttendanceClosingStatus = "COMPLETED";
            
            _context.OffboardingHistories.Add(new OffboardingHistory {
                OffboardingRecordId = id,
                Actor = GetCurrentUser(),
                Action = "CLOSE_ATTENDANCE",
                Details = $"Chốt công đến ngày {record.LastWorkingDate:dd/MM/yyyy}"
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // 11. CLOSING LEAVE
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> CloseLeave(int id)
        {
            var record = await _context.OffboardingRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            record.LeaveClosingStatus = "COMPLETED";
            
            _context.OffboardingHistories.Add(new OffboardingHistory {
                OffboardingRecordId = id,
                Actor = GetCurrentUser(),
                Action = "CLOSE_LEAVE",
                Details = "Xác nhận chốt phép"
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // 12. SEND SETTLEMENT (Chuyển quyết toán)
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> SendSettlement(int id)
        {
            var record = await _context.OffboardingRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            // Require attendance closing
            if (record.AttendanceClosingStatus != "COMPLETED")
                return Json(new { success = false, message = "Vui lòng chốt công trước khi chuyển quyết toán." });

            record.SettlementStatus = "COMPLETED"; // Giả lập hoàn tất luôn (do Payroll hiện chưa có Offboarding Payroll entity)
            record.OffboardingStatus = "SETTLEMENT_PENDING"; // Đang đợi hoàn tất nghỉ việc cuối cùng

            _context.OffboardingHistories.Add(new OffboardingHistory {
                OffboardingRecordId = id,
                Actor = GetCurrentUser(),
                Action = "SEND_SETTLEMENT",
                Details = "Chuyển dữ liệu quyết toán sang C&B/Kế toán"
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // 13. COMPLETE OFFBOARDING
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Complete(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var record = await _context.OffboardingRecords.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
                if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

                if (record.OffboardingStatus == "COMPLETED")
                {
                    // Idempotent return
                    return Json(new { success = true });
                }

                if (record.ApprovalStatus != "APPROVED")
                    return Json(new { success = false, message = "Hồ sơ chưa được duyệt." });

                // Validate Requirements
                var requiredTasks = await _context.OffboardingChecklistTasks
                    .Where(t => t.OffboardingRecordId == id && t.IsMandatory)
                    .ToListAsync();
                
                if (requiredTasks.Any(t => t.Status != "DONE" && t.Status != "SKIPPED"))
                    return Json(new { success = false, message = "Chưa hoàn thành tất cả công việc bàn giao bắt buộc." });

                if (record.AttendanceClosingStatus != "COMPLETED")
                    return Json(new { success = false, message = "Chưa chốt công." });

                record.OffboardingStatus = "COMPLETED";
                record.CompletedBy = GetCurrentUser();
                record.CompletedAt = DateTime.Now;

                _context.OffboardingHistories.Add(new OffboardingHistory {
                    OffboardingRecordId = id,
                    Actor = GetCurrentUser(),
                    Action = "COMPLETE_OFFBOARDING",
                    Details = "Xác nhận hoàn tất thủ tục nghỉ việc."
                });

                // Nếu Effective Date đã đến, chuyển trạng thái Employee ngay
                if (record.EffectiveDate.Date <= DateTime.Today && record.Employee != null)
                {
                    // OD-12 Guard: Kiểm tra người quản lý còn cấp dưới trực tiếp đang Active không
                    bool hasActiveSubordinates = await _context.Employees
                        .AnyAsync(e => e.ManagerId == record.EmployeeId && e.Status == "Active");

                    if (hasActiveSubordinates)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { 
                            success = false, 
                            message = "Không thể hoàn tất bước có hiệu lực nghỉ việc: Người quản lý vẫn còn cấp dưới trực tiếp đang hoạt động. Cần tái phân công cấp dưới hoặc phê duyệt ngoại lệ trước khi hoàn tất (OD-12)." 
                        });
                    }

                    record.Employee.Status = "Offboarded";
                    
                    // Khóa tài khoản User tương ứng
                    var userAccount = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == record.EmployeeId);
                    if (userAccount != null) {
                        userAccount.CanLogin = false;
                    }

                    _context.OffboardingHistories.Add(new OffboardingHistory {
                        OffboardingRecordId = id,
                        Actor = "System",
                        Action = "APPLY",
                        Details = "Tự động chuyển trạng thái Nhân viên thành Offboarded"
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                await _auditLogService.LogAsync("Complete", "EmployeeLifecycle", "Offboarding", record.Id.ToString(), "Activity", "Success");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Đã xảy ra lỗi khi hoàn tất thủ tục: " + ex.Message });
            }
        }
    }
}
