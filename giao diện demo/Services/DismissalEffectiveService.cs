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
    public interface IDismissalEffectiveService
    {
        Task<bool> ApplyDismissalAsync(int dismissalRecordId, string actor = "SYSTEM");
        Task<int> ProcessPendingDismissalsAsync(CancellationToken cancellationToken = default);
    }

    public class DismissalEffectiveService : IDismissalEffectiveService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DismissalEffectiveService> _logger;
        private readonly IAuditLogService _auditLogService;
        private readonly IInAppNotificationService _notificationService;

        public DismissalEffectiveService(
            ApplicationDbContext context,
            ILogger<DismissalEffectiveService> logger,
            IAuditLogService auditLogService,
            IInAppNotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _auditLogService = auditLogService;
            _notificationService = notificationService;
        }

        public async Task<bool> ApplyDismissalAsync(int dismissalRecordId, string actor = "SYSTEM")
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var record = await _context.DismissalRecords
                    .Include(d => d.Employee)
                    .FirstOrDefaultAsync(d => d.Id == dismissalRecordId);

                if (record == null)
                {
                    _logger.LogWarning($"[DismissalEffective] Record {dismissalRecordId} not found.");
                    return false;
                }

                // Idempotency: skip if already applied or not in APPROVED status
                if (record.ApprovalStatus == "EFFECTIVE" || record.AppliedAt != null)
                {
                    _logger.LogInformation($"[DismissalEffective] Record {dismissalRecordId} has already been applied at {record.AppliedAt}. Skipping.");
                    return false;
                }

                if (record.ApprovalStatus != "APPROVED")
                {
                    _logger.LogWarning($"[DismissalEffective] Record {dismissalRecordId} cannot be applied because status is '{record.ApprovalStatus}' (expected 'APPROVED').");
                    return false;
                }

                if (record.EffectiveDate.Date > DateTime.Today)
                {
                    _logger.LogInformation($"[DismissalEffective] Record {dismissalRecordId} has future EffectiveDate ({record.EffectiveDate:yyyy-MM-dd}). Not yet due.");
                    return false;
                }

                if (record.Employee == null)
                {
                    _logger.LogError($"[DismissalEffective] Record {dismissalRecordId} references non-existing EmployeeId {record.EmployeeId}. Cannot apply.");
                    return false;
                }

                // Validation of PostReliefOption
                if (string.IsNullOrWhiteSpace(record.PostReliefOption))
                {
                    if (!string.IsNullOrWhiteSpace(record.TargetDepartment) || !string.IsNullOrWhiteSpace(record.TargetPosition))
                    {
                        record.PostReliefOption = "TRANSFER_TO_OTHER_POSITION";
                    }
                    else
                    {
                        _logger.LogWarning($"[DismissalEffective] Record {dismissalRecordId} has null/empty PostReliefOption. Skipping to avoid corrupting Employee master.");
                        return false;
                    }
                }

                if (record.PostReliefOption == "RETURN_TO_PREVIOUS_POSITION" || record.PostReliefOption == "TRANSFER_TO_OTHER_POSITION")
                {
                    if (string.IsNullOrWhiteSpace(record.TargetDepartment) && string.IsNullOrWhiteSpace(record.TargetPosition))
                    {
                        _logger.LogWarning($"[DismissalEffective] Record {dismissalRecordId} specifies option '{record.PostReliefOption}' but lacks target department and position. Skipping.");
                        return false;
                    }
                }
                else if (record.PostReliefOption != "PENDING_ASSIGNMENT")
                {
                    _logger.LogWarning($"[DismissalEffective] Record {dismissalRecordId} has unrecognized PostReliefOption '{record.PostReliefOption}'. Skipping.");
                    return false;
                }

                var emp = record.Employee;
                string oldDept = emp.Department ?? string.Empty;
                string oldPos = emp.Position ?? string.Empty;
                string oldTitle = emp.JobTitle ?? string.Empty;
                string oldStatus = emp.Status ?? "Active";

                // CRITICAL BUSINESS SEMANTICS:
                // Dismissal = Miễn nhiệm chức vụ, NOT termination / offboarding.
                // Do NOT set emp.Status = "Dismissed" or "Terminated" or "Active".
                // Do NOT set user.CanLogin = false.
                // Employee status and CanLogin remain completely unchanged.

                // Apply PostReliefOption safely
                if (record.PostReliefOption == "RETURN_TO_PREVIOUS_POSITION" || record.PostReliefOption == "TRANSFER_TO_OTHER_POSITION")
                {
                    if (!string.IsNullOrWhiteSpace(record.TargetDepartment))
                    {
                        emp.Department = record.TargetDepartment;
                    }
                    if (!string.IsNullOrWhiteSpace(record.TargetPosition))
                    {
                        emp.Position = record.TargetPosition;
                    }
                }
                else if (record.PostReliefOption == "PENDING_ASSIGNMENT")
                {
                    // Chờ bố trí: keep employee active. If target fields specified, update them.
                    if (!string.IsNullOrWhiteSpace(record.TargetDepartment))
                    {
                        emp.Department = record.TargetDepartment;
                    }
                    if (!string.IsNullOrWhiteSpace(record.TargetPosition))
                    {
                        emp.Position = record.TargetPosition;
                    }
                }

                // Mark Record as EFFECTIVE
                record.ApprovalStatus = "EFFECTIVE";
                record.AppliedAt = DateTime.Now;
                record.AppliedBy = actor;

                // Check active Manager / Leader assignments that might need review/revocation
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
                        assignmentWarning = $"CẢNH BÁO AN NINH: Nhân sự được miễn nhiệm hiện đang giữ phân công quản lý (Manager: [{mgrUnits}], Leader: [{ldrUnits}]). Quản trị viên/HR cần rà soát và thu hồi phân công quản lý tương ứng.";
                        _logger.LogWarning($"[DismissalEffective - SecurityAlert] Dismissal {record.Id}: Employee {emp.EmployeeCode} has active assignments. {assignmentWarning}");
                    }
                }

                // Domain History
                string historyDetails = $"Quyết định miễn nhiệm đã có hiệu lực. Phương án: {record.PostReliefOptionLabel} (Phòng ban: {emp.Department}, Chức vụ: {emp.Position}).";
                if (hasActiveAssignments)
                {
                    historyDetails += $" [{assignmentWarning}]";
                }

                var history = new DismissalHistory
                {
                    DismissalRecordId = record.Id,
                    Timestamp = DateTime.Now,
                    Actor = actor,
                    Action = "EFFECTIVE",
                    Details = historyDetails
                };
                _context.DismissalHistories.Add(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"[DismissalEffective] Successfully applied dismissal {record.Id} for employee {emp.EmployeeCode} ({emp.Name}). Employee remains active (Status: {emp.Status}).");

                // Audit Log
                try
                {
                    await _auditLogService.LogAsync(
                        action: "EFFECTIVE",
                        module: "EmployeeLifecycle",
                        resource: "DismissalRecord",
                        resourceId: record.Id.ToString(),
                        eventType: "Lifecycle",
                        status: "Success",
                        failureReason: null,
                        oldValues: $"Dept={oldDept}, Pos={oldPos}, Title={oldTitle}, Status={oldStatus}",
                        newValues: $"Dept={emp.Department}, Pos={emp.Position}, Title={emp.JobTitle}, Status={emp.Status}",
                        changes: hasActiveAssignments ? $"Miễn nhiệm có hiệu lực. Phương án: {record.PostReliefOptionLabel}. {assignmentWarning}" : $"Miễn nhiệm chức vụ có hiệu lực. Phương án: {record.PostReliefOptionLabel}.");

                    if (hasActiveAssignments)
                    {
                        await _auditLogService.LogSecurityEventAsync(
                            action: "DISMISSAL_ASSIGNMENT_SECURITY_ALERT",
                            resource: "DismissalRecord",
                            resourceId: record.Id.ToString(),
                            status: "Warning",
                            failureReason: assignmentWarning);
                    }
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, $"[DismissalEffective] Failed to write audit log for dismissal {record.Id}.");
                }

                // Notification to Employee
                try
                {
                    if (user != null && user.CanLogin)
                    {
                        var dedupeKey = $"DISMISSAL_EFFECTIVE_EMP_{record.Id}_{user.Id}";
                        await _notificationService.SendAsync(
                            recipientSelector: user.Username,
                            message: $"Quyết định miễn nhiệm số {record.DecisionNumber ?? record.Id.ToString()} đã chính thức có hiệu lực. Phương án: {record.PostReliefOptionLabel}.",
                            notificationType: "DISMISSAL_EFFECTIVE",
                            targetUrl: "/Dismissal",
                            referenceId: record.Id,
                            senderName: actor,
                            deduplicationKey: dedupeKey);
                    }
                }
                catch (Exception notifEx)
                {
                    _logger.LogError(notifEx, $"[DismissalEffective] Failed to send notification to employee {emp.Id}.");
                }

                // Notification to HR / Admin if hasActiveAssignments
                if (hasActiveAssignments)
                {
                    try
                    {
                        var dedupeAlertKey = $"DISMISSAL_ASSIGNMENT_ALERT_{record.Id}";
                        await _notificationService.SendAsync(
                            recipientSelector: "HR",
                            message: $"Cảnh báo nhân sự: {emp.Name} ({emp.EmployeeCode}) đã có hiệu lực miễn nhiệm nhưng vẫn đang giữ phân công Manager/Leader. Vui lòng kiểm tra và thu hồi Manager/Leader Assignment.",
                            notificationType: "SECURITY_ALERT",
                            targetUrl: "/Employee",
                            referenceId: record.Id,
                            senderName: actor,
                            deduplicationKey: dedupeAlertKey);
                    }
                    catch (Exception alertEx)
                    {
                        _logger.LogError(alertEx, $"[DismissalEffective] Failed to send assignment alert for dismissal {record.Id}.");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"[DismissalEffective] Error applying dismissal {dismissalRecordId}.");
                return false;
            }
        }

        public async Task<int> ProcessPendingDismissalsAsync(CancellationToken cancellationToken = default)
        {
            var today = DateTime.Today;
            var dueRecordIds = await _context.DismissalRecords
                .Where(d => d.ApprovalStatus == "APPROVED" && d.AppliedAt == null && d.EffectiveDate.Date <= today)
                .OrderBy(d => d.EffectiveDate)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            int appliedCount = 0;
            foreach (var id in dueRecordIds)
            {
                if (cancellationToken.IsCancellationRequested) break;

                bool success = await ApplyDismissalAsync(id, "SYSTEM");
                if (success)
                {
                    appliedCount++;
                }
            }

            return appliedCount;
        }
    }
}
