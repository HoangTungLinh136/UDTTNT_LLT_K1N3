using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public interface ITransferEffectiveService
    {
        Task<bool> ApplyTransferAsync(int transferRecordId, string actor = "SYSTEM");
        Task<int> ProcessPendingTransfersAsync(CancellationToken cancellationToken = default);
    }

    public class TransferEffectiveService : ITransferEffectiveService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TransferEffectiveService> _logger;
        private readonly IAuditLogService _auditLogService;
        private readonly IInAppNotificationService _notificationService;

        public TransferEffectiveService(
            ApplicationDbContext context,
            ILogger<TransferEffectiveService> logger,
            IAuditLogService auditLogService,
            IInAppNotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _auditLogService = auditLogService;
            _notificationService = notificationService;
        }

        public async Task<bool> ApplyTransferAsync(int transferRecordId, string actor = "SYSTEM")
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var record = await _context.TransferRecords
                    .Include(t => t.Employee)
                    .FirstOrDefaultAsync(t => t.Id == transferRecordId);

                if (record == null)
                {
                    _logger.LogWarning($"[TransferEffective] Record {transferRecordId} not found.");
                    return false;
                }

                // Idempotency: skip if already applied or not in APPROVED status
                if (record.ApprovalStatus == "EFFECTIVE" || record.AppliedAt != null)
                {
                    _logger.LogInformation($"[TransferEffective] Record {transferRecordId} has already been applied at {record.AppliedAt}. Skipping.");
                    return false;
                }

                if (record.ApprovalStatus != "APPROVED")
                {
                    _logger.LogWarning($"[TransferEffective] Record {transferRecordId} cannot be applied because status is '{record.ApprovalStatus}' (expected 'APPROVED').");
                    return false;
                }

                if (record.EffectiveDate.Date > DateTime.Today)
                {
                    _logger.LogInformation($"[TransferEffective] Record {transferRecordId} has future EffectiveDate ({record.EffectiveDate:yyyy-MM-dd}). Not yet due.");
                    return false;
                }

                if (record.Employee == null)
                {
                    _logger.LogError($"[TransferEffective] Record {transferRecordId} references non-existing EmployeeId {record.EmployeeId}. Cannot apply.");
                    return false;
                }

                // Validation of required target department
                if (string.IsNullOrWhiteSpace(record.TargetDepartment))
                {
                    _logger.LogWarning($"[TransferEffective] Record {transferRecordId} has empty TargetDepartment. Skipping to avoid corrupting Employee master.");
                    return false;
                }

                var emp = record.Employee;
                string oldDept = emp.Department ?? string.Empty;
                string oldPos = emp.Position ?? string.Empty;
                string oldTitle = emp.JobTitle ?? string.Empty;

                // Mutate Employee Master
                emp.Department = record.TargetDepartment;
                if (!string.IsNullOrWhiteSpace(record.TargetPosition))
                {
                    emp.Position = record.TargetPosition;
                }
                if (!string.IsNullOrWhiteSpace(record.TargetJobTitle))
                {
                    emp.JobTitle = record.TargetJobTitle;
                }

                // Mark Record as EFFECTIVE
                record.ApprovalStatus = "EFFECTIVE";
                record.AppliedAt = DateTime.Now;
                record.AppliedBy = actor;

                // Section 5: Security Check for Active Manager / Leader Assignments
                var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == emp.Id);
                bool hasActiveAssignments = false;
                string assignmentWarning = string.Empty;

                if (user != null)
                {
                    var now = DateTime.Now;
                    var activeMgrAssignments = await _context.ManagerAssignments
                        .Where(a => a.UserId == user.Id && (a.EffectiveTo == null || a.EffectiveTo > now))
                        .ToListAsync();

                    var activeLdrAssignments = await _context.LeaderAssignments
                        .Where(a => a.UserId == user.Id && (a.EffectiveTo == null || a.EffectiveTo > now))
                        .ToListAsync();

                    if (activeMgrAssignments.Any() || activeLdrAssignments.Any())
                    {
                        hasActiveAssignments = true;
                        var mgrUnits = string.Join(", ", activeMgrAssignments.Select(m => m.OrganizationalUnit));
                        var ldrUnits = string.Join(", ", activeLdrAssignments.Select(l => l.OrganizationalUnit));
                        assignmentWarning = $"CẢNH BÁO AN NINH: Nhân sự chuyển phòng đang giữ phân công quản lý (Manager: [{mgrUnits}], Leader: [{ldrUnits}]). Quyền quản lý KHÔNG tự động chuyển sang {record.TargetDepartment}. Quản trị viên/HR cần rà soát và phân công lại quyền quản lý độc lập.";
                        _logger.LogWarning($"[TransferEffective - SecurityAlert] Transfer {record.Id}: Employee {emp.EmployeeCode} has active assignments. {assignmentWarning}");
                    }
                }

                // Domain History
                string historyDetails = $"Quyết định thuyên chuyển đã có hiệu lực. Phòng ban mới: {emp.Department}, Chức vụ: {emp.Position}, Chức danh: {emp.JobTitle}.";
                if (hasActiveAssignments)
                {
                    historyDetails += $" [{assignmentWarning}]";
                }

                var history = new TransferHistory
                {
                    TransferRecordId = record.Id,
                    Timestamp = DateTime.Now,
                    Actor = actor,
                    Action = "EFFECTIVE",
                    Details = historyDetails
                };
                _context.TransferHistories.Add(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"[TransferEffective] Successfully applied transfer {record.Id} for employee {emp.EmployeeCode} ({emp.Name}) to department {emp.Department}.");

                // Audit Log
                try
                {
                    await _auditLogService.LogAsync(
                        action: "EFFECTIVE",
                        module: "EmployeeLifecycle",
                        resource: "TransferRecord",
                        resourceId: record.Id.ToString(),
                        eventType: "Lifecycle",
                        status: "Success",
                        failureReason: null,
                        oldValues: $"Dept={oldDept}, Pos={oldPos}, Title={oldTitle}",
                        newValues: $"Dept={emp.Department}, Pos={emp.Position}, Title={emp.JobTitle}",
                        changes: hasActiveAssignments ? $"Thuyên chuyển có hiệu lực. {assignmentWarning}" : "Thuyên chuyển nhân sự chính thức có hiệu lực.");

                    if (hasActiveAssignments)
                    {
                        await _auditLogService.LogSecurityEventAsync(
                            action: "TRANSFER_ASSIGNMENT_SECURITY_ALERT",
                            resource: "TransferRecord",
                            resourceId: record.Id.ToString(),
                            status: "Warning",
                            failureReason: assignmentWarning);
                    }
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, $"[TransferEffective] Failed to write audit log for transfer {record.Id}.");
                }

                // Section 6 & 7: Check Pending Leave / Attendance Adjustment Queue & Notifications
                try
                {
                    var pendingLeaves = await _context.Leaves
                        .Where(l => l.EmployeeId == emp.Id && (l.Status == "Chờ duyệt" || l.Status == "Pending" || l.Status == "PENDING_APPROVAL"))
                        .ToListAsync();

                    var pendingAdjustments = await _context.AttendanceAdjustmentRequests
                        .Where(a => a.EmployeeCode == emp.EmployeeCode && (a.Status == "PENDING" || a.Status == "Chờ duyệt" || a.Status == "PENDING_APPROVAL"))
                        .ToListAsync();

                    int totalPending = pendingLeaves.Count + pendingAdjustments.Count;
                    if (totalPending > 0)
                    {
                        // Identify new Managers who manage TargetDepartment
                        var targetManagers = await _context.ManagerAssignments
                            .Include(m => m.User)
                            .Where(m => m.OrganizationalUnit == record.TargetDepartment && (m.EffectiveTo == null || m.EffectiveTo > DateTime.Now))
                            .Select(m => m.User)
                            .Where(u => u != null && u.CanLogin)
                            .Distinct()
                            .ToListAsync();

                        foreach (var mgr in targetManagers)
                        {
                            if (mgr == null) continue;
                            var dedupeKey = $"TRANSFER_PENDING_QUEUE_{record.Id}_{mgr.Id}";
                            await _notificationService.SendAsync(
                                recipientSelector: mgr.Username,
                                message: $"Nhân viên {emp.Name} vừa được thuyên chuyển sang {record.TargetDepartment} và có {totalPending} đơn từ (nghỉ phép/điều chỉnh công) đang chờ bạn duyệt.",
                                notificationType: "TRANSFER_PENDING_QUEUE",
                                targetUrl: "/ManagerWorkspace",
                                referenceId: record.Id,
                                senderName: actor,
                                deduplicationKey: dedupeKey);
                        }
                    }
                }
                catch (Exception queueEx)
                {
                    _logger.LogError(queueEx, $"[TransferEffective] Error notifying new manager about pending queue for employee {emp.Id}.");
                }

                // Notification to Employee
                try
                {
                    if (user != null && user.CanLogin)
                    {
                        var dedupeKey = $"TRANSFER_EFFECTIVE_EMP_{record.Id}_{user.Id}";
                        await _notificationService.SendAsync(
                            recipientSelector: user.Username,
                            message: $"Quyết định thuyên chuyển số {record.DecisionNumber ?? record.Id.ToString()} của bạn sang đơn vị {record.TargetDepartment} đã chính thức có hiệu lực.",
                            notificationType: "TRANSFER_EFFECTIVE",
                            targetUrl: "/Transfer",
                            referenceId: record.Id,
                            senderName: actor,
                            deduplicationKey: dedupeKey);
                    }
                }
                catch (Exception notifEx)
                {
                    _logger.LogError(notifEx, $"[TransferEffective] Failed to send notification to employee {emp.Id}.");
                }

                // Notification to HR / Admin if hasActiveAssignments
                if (hasActiveAssignments)
                {
                    try
                    {
                        var dedupeAlertKey = $"TRANSFER_ASSIGNMENT_ALERT_{record.Id}";
                        await _notificationService.SendAsync(
                            recipientSelector: "HR",
                            message: $"Cảnh báo nhân sự: {emp.Name} ({emp.EmployeeCode}) đã chuyển sang {record.TargetDepartment} nhưng vẫn đang giữ phân công Manager/Leader tại phòng cũ. Vui lòng rà soát Manager/Leader Assignment.",
                            notificationType: "SECURITY_ALERT",
                            targetUrl: "/Employee",
                            referenceId: record.Id,
                            senderName: actor,
                            deduplicationKey: dedupeAlertKey);
                    }
                    catch (Exception alertEx)
                    {
                        _logger.LogError(alertEx, $"[TransferEffective] Failed to send assignment security alert for transfer {record.Id}.");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"[TransferEffective] Error applying transfer {transferRecordId}.");
                return false;
            }
        }

        public async Task<int> ProcessPendingTransfersAsync(CancellationToken cancellationToken = default)
        {
            var today = DateTime.Today;
            var dueRecordIds = await _context.TransferRecords
                .Where(t => t.ApprovalStatus == "APPROVED" && t.AppliedAt == null && t.EffectiveDate.Date <= today)
                .OrderBy(t => t.EffectiveDate)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            int appliedCount = 0;
            foreach (var id in dueRecordIds)
            {
                if (cancellationToken.IsCancellationRequested) break;

                bool success = await ApplyTransferAsync(id, "SYSTEM");
                if (success)
                {
                    appliedCount++;
                }
            }

            return appliedCount;
        }
    }
}
