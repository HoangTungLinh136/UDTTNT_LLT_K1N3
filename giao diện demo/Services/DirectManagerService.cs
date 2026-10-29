using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Helpers;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public class DirectManagerService : IDirectManagerService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLogService;

        public DirectManagerService(ApplicationDbContext context, IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
        }

        public async Task<DirectManagerChangeResult> ChangeDirectManagerAsync(
            DirectManagerChangeRequest request,
            string actor,
            int? actorUserId = null)
        {
            if (request == null)
            {
                return new DirectManagerChangeResult { Success = false, Error = "Dữ liệu yêu cầu không hợp lệ." };
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId);
                if (employee == null)
                {
                    return new DirectManagerChangeResult { Success = false, Error = $"Không tìm thấy nhân viên ID {request.EmployeeId}." };
                }

                // OD-08: Concurrency check if RowVersion provided
                if (request.RowVersion != null && employee.RowVersion != null && !request.RowVersion.SequenceEqual(employee.RowVersion))
                {
                    await transaction.RollbackAsync();
                    return new DirectManagerChangeResult 
                    { 
                        Success = false, 
                        Error = "Dữ liệu nhân sự đã bị thay đổi bởi phiên làm việc khác (Concurrency Conflict - OD-08). Vui lòng làm mới trang." 
                    };
                }

                // OD-07, OD-11: Universal cycle check, non-self check, existence check, null check
                var validation = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                    _context,
                    request.EmployeeId,
                    request.NewManagerId,
                    request.AllowNullManagerException);

                if (!validation.IsValid)
                {
                    await transaction.RollbackAsync();
                    return new DirectManagerChangeResult { Success = false, Error = validation.ErrorMessage };
                }

                int? previousManagerId = employee.ManagerId;

                // If manager doesn't change, return success without logging new history
                if (previousManagerId == request.NewManagerId)
                {
                    await transaction.RollbackAsync();
                    return new DirectManagerChangeResult { Success = true, History = null };
                }

                // Mutate direct manager
                employee.ManagerId = request.NewManagerId;

                int resolvedUserId = actorUserId ?? 
                    (await _context.Users.Where(u => u.Username == actor).Select(u => (int?)u.Id).FirstOrDefaultAsync()) ?? 
                    (await _context.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync());

                var historyEntry = new EmployeeReportingHistory
                {
                    EmployeeId = request.EmployeeId,
                    PreviousManagerId = previousManagerId,
                    NewManagerId = request.NewManagerId,
                    EffectiveDate = DateTime.UtcNow,
                    ChangeReason = request.Reason ?? (request.AllowNullManagerException ? $"Ngoại lệ quản lý NULL: {request.NullManagerReason}" : "Thay đổi người quản lý trực tiếp"),
                    ChangedByUserId = resolvedUserId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.EmployeeReportingHistories.Add(historyEntry);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Audit Log
                await _auditLogService.LogAsync(
                    action: "CHANGE_DIRECT_MANAGER",
                    module: "Organization",
                    resource: "Employee",
                    resourceId: employee.Id.ToString(),
                    eventType: "Lifecycle",
                    status: "Success",
                    failureReason: null,
                    oldValues: $"ManagerId={previousManagerId}",
                    newValues: $"ManagerId={request.NewManagerId}",
                    changes: $"Thay đổi quản lý trực tiếp nhân sự {employee.EmployeeCode} ({employee.Name}) từ ID {previousManagerId} sang ID {request.NewManagerId}.");

                return new DirectManagerChangeResult { Success = true, History = historyEntry };
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return new DirectManagerChangeResult 
                { 
                    Success = false, 
                    Error = "Xung đột đồng thời (Concurrency Conflict - OD-08): Dữ liệu nhân sự đã bị cập nhật trong lúc xử lý." 
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new DirectManagerChangeResult 
                { 
                    Success = false, 
                    Error = $"Lỗi hệ thống khi cập nhật quản lý trực tiếp: {ex.Message}" 
                };
            }
        }
    }
}
