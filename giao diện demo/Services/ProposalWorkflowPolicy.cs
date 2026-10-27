using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Services;

public sealed class ProposalWorkflowPolicy : IWorkflowPolicy<Proposal>
{
    public string DomainName => "Proposal";

    public async Task<bool> IsEligibleApproverAsync(
        Proposal entity,
        string approverUsername,
        string approverRole,
        ApplicationDbContext context)
    {
        // PROPOSAL-AUTH-01: HR is NEVER an eligible approver. Only Leaders can approve proposals.
        if (approverRole != AppRoles.Leader) return false;

        // Only proposals in pending state can be acted upon by approvers
        if (entity.Status != "PENDING_APPROVAL" && entity.Status != "PENDING") return false;

        var approver = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == approverUsername && x.Role == approverRole && x.CanLogin);
        if (approver == null) return false;

        // Prevent self-approval: Proposer / Originator cannot approve their own proposal
        if (!string.IsNullOrEmpty(entity.CreatedBy) && 
            (entity.CreatedBy.Equals(approverUsername, StringComparison.OrdinalIgnoreCase) || 
             (!string.IsNullOrEmpty(approver.FullName) && entity.CreatedBy.Equals(approver.FullName, StringComparison.OrdinalIgnoreCase))))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(entity.SubmittedBy) && 
            (entity.SubmittedBy.Equals(approverUsername, StringComparison.OrdinalIgnoreCase) || 
             (!string.IsNullOrEmpty(approver.FullName) && entity.SubmittedBy.Equals(approver.FullName, StringComparison.OrdinalIgnoreCase))))
        {
            return false;
        }

        // PROPOSAL-SCOPE-01: Resolve department from RelatedEmployee or fallback to RelatedDepartment
        string? department = null;
        if (entity.RelatedEmployeeId.HasValue)
        {
            department = await context.Employees.AsNoTracking()
                .Where(x => x.Id == entity.RelatedEmployeeId.Value)
                .Select(x => x.Department)
                .FirstOrDefaultAsync();
        }

        if (string.IsNullOrWhiteSpace(department) && !string.IsNullOrWhiteSpace(entity.RelatedDepartment))
        {
            department = entity.RelatedDepartment.Trim();
        }

        // Fail-closed if department cannot be determined
        if (string.IsNullOrWhiteSpace(department)) return false;

        var now = DateTime.Now;
        return await context.LeaderAssignments.AnyAsync(x =>
            x.UserId == approver.Id &&
            x.OrganizationalUnit == department &&
            x.EffectiveFrom <= now &&
            (x.EffectiveTo == null || x.EffectiveTo > now));
    }

    public Task OnApprovedAsync(Proposal entity, ApplicationDbContext context)
    {
        entity.Status = "APPROVED";
        entity.UpdatedAt = DateTime.Now;
        return AddHistory(entity, context, "APPROVE", "Đã phê duyệt đề xuất");
    }

    public Task OnRejectedAsync(Proposal entity, ApplicationDbContext context)
    {
        entity.Status = "REJECTED";
        entity.UpdatedAt = DateTime.Now;
        return AddHistory(entity, context, "REJECT", $"Từ chối đề xuất. Lý do: {entity.RejectionReason}");
    }

    public Task OnRevisionRequestedAsync(Proposal entity, ApplicationDbContext context)
    {
        entity.RevisionRequestReason = entity.RejectionReason;
        entity.RejectionReason = null;
        entity.UpdatedAt = DateTime.Now;
        return AddHistory(entity, context, "REQUEST_REVISION", $"Yêu cầu bổ sung. Lý do: {entity.RevisionRequestReason}");
    }

    private static Task AddHistory(Proposal entity, ApplicationDbContext context, string action, string details)
    {
        context.ProposalHistories.Add(new ProposalHistory
        {
            ProposalId = entity.Id,
            Action = action,
            Actor = entity.ReviewedBy,
            Details = details,
            Timestamp = DateTime.Now
        });
        return Task.CompletedTask;
    }
}
