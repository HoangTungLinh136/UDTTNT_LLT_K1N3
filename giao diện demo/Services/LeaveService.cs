using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Hubs;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Services
{
    public class LeaveCreationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Leave? Leave { get; set; }
    }

    public class LeaveCancelResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface ILeaveService
    {
        Task<LeaveCreationResult> CreateLeaveAsync(
            Employee currentEmployee,
            DateTime fromDate,
            DateTime toDate,
            string? reason,
            string? leaveType,
            string? signature,
            string actorUsername);

        Task<LeaveCancelResult> CancelLeaveAsync(
            int leaveId,
            int currentEmployeeId,
            string actorUsername);
    }

    public class LeaveService : ILeaveService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLogService;
        private readonly IInAppNotificationService _notifications;
        private readonly IHubContext<DashboardHub> _hubContext;

        public LeaveService(
            ApplicationDbContext context,
            IAuditLogService auditLogService,
            IInAppNotificationService notifications,
            IHubContext<DashboardHub> hubContext)
        {
            _context = context;
            _auditLogService = auditLogService;
            _notifications = notifications;
            _hubContext = hubContext;
        }

        public async Task<LeaveCreationResult> CreateLeaveAsync(
            Employee currentEmployee,
            DateTime fromDate,
            DateTime toDate,
            string? reason,
            string? leaveType,
            string? signature,
            string actorUsername)
        {
            if (currentEmployee == null || currentEmployee.Id <= 0)
            {
                return new LeaveCreationResult
                {
                    Success = false,
                    ErrorMessage = "Tài khoản chưa được liên kết với hồ sơ nhân viên."
                };
            }

            if (fromDate.Date > toDate.Date)
            {
                return new LeaveCreationResult
                {
                    Success = false,
                    ErrorMessage = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu."
                };
            }

            var from = fromDate.Date;
            var to = toDate.Date;

            // GAP-LEAVE-01 & Overlap Validation:
            // Terminal inactive states (Đã từ chối, Từ chối, REJECTED, Đã hủy, CANCELLED) MUST NOT block new requests.
            // Active blocking states are PENDING / Chờ duyệt and APPROVED / Đã duyệt.
            bool isOverlap = await _context.Leaves.AnyAsync(l =>
                l.EmployeeId == currentEmployee.Id &&
                l.Status != "Từ chối" &&
                l.Status != "Đã từ chối" &&
                l.Status != "REJECTED" &&
                l.Status != "Đã hủy" &&
                l.Status != "CANCELLED" &&
                l.FromDate.Date <= to &&
                l.ToDate.Date >= from);

            if (isOverlap)
            {
                return new LeaveCreationResult
                {
                    Success = false,
                    ErrorMessage = "Khoảng thời gian này đã trùng với một đơn nghỉ phép khác của bạn."
                };
            }

            var leave = new Leave
            {
                EmployeeId = currentEmployee.Id,
                EmployeeNameSnapshot = currentEmployee.Name,
                FromDate = fromDate,
                ToDate = toDate,
                Reason = reason,
                LeaveType = string.IsNullOrWhiteSpace(leaveType) ? "Nghỉ phép năm" : leaveType,
                EmployeeSignature = signature,
                Status = "Chờ duyệt",
                CreatedDate = DateTime.Now,
                ReviewedBy = null,
                ReviewedAt = null,
                RejectionReason = null
            };

            _context.Leaves.Add(leave);
            await _context.SaveChangesAsync();

            // Audit log: Module = Leave, Resource = LeaveCore, ResourceId = leave.Id (raw signature excluded)
            await _auditLogService.LogAsync("CreateLeave", "Leave", "LeaveCore", leave.Id.ToString(), "Submit", "Success");

            // Notification: Scoped to Manager(s) responsible for the employee's department
            if (!string.IsNullOrEmpty(currentEmployee.Department))
            {
                var now = DateTime.Now;
                var managerUserIds = await _context.ManagerAssignments
                    .Where(a => a.OrganizationalUnit == currentEmployee.Department && a.EffectiveFrom <= now && (a.EffectiveTo == null || a.EffectiveTo > now))
                    .Select(a => a.UserId)
                    .Distinct()
                    .ToListAsync();

                var managers = await _context.Users.Where(u => managerUserIds.Contains(u.Id) && u.CanLogin).ToListAsync();
                foreach (var mgr in managers)
                {
                    await _notifications.SendAsync(
                        mgr.Username,
                        $"Nhân viên {currentEmployee.Name} đã nộp đơn nghỉ phép chờ duyệt.",
                        "LEAVE_PENDING",
                        "/ManagerWorkspace/LeaveApprovals",
                        leave.Id,
                        deduplicationKey: $"leave-submitted:{leave.Id}:{mgr.Id}");
                }
            }

            // SignalR requester update
            if (!string.IsNullOrEmpty(actorUsername))
            {
                int targetCount = await _context.Leaves.CountAsync(x => x.EmployeeId == currentEmployee.Id);
                await _hubContext.Clients.User(actorUsername).SendAsync("UpdateMyLeaveCount", targetCount, leave.EmployeeNameSnapshot);
            }

            return new LeaveCreationResult
            {
                Success = true,
                Leave = leave
            };
        }

        public async Task<LeaveCancelResult> CancelLeaveAsync(
            int leaveId,
            int currentEmployeeId,
            string actorUsername)
        {
            var item = await _context.Leaves.FindAsync(leaveId);
            if (item == null)
            {
                return new LeaveCancelResult
                {
                    Success = false,
                    ErrorMessage = "Không tìm thấy đơn nghỉ phép."
                };
            }

            if (!item.EmployeeId.HasValue || item.EmployeeId.Value != currentEmployeeId)
            {
                return new LeaveCancelResult
                {
                    Success = false,
                    ErrorMessage = "Bạn không có quyền hủy đơn nghỉ phép của nhân viên khác."
                };
            }

            if (item.Status != "Chờ duyệt" && item.Status != "PENDING")
            {
                return new LeaveCancelResult
                {
                    Success = false,
                    ErrorMessage = "Chỉ có thể hủy đơn nghỉ phép đang ở trạng thái chờ duyệt."
                };
            }

            item.Status = "Đã hủy";
            _context.Leaves.Update(item);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync("CancelLeave", "Leave", "LeaveCore", leaveId.ToString(), "Cancel", "Success");

            if (!string.IsNullOrEmpty(actorUsername))
            {
                int targetCount = await _context.Leaves.CountAsync(x => x.EmployeeId == currentEmployeeId);
                await _hubContext.Clients.User(actorUsername).SendAsync("UpdateMyLeaveCount", targetCount, item.EmployeeNameSnapshot);
            }

            return new LeaveCancelResult
            {
                Success = true
            };
        }
    }
}
