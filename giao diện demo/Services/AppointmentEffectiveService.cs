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
    public interface IAppointmentEffectiveService
    {
        Task<bool> ApplyAppointmentAsync(int appointmentRecordId, string actor = "SYSTEM");
        Task<int> ProcessPendingAppointmentsAsync(CancellationToken cancellationToken = default);
    }

    public class AppointmentEffectiveService : IAppointmentEffectiveService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AppointmentEffectiveService> _logger;
        private readonly IAuditLogService _auditLogService;
        private readonly IInAppNotificationService _notificationService;

        public AppointmentEffectiveService(
            ApplicationDbContext context,
            ILogger<AppointmentEffectiveService> logger,
            IAuditLogService auditLogService,
            IInAppNotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _auditLogService = auditLogService;
            _notificationService = notificationService;
        }

        public async Task<bool> ApplyAppointmentAsync(int appointmentRecordId, string actor = "SYSTEM")
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var record = await _context.AppointmentRecords
                    .Include(a => a.Employee)
                    .FirstOrDefaultAsync(a => a.Id == appointmentRecordId);

                if (record == null)
                {
                    _logger.LogWarning($"[AppointmentEffective] Record {appointmentRecordId} not found.");
                    return false;
                }

                // Idempotency: skip if already applied or not in APPROVED status
                if (record.ApprovalStatus == "EFFECTIVE" || record.AppliedAt != null)
                {
                    _logger.LogInformation($"[AppointmentEffective] Record {appointmentRecordId} has already been applied at {record.AppliedAt}. Skipping.");
                    return false;
                }

                if (record.ApprovalStatus != "APPROVED")
                {
                    _logger.LogWarning($"[AppointmentEffective] Record {appointmentRecordId} cannot be applied because status is '{record.ApprovalStatus}' (expected 'APPROVED').");
                    return false;
                }

                if (record.EffectiveDate.Date > DateTime.Today)
                {
                    _logger.LogInformation($"[AppointmentEffective] Record {appointmentRecordId} has future EffectiveDate ({record.EffectiveDate:yyyy-MM-dd}). Not yet due.");
                    return false;
                }

                if (record.Employee == null)
                {
                    _logger.LogError($"[AppointmentEffective] Record {appointmentRecordId} references non-existing EmployeeId {record.EmployeeId}. Cannot apply.");
                    return false;
                }

                // Validation of target data before mutation
                bool hasTargetData = !string.IsNullOrWhiteSpace(record.NewDepartment) ||
                                     !string.IsNullOrWhiteSpace(record.NewPosition) ||
                                     !string.IsNullOrWhiteSpace(record.NewJobTitle);

                if (!hasTargetData)
                {
                    _logger.LogWarning($"[AppointmentEffective] Record {appointmentRecordId} lacks any valid target fields (Department/Position/JobTitle). Skipping to avoid corrupting Employee master.");
                    return false;
                }

                var emp = record.Employee;
                string oldDept = emp.Department ?? string.Empty;
                string oldPos = emp.Position ?? string.Empty;
                string oldTitle = emp.JobTitle ?? string.Empty;

                // Mutate Employee Master
                if (!string.IsNullOrWhiteSpace(record.NewDepartment))
                {
                    emp.Department = record.NewDepartment;
                }
                if (!string.IsNullOrWhiteSpace(record.NewPosition))
                {
                    emp.Position = record.NewPosition;
                }
                if (!string.IsNullOrWhiteSpace(record.NewJobTitle))
                {
                    emp.JobTitle = record.NewJobTitle;
                }

                // Mark Record as EFFECTIVE
                record.ApprovalStatus = "EFFECTIVE";
                record.AppliedAt = DateTime.Now;
                record.AppliedBy = actor;

                // Domain History
                var history = new AppointmentHistory
                {
                    AppointmentRecordId = record.Id,
                    Timestamp = DateTime.Now,
                    Actor = actor,
                    Action = "EFFECTIVE",
                    Details = $"Quyết định bổ nhiệm đã có hiệu lực. Phòng ban: {emp.Department}, Chức vụ: {emp.Position}, Chức danh: {emp.JobTitle}."
                };
                _context.AppointmentHistories.Add(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"[AppointmentEffective] Successfully applied appointment {record.Id} for employee {emp.EmployeeCode} ({emp.Name}).");

                // Audit Log
                try
                {
                    await _auditLogService.LogAsync(
                        action: "EFFECTIVE",
                        module: "EmployeeLifecycle",
                        resource: "AppointmentRecord",
                        resourceId: record.Id.ToString(),
                        eventType: "Lifecycle",
                        status: "Success",
                        failureReason: null,
                        oldValues: $"Dept={oldDept}, Pos={oldPos}, Title={oldTitle}",
                        newValues: $"Dept={emp.Department}, Pos={emp.Position}, Title={emp.JobTitle}",
                        changes: "Quyết định bổ nhiệm chính thức có hiệu lực.");
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, $"[AppointmentEffective] Failed to write audit log for appointment {record.Id}.");
                }

                // In-App Notification
                try
                {
                    var empUser = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == emp.Id);
                    if (empUser != null && empUser.CanLogin)
                    {
                        var dedupeKey = $"APPOINTMENT_EFFECTIVE_{record.Id}_{empUser.Id}";
                        await _notificationService.SendAsync(
                            recipientSelector: empUser.Username,
                            message: $"Quyết định bổ nhiệm số {record.DecisionNumber ?? record.Id.ToString()} của bạn đã chính thức có hiệu lực. Chức vụ mới: {emp.Position}, Phòng ban: {emp.Department}.",
                            notificationType: "APPOINTMENT_EFFECTIVE",
                            targetUrl: "/Appointment",
                            referenceId: record.Id,
                            senderName: actor,
                            deduplicationKey: dedupeKey);
                    }
                }
                catch (Exception notifEx)
                {
                    _logger.LogError(notifEx, $"[AppointmentEffective] Failed to send notification for appointment {record.Id}.");
                }

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"[AppointmentEffective] Error applying appointment {appointmentRecordId}.");
                return false;
            }
        }

        public async Task<int> ProcessPendingAppointmentsAsync(CancellationToken cancellationToken = default)
        {
            var today = DateTime.Today;
            var dueRecordIds = await _context.AppointmentRecords
                .Where(a => a.ApprovalStatus == "APPROVED" && a.AppliedAt == null && a.EffectiveDate.Date <= today)
                .OrderBy(a => a.EffectiveDate)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            int appliedCount = 0;
            foreach (var id in dueRecordIds)
            {
                if (cancellationToken.IsCancellationRequested) break;

                bool success = await ApplyAppointmentAsync(id, "SYSTEM");
                if (success)
                {
                    appliedCount++;
                }
            }

            return appliedCount;
        }
    }
}
