using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Services
{
    public sealed class ManagerAssignmentService : IManagerAssignmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLogService;
        private readonly IInAppNotificationService _notifications;

        public ManagerAssignmentService(
            ApplicationDbContext context,
            IAuditLogService auditLogService,
            IInAppNotificationService notifications)
        {
            _context = context;
            _auditLogService = auditLogService;
            _notifications = notifications;
        }

        public async Task<ManagerAssignmentDetailsViewModel> GetDetailsAsync(int employeeId)
        {
            var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return new ManagerAssignmentDetailsViewModel { EmployeeId = employeeId };

            var managerUser = await _context.Users.AsNoTracking()
                .Where(u => u.EmployeeId == employeeId && u.Role == "Manager")
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync();

            var units = await _context.Employees.AsNoTracking()
                .Where(e => !string.IsNullOrWhiteSpace(e.Department))
                .Select(e => e.Department!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var result = new ManagerAssignmentDetailsViewModel
            {
                EmployeeId = employeeId,
                AvailableUnits = units,
                IsManager = managerUser != null
            };

            if (managerUser == null) return result;

            var now = DateTime.Now;
            result.Assignments = await _context.ManagerAssignments.AsNoTracking()
                .Where(a => a.UserId == managerUser.Id)
                .OrderByDescending(a => a.EffectiveTo == null)
                .ThenByDescending(a => a.EffectiveFrom)
                .ThenByDescending(a => a.Id)
                .Select(a => new ManagerAssignmentListItem
                {
                    Id = a.Id,
                    OrganizationalUnit = a.OrganizationalUnit,
                    EffectiveFrom = a.EffectiveFrom,
                    EffectiveTo = a.EffectiveTo,
                    Status = a.EffectiveFrom > now ? "Sắp hiệu lực" :
                        (a.EffectiveTo.HasValue && a.EffectiveTo.Value <= now ? "Đã kết thúc" : "Đang hiệu lực"),
                    AssignedBy = a.AssignedBy,
                    CanEdit = a.EffectiveFrom > now,
                    CanEnd = !a.EffectiveTo.HasValue || a.EffectiveTo.Value > now
                })
                .ToListAsync();

            return result;
        }

        public async Task<(bool Success, string? Error, ManagerAssignment? Assignment)> CreateAsync(
            int employeeId, string organizationalUnit, DateTime effectiveFrom, DateTime? effectiveTo, string actor)
        {
            var validation = await ResolveAndValidateAsync(employeeId, organizationalUnit, effectiveFrom, effectiveTo, null);
            if (!validation.Success) return (false, validation.Error, null);

            var manager = validation.User!;
            var normalizedUnit = validation.Unit!;
            var now = DateTime.Now;

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                if (await HasOverlapAsync(manager.Id, normalizedUnit, effectiveFrom, effectiveTo, null))
                    return (false, "Khoảng thời gian phân công bị trùng với một phân công đã tồn tại.", null);

                var assignment = new ManagerAssignment
                {
                    UserId = manager.Id,
                    OrganizationalUnit = normalizedUnit,
                    EffectiveFrom = effectiveFrom,
                    EffectiveTo = effectiveTo,
                    AssignedBy = actor,
                    AssignedAt = now
                };
                _context.ManagerAssignments.Add(assignment);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _auditLogService.LogAsync(
                    "MANAGER_SCOPE_ASSIGNED", "ManagerScope", "ManagerAssignment", assignment.Id.ToString(),
                    "Lifecycle", "Success", "", "",
                    $"UserId={manager.Id};Unit={normalizedUnit};EffectiveFrom={effectiveFrom:O};EffectiveTo={effectiveTo:O}");
                await NotifyAsync(manager.Username, "Phạm vi quản lý của bạn đã được phân công.", "MANAGER_SCOPE_ASSIGNED", assignment.Id, actor,
                    $"manager-scope-assigned:{assignment.Id}");
                return (true, null, assignment);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                return (false, "Không thể lưu phân công. Vui lòng kiểm tra lại dữ liệu và thử lại.", null);
            }
        }

        public async Task<(bool Success, string? Error, ManagerAssignment? Assignment)> UpdateAsync(
            int assignmentId, string organizationalUnit, DateTime effectiveFrom, DateTime? effectiveTo, string actor)
        {
            var assignment = await _context.ManagerAssignments.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == assignmentId);
            if (assignment == null) return (false, "Không tìm thấy phân công.", null);
            if (assignment.User == null || assignment.User.Role != "Manager" || assignment.User.EmployeeId == null)
                return (false, "Phân công không còn gắn với tài khoản Manager hợp lệ.", null);
            if (assignment.EffectiveTo.HasValue && assignment.EffectiveTo.Value <= DateTime.Now)
                return (false, "Phân công đã kết thúc và chỉ được xem lịch sử.", null);

            var validation = await ValidateUnitAndDatesAsync(organizationalUnit, effectiveFrom, effectiveTo);
            if (!validation.Success) return (false, validation.Error, null);

            var isActive = assignment.EffectiveFrom <= DateTime.Now && (!assignment.EffectiveTo.HasValue || assignment.EffectiveTo.Value > DateTime.Now);
            if (isActive && (effectiveFrom != assignment.EffectiveFrom || !string.Equals(assignment.OrganizationalUnit, validation.Unit, StringComparison.Ordinal)))
                return (false, "Phân công đang hiệu lực chỉ được cập nhật ngày kết thúc.", null);

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                if (await HasOverlapAsync(assignment.UserId, validation.Unit!, effectiveFrom, effectiveTo, assignment.Id))
                    return (false, "Khoảng thời gian phân công bị trùng với một phân công đã tồn tại.", null);

                var oldValues = $"Unit={assignment.OrganizationalUnit};EffectiveFrom={assignment.EffectiveFrom:O};EffectiveTo={assignment.EffectiveTo:O}";
                assignment.OrganizationalUnit = validation.Unit!;
                assignment.EffectiveFrom = effectiveFrom;
                assignment.EffectiveTo = effectiveTo;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _auditLogService.LogAsync(
                    "MANAGER_SCOPE_UPDATED", "ManagerScope", "ManagerAssignment", assignment.Id.ToString(),
                    "Lifecycle", "Success", "", oldValues,
                    $"Unit={assignment.OrganizationalUnit};EffectiveFrom={assignment.EffectiveFrom:O};EffectiveTo={assignment.EffectiveTo:O}");
                await NotifyAsync(assignment.User?.Username, "Phạm vi quản lý của bạn đã được cập nhật.", "MANAGER_SCOPE_UPDATED", assignment.Id, actor,
                    $"manager-scope-updated:{assignment.Id}:{assignment.EffectiveFrom:O}:{assignment.EffectiveTo:O}");
                return (true, null, assignment);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                return (false, "Không thể cập nhật phân công. Vui lòng thử lại.", null);
            }
        }

        public async Task<(bool Success, string? Error)> EndAsync(int assignmentId, DateTime? effectiveTo, string actor)
        {
            var assignment = await _context.ManagerAssignments.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == assignmentId);
            if (assignment == null) return (false, "Không tìm thấy phân công.");
            if (assignment.User == null || assignment.User.Role != "Manager" || assignment.User.EmployeeId == null)
                return (false, "Phân công không còn gắn với tài khoản Manager hợp lệ.");
            var now = DateTime.Now;
            if (assignment.EffectiveTo.HasValue && assignment.EffectiveTo.Value <= now)
                return (false, "Phân công đã kết thúc.");

            var endAt = effectiveTo ?? now;
            if (endAt <= assignment.EffectiveFrom)
                return (false, "Ngày kết thúc phải sau ngày bắt đầu.");

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var current = await _context.ManagerAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId);
            if (current == null)
                return (false, "Phân công đã được kết thúc trước đó.");
            await _context.Entry(current).ReloadAsync();
            if (current.EffectiveTo.HasValue && current.EffectiveTo.Value <= DateTime.Now)
                return (false, "Phân công đã được kết thúc trước đó.");
            current.EffectiveTo = endAt;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            await _auditLogService.LogAsync(
                "MANAGER_SCOPE_ENDED", "ManagerScope", "ManagerAssignment", assignment.Id.ToString(),
                "Lifecycle", "Success", "", "", $"EffectiveTo={endAt:O}");
            await NotifyAsync(assignment.User?.Username, "Phạm vi quản lý của bạn đã được kết thúc.", "MANAGER_SCOPE_ENDED", assignment.Id, actor,
                $"manager-scope-ended:{assignment.Id}:{endAt:O}");
            return (true, null);
        }

        private async Task<(bool Success, string? Error, User? User, string? Unit)> ResolveAndValidateAsync(
            int employeeId, string organizationalUnit, DateTime effectiveFrom, DateTime? effectiveTo, int? excludedId)
        {
            var employeeExists = await _context.Employees.AsNoTracking().AnyAsync(e => e.Id == employeeId);
            if (!employeeExists) return (false, "Nhân viên không tồn tại.", null, null);

            var user = await _context.Users
                .Where(u => u.EmployeeId == employeeId && u.Role == "Manager")
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync();
            if (user == null) return (false, "Nhân viên chưa có tài khoản Manager hợp lệ.", null, null);

            var validation = await ValidateUnitAndDatesAsync(organizationalUnit, effectiveFrom, effectiveTo);
            if (!validation.Success) return (false, validation.Error, user, null);
            return (true, null, user, validation.Unit);
        }

        private async Task<(bool Success, string? Error, string? Unit)> ValidateUnitAndDatesAsync(
            string organizationalUnit, DateTime effectiveFrom, DateTime? effectiveTo)
        {
            if (effectiveFrom == default) return (false, "Vui lòng nhập ngày bắt đầu hiệu lực.", null);
            if (effectiveTo.HasValue && effectiveTo.Value <= effectiveFrom)
                return (false, "Ngày kết thúc phải sau ngày bắt đầu.", null);

            var unit = organizationalUnit?.Trim();
            if (string.IsNullOrWhiteSpace(unit)) return (false, "Vui lòng chọn đơn vị quản lý.", null);
            var exists = await _context.Employees.AsNoTracking().AnyAsync(e => e.Department == unit);
            if (!exists) return (false, "Đơn vị quản lý không tồn tại trong danh mục hiện tại.", null);
            return (true, null, unit);
        }

        private Task<bool> HasOverlapAsync(int userId, string unit, DateTime start, DateTime? end, int? excludedId)
        {
            var query = _context.ManagerAssignments.Where(a => a.UserId == userId && a.OrganizationalUnit == unit);
            if (excludedId.HasValue) query = query.Where(a => a.Id != excludedId.Value);
            query = query.Where(a => (a.EffectiveTo == null || a.EffectiveTo > start)
                && (end == null || a.EffectiveFrom < end.Value));
            return query.AnyAsync();
        }

        private async Task NotifyAsync(string? username, string message, string type, int assignmentId, string actor, string key)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            await _notifications.SendAsync(username, message, type, "/ManagerWorkspace", assignmentId,
                senderName: actor, deduplicationKey: key);
        }
    }
}
