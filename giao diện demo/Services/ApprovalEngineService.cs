using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System.Data;

namespace giao_dien_demo.Services
{
    public interface IApprovalEngine
    {
        Task<(bool Success, string Message)> ProcessDecisionAsync<T>(
            T entity,
            string decision, // "APPROVE", "REJECT", "REQUEST_REVISION"
            string approverUsername,
            string approverRole,
            string? reason,
            IWorkflowPolicy<T> policy) where T : class, IApprovableEntity;
    }

    public class ApprovalEngineService : IApprovalEngine
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLogService;

        public ApprovalEngineService(ApplicationDbContext context, IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
        }

        public async Task<(bool Success, string Message)> ProcessDecisionAsync<T>(
            T entity, 
            string decision, 
            string approverUsername, 
            string approverRole, 
            string? reason, 
            IWorkflowPolicy<T> policy) where T : class, IApprovableEntity
        {
            var normalizedDecision = decision?.Trim().ToUpperInvariant() ?? string.Empty;
            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await _context.Entry(entity).ReloadAsync();
                if (entity.Status != "PENDING" && entity.Status != "PENDING_APPROVAL" && entity.Status != "PENDING_LEADER" && entity.Status != "Chờ duyệt" && entity.Status != "Đã ký (Chờ duyệt cuối)")
                {
                    await transaction.RollbackAsync();
                    DetachPendingOutboxMessages();
                    return (false, "Yêu cầu này đã được xử lý trước đó!");
                }

                bool isEligible = await policy.IsEligibleApproverAsync(entity, approverUsername, approverRole, _context);
                if (!isEligible)
                {
                    await transaction.RollbackAsync();
                    DetachPendingOutboxMessages();
                    return (false, "Bạn không có quyền duyệt yêu cầu này.");
                }

                // Update Canonical Status
                if (normalizedDecision == "APPROVE")
                {
                    entity.Status = "APPROVED"; // Will map to domain specifics in controller or keep it standard
                    entity.ReviewedBy = approverUsername;
                    entity.ReviewedAt = DateTime.Now;
                    
                    await policy.OnApprovedAsync(entity, _context);
                }
                else if (normalizedDecision == "REJECT")
                {
                    if (string.IsNullOrEmpty(reason)) return (false, "Vui lòng nhập lý do từ chối");
                    
                    entity.Status = "REJECTED";
                    entity.ReviewedBy = approverUsername;
                    entity.ReviewedAt = DateTime.Now;
                    entity.RejectionReason = reason;
                    await policy.OnRejectedAsync(entity, _context);
                }
                else if (normalizedDecision == "REQUEST_REVISION")
                {
                    if (string.IsNullOrEmpty(reason)) return (false, "Vui lòng nhập nội dung yêu cầu bổ sung");

                    entity.Status = "NEEDS_REVISION";
                    entity.ReviewedBy = approverUsername;
                    entity.ReviewedAt = DateTime.Now;
                    entity.RejectionReason = reason;
                    await policy.OnRevisionRequestedAsync(entity, _context);
                }
                else
                {
                    return (false, "Quyết định không hợp lệ.");
                }

                _context.Update(entity);
                await _context.SaveChangesAsync();

                string action = decision == "APPROVE" ? "Approve" : (decision == "REJECT" ? "Reject" : "Revision");
                await _auditLogService.LogAsync($"Workflow_{action}", policy.DomainName, "ApprovalEngine", entity.GetId().ToString(), $"Decision:{decision}", "Success");

                await transaction.CommitAsync();

                return (true, "Xử lý thành công!");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                DetachPendingOutboxMessages();
                return (false, "Yêu cầu đã được người khác xử lý. Vui lòng tải lại dữ liệu.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                DetachPendingOutboxMessages();
                return (false, "Yêu cầu không thể được xử lý; trạng thái có thể đã thay đổi.");
            }
        }

        private void DetachPendingOutboxMessages()
        {
            var pending = _context.ChangeTracker.Entries<EventOutboxMessage>()
                .Where(e => e.State == EntityState.Added).ToList();
            foreach (var entry in pending)
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
