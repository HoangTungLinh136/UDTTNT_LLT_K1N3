using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Models;
using giao_dien_demo.Data;

namespace giao_dien_demo.Services
{
    public class OffboardingWorkflowPolicy : IWorkflowPolicy<OffboardingRecord>
    {
        public string DomainName => "Offboarding";

        public Task<bool> IsEligibleApproverAsync(OffboardingRecord entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            return Task.FromResult(approverRole == "HR");
        }

        public Task OnApprovedAsync(OffboardingRecord entity, ApplicationDbContext context)
        {
            // Approval only authorizes the offboarding workflow. Employment status and
            // login access are changed exclusively by the guarded Complete endpoint/
            // effective background process after checklist requirements are satisfied.
            return Task.CompletedTask;
        }

        public Task OnRejectedAsync(OffboardingRecord entity, ApplicationDbContext context)
        {
            return Task.CompletedTask;
        }
    }
}
