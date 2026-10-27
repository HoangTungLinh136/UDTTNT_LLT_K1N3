using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Models;
using giao_dien_demo.Data;

namespace giao_dien_demo.Services
{
    public class BenefitEnrollmentWorkflowPolicy : IWorkflowPolicy<BenefitEnrollment>
    {
        public string DomainName => "BenefitEnrollment";

        public Task<bool> IsEligibleApproverAsync(BenefitEnrollment entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            // Only HR can approve or reject Benefit Enrollments
            return Task.FromResult(approverRole == "HR");
        }

        public async Task OnApprovedAsync(BenefitEnrollment entity, ApplicationDbContext context)
        {
            entity.ApprovalStatus = "APPROVED";
            entity.ReviewedBy = entity.ReviewedBy ?? "SYSTEM";
            entity.ReviewedAt = DateTime.Now;

            // Auto-generate BenefitEntitlement when enrollment is approved
            var entitlement = new BenefitEntitlement
            {
                EmployeeId = entity.EmployeeId,
                EmployeeNameSnapshot = entity.EmployeeNameSnapshot,
                BenefitProgramId = entity.BenefitProgramId,
                EffectiveFrom = DateTime.Now,
                Status = "Active",
                Notes = $"Auto-generated from Approved Enrollment #{entity.Id}"
            };

            context.BenefitEntitlements.Add(entitlement);
            await Task.CompletedTask;
        }

        public async Task OnRejectedAsync(BenefitEnrollment entity, ApplicationDbContext context)
        {
            entity.ApprovalStatus = "REJECTED";
            entity.ReviewedBy = entity.ReviewedBy ?? "SYSTEM";
            entity.ReviewedAt = DateTime.Now;

            await Task.CompletedTask;
        }
    }
}
