using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Services
{
    public class DismissalWorkflowPolicy : IWorkflowPolicy<DismissalRecord>
    {
        public string DomainName => "Dismissal";

        public async Task<bool> IsEligibleApproverAsync(DismissalRecord entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            if (entity.ApprovalStatus == "PENDING_HR_REVIEW")
            {
                return approverRole == AppRoles.HR && !IsSelfApproval(entity, approverUsername, context);
            }
            if (entity.ApprovalStatus == "PENDING_LEADER_APPROVAL")
            {
                if (approverRole != AppRoles.Leader || IsSelfApproval(entity, approverUsername, context)) return false;

                var approver = await context.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Username == approverUsername && u.CanLogin && u.Role == AppRoles.Leader);
                if (approver == null) return false;

                var now = DateTime.Now;
                var units = await context.LeaderAssignments.AsNoTracking()
                    .Where(a => a.UserId == approver.Id
                        && a.EffectiveFrom <= now
                        && (a.EffectiveTo == null || a.EffectiveTo > now))
                    .Select(a => a.OrganizationalUnit)
                    .Distinct()
                    .ToListAsync();

                return (!string.IsNullOrWhiteSpace(entity.CurrentDepartmentSnapshot)
                        && units.Contains(entity.CurrentDepartmentSnapshot));
            }
            return false;
        }

        private static bool IsSelfApproval(DismissalRecord entity, string approverUsername, ApplicationDbContext context)
        {
            if (entity.Employee?.Id > 0)
            {
                var approver = context.Users.AsNoTracking()
                    .FirstOrDefault(u => u.Username == approverUsername);
                if (approver?.EmployeeId == entity.EmployeeId) return true;
            }

            return (!string.IsNullOrWhiteSpace(entity.CreatedBy)
                    && entity.CreatedBy.Equals(approverUsername, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(entity.SubmittedBy)
                    && entity.SubmittedBy.Equals(approverUsername, StringComparison.OrdinalIgnoreCase));
        }

        public async Task OnApprovedAsync(DismissalRecord entity, ApplicationDbContext context)
        {
            var reviewer = await context.Users.FirstOrDefaultAsync(u => u.Username == entity.ReviewedBy);
            if (reviewer != null && reviewer.Role == "HR")
            {
                // HR verification complete, move to Leader approval
                entity.Status = "PENDING_LEADER_APPROVAL";
            }
            else if (reviewer != null && reviewer.Role == "Leader")
            {
                // Leader final approval
                entity.Status = "APPROVED";
            }
        }

        public Task OnRejectedAsync(DismissalRecord entity, ApplicationDbContext context)
        {
            entity.Status = "REJECTED";
            return Task.CompletedTask;
        }
    }
}
