using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Services
{
    public class TransferWorkflowPolicy : IWorkflowPolicy<TransferRecord>
    {
        public string DomainName => "Transfer";

        public async Task<bool> IsEligibleApproverAsync(TransferRecord entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            if (entity.ApprovalStatus == "PENDING_HR_REVIEW")
            {
                return approverRole == AppRoles.HR && !await IsSelfApprovalAsync(entity, approverUsername, context);
            }
            if (entity.ApprovalStatus == "PENDING_LEADER_APPROVAL")
            {
                if (approverRole != AppRoles.Leader || await IsSelfApprovalAsync(entity, approverUsername, context)) return false;
                var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == approverUsername && u.Role == AppRoles.Leader && u.CanLogin);
                if (user == null) return false;
                var now = DateTime.Now;
                return await context.LeaderAssignments.AsNoTracking().AnyAsync(a => a.UserId == user.Id
                    && a.EffectiveFrom <= now && (a.EffectiveTo == null || a.EffectiveTo > now)
                    && (a.OrganizationalUnit == entity.CurrentDepartmentSnapshot || a.OrganizationalUnit == entity.TargetDepartment));
            }
            return false;
        }

        private static async Task<bool> IsSelfApprovalAsync(TransferRecord entity, string username, ApplicationDbContext context)
        {
            if (entity.CreatedBy?.Equals(username, StringComparison.OrdinalIgnoreCase) == true || entity.SubmittedBy?.Equals(username, StringComparison.OrdinalIgnoreCase) == true) return true;
            var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
            return user?.EmployeeId == entity.EmployeeId;
        }

        public async Task OnApprovedAsync(TransferRecord entity, ApplicationDbContext context)
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

        public Task OnRejectedAsync(TransferRecord entity, ApplicationDbContext context)
        {
            entity.Status = "REJECTED";
            return Task.CompletedTask;
        }
    }
}
