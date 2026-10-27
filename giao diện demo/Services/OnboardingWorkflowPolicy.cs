using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Models;
using giao_dien_demo.Data;

namespace giao_dien_demo.Services
{
    public class OnboardingWorkflowPolicy : IWorkflowPolicy<OnboardingRecord>
    {
        private readonly IEmployeeCodeAssignmentService? _employeeCodeAssignmentService;

        public OnboardingWorkflowPolicy(IEmployeeCodeAssignmentService? employeeCodeAssignmentService = null)
        {
            _employeeCodeAssignmentService = employeeCodeAssignmentService;
        }

        public string DomainName => "Onboarding";

        public Task<bool> IsEligibleApproverAsync(OnboardingRecord entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            return Task.FromResult(approverRole == "HR");
        }

        public async Task OnApprovedAsync(OnboardingRecord entity, ApplicationDbContext context)
        {
            if (entity.StartDate <= DateTime.Today && entity.EmployeeId.GetValueOrDefault() == 0)
            {
                // Create Canonical Employee if not exists
                var newEmp = new Employee
                {
                    Name = entity.EmployeeName,
                    Department = entity.Department,
                    JobTitle = entity.JobTitle,
                    Status = "Active",
                    Email = entity.Email,
                    PhoneNumber = entity.PhoneNumber,
                    Address = entity.Address,
                    Gender = entity.Gender,
                    BirthDate = entity.DateOfBirth
                };
                context.Employees.Add(newEmp);
                await context.SaveChangesAsync();
                var codeAssignment = _employeeCodeAssignmentService ?? new EmployeeCodeAssignmentService(context);
                await codeAssignment.EnsureAssignedAsync(newEmp);
                
                entity.EmployeeId = newEmp.Id;
                
                // Create Canonical User for login
                var newUser = new User
                {
                    Username = newEmp.EmployeeCode ?? "NEW_EMP",
                    FullName = entity.EmployeeName ?? "New Employee",
                    Email = entity.Email ?? "no-reply@example.com",
                    Role = "Employee",
                    CanLogin = true,
                    EmployeeId = newEmp.Id
                };
                context.Users.Add(newUser);
                await context.SaveChangesAsync();
            }
        }

        public Task OnRejectedAsync(OnboardingRecord entity, ApplicationDbContext context)
        {
            return Task.CompletedTask;
        }
    }
}
