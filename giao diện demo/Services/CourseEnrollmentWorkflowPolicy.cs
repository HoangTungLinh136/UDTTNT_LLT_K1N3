using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Models;
using giao_dien_demo.Data;

namespace giao_dien_demo.Services
{
    public class CourseEnrollmentWorkflowPolicy : IWorkflowPolicy<CourseEnrollment>
    {
        public string DomainName => "CourseEnrollment";

        public Task<bool> IsEligibleApproverAsync(CourseEnrollment entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            // Simple rule for now: HR and Admin can approve course enrollments.
            return Task.FromResult(approverRole == "HR");
        }

        public async Task OnApprovedAsync(CourseEnrollment entity, ApplicationDbContext context)
        {
            // Once approved, enrollment is set to ENROLLED
            entity.ProgressStatus = "Enrolled";
            entity.ReviewedBy = entity.ReviewedBy ?? "SYSTEM";
            entity.ReviewedAt = DateTime.Now;

            await Task.CompletedTask;
        }

        public async Task OnRejectedAsync(CourseEnrollment entity, ApplicationDbContext context)
        {
            entity.ProgressStatus = "REJECTED";
            entity.ReviewedBy = entity.ReviewedBy ?? "SYSTEM";
            entity.ReviewedAt = DateTime.Now;

            await Task.CompletedTask;
        }
    }
}

