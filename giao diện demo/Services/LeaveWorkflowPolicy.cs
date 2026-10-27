using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Services
{
    public class LeaveWorkflowPolicy : IWorkflowPolicy<Leave>
    {
        public string DomainName => "Leave";

        public async Task<bool> IsEligibleApproverAsync(Leave entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            // Business Rule: Workflow duy nhất: Employee → Manager → Approved/Rejected
            // HR, Leader, Admin KHÔNG duyệt Leave thông thường.
            if (approverRole != AppRoles.Manager) return false;

            // Kiểm tra trạng thái pending
            if (entity.Status != "Chờ duyệt" && entity.Status != "PENDING") return false;

            var manager = await context.Users.FirstOrDefaultAsync(u => u.Username == approverUsername && u.Role == AppRoles.Manager && u.CanLogin);
            if (manager == null) return false;

            var target = await context.Employees.FirstOrDefaultAsync(e => e.Id == entity.EmployeeId);
            if (target == null || string.IsNullOrEmpty(target.Department)) return false;

            // Không cho phép tự duyệt đơn của chính mình
            if (manager.EmployeeId.HasValue && manager.EmployeeId.Value == target.Id) return false;

            var now = System.DateTime.Now;
            return await context.ManagerAssignments.AnyAsync(a => a.UserId == manager.Id 
                && a.OrganizationalUnit == target.Department 
                && a.EffectiveFrom <= now 
                && (a.EffectiveTo == null || a.EffectiveTo > now));
        }

        public Task OnApprovedAsync(Leave entity, ApplicationDbContext context)
        {
            // P11 calculation engine automatically picks up approved leaves during runtime calculation.
            // No direct mutation to Attendance is needed immediately here unless pre-calculating.
            entity.Status = "Đã duyệt";
            return Task.CompletedTask;
        }

        public Task OnRejectedAsync(Leave entity, ApplicationDbContext context)
        {
            entity.Status = "Đã từ chối";
            return Task.CompletedTask;
        }
    }
}
