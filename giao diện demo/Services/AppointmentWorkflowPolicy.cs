using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Services
{
    public class AppointmentWorkflowPolicy : IWorkflowPolicy<AppointmentRecord>
    {
        public string DomainName => "Appointment";

        public async Task<bool> IsEligibleApproverAsync(AppointmentRecord entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            // Managers cannot approve appointments; they only submit drafts via Submit endpoint.
            if (entity.ApprovalStatus == "DRAFT")
            {
                return false;
            }

            var approverUser = await context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == approverUsername && u.CanLogin);
            if (approverUser == null) return false;

            // Prevent self-approval: approver cannot be the target employee being appointed
            if (approverUser.EmployeeId.HasValue && approverUser.EmployeeId.Value == entity.EmployeeId)
            {
                return false;
            }

            // Prevent originator approval: originator cannot approve/verify their own appointment proposal
            if (!string.IsNullOrEmpty(entity.CreatedBy) &&
                (entity.CreatedBy.Equals(approverUsername, StringComparison.OrdinalIgnoreCase) ||
                 (!string.IsNullOrEmpty(approverUser.FullName) && entity.CreatedBy.Equals(approverUser.FullName, StringComparison.OrdinalIgnoreCase))))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(entity.SubmittedBy) &&
                (entity.SubmittedBy.Equals(approverUsername, StringComparison.OrdinalIgnoreCase) ||
                 (!string.IsNullOrEmpty(approverUser.FullName) && entity.SubmittedBy.Equals(approverUser.FullName, StringComparison.OrdinalIgnoreCase))))
            {
                return false;
            }

            // HR verification stage
            if (entity.ApprovalStatus == "PENDING_HR_REVIEW")
            {
                return approverRole == AppRoles.HR;
            }

            // Leader final approval stage: requires active LeaderAssignment covering either previous or new department
            if (entity.ApprovalStatus == "PENDING_LEADER_APPROVAL")
            {
                if (approverRole != AppRoles.Leader) return false;

                var now = DateTime.Now;
                var allowedUnits = await context.LeaderAssignments.AsNoTracking()
                    .Where(la => la.UserId == approverUser.Id
                              && la.EffectiveFrom <= now
                              && (la.EffectiveTo == null || la.EffectiveTo > now))
                    .Select(la => la.OrganizationalUnit)
                    .Distinct()
                    .ToListAsync();

                if (!allowedUnits.Any()) return false;

                // Leader is eligible if overseeing the previous department or the target department
                bool canAccessPrevious = !string.IsNullOrEmpty(entity.PreviousDepartment) && allowedUnits.Contains(entity.PreviousDepartment);
                bool canAccessNew = !string.IsNullOrEmpty(entity.NewDepartment) && allowedUnits.Contains(entity.NewDepartment);

                return canAccessPrevious || canAccessNew;
            }

            return false;
        }

        public async Task OnApprovedAsync(AppointmentRecord entity, ApplicationDbContext context)
        {
            var reviewer = await context.Users.FirstOrDefaultAsync(u => u.Username == entity.ReviewedBy);
            if (reviewer != null && reviewer.Role == AppRoles.HR)
            {
                // HR verification complete -> transition to Leader pending approval
                entity.Status = "PENDING_LEADER_APPROVAL";
            }
            else if (reviewer != null && reviewer.Role == AppRoles.Leader)
            {
                // Leader final approval -> transition to APPROVED
                entity.Status = "APPROVED";
            }
        }

        public Task OnRejectedAsync(AppointmentRecord entity, ApplicationDbContext context)
        {
            entity.Status = "REJECTED";
            return Task.CompletedTask;
        }
    }
}
