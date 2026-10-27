using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Services
{
    public class AdjustmentWorkflowPolicy : IWorkflowPolicy<AttendanceAdjustmentRequest>
    {
        public string DomainName => "AttendanceAdjustment";
        private readonly IAttendanceCalculationService _attendanceCalc;

        public AdjustmentWorkflowPolicy(IAttendanceCalculationService attendanceCalc)
        {
            _attendanceCalc = attendanceCalc;
        }

        public async Task<bool> IsEligibleApproverAsync(AttendanceAdjustmentRequest entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            // Business Rule: Workflow thông thường: Employee → Manager → Approved/Rejected
            // HR, Leader, Admin KHÔNG duyệt normal Adjustment.
            if (approverRole != AppRoles.Manager) return false;

            // Kiểm tra trạng thái pending
            if (entity.Status != "PENDING" && entity.Status != "Chờ duyệt") return false;

            var manager = await context.Users.FirstOrDefaultAsync(u => u.Username == approverUsername && u.Role == AppRoles.Manager && u.CanLogin);
            if (manager == null) return false;

            var target = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == entity.EmployeeCode);
            if (target == null || string.IsNullOrEmpty(target.Department)) return false;

            // Không cho phép tự duyệt đơn của chính mình
            if (manager.EmployeeId.HasValue && manager.EmployeeId.Value == target.Id) return false;

            var now = System.DateTime.Now;
            return await context.ManagerAssignments.AnyAsync(a => a.UserId == manager.Id 
                && a.OrganizationalUnit == target.Department 
                && a.EffectiveFrom <= now 
                && (a.EffectiveTo == null || a.EffectiveTo > now));
        }

        public async Task OnApprovedAsync(AttendanceAdjustmentRequest entity, ApplicationDbContext context)
        {
            var attendance = await context.Attendances.FindAsync(entity.AttendanceId);
            if (attendance != null)
            {
                if (entity.RequestedCheckIn.HasValue) 
                    attendance.CheckIn = attendance.CheckIn.Date.Add(entity.RequestedCheckIn.Value);
                
                if (entity.RequestedCheckOut.HasValue)
                    attendance.CheckOut = attendance.CheckIn.Date.Add(entity.RequestedCheckOut.Value);

                if (attendance.CheckOut.HasValue)
                {
                    await _attendanceCalc.ProcessAttendanceTransactionAsync(attendance);
                }

                string auditNote = $"[Được ĐC bởi {entity.ReviewedBy} ({DateTime.Now:dd/MM/yyyy HH:mm}): {entity.Reason}]";
                attendance.StatusNote = (attendance.StatusNote ?? "") + " " + auditNote;

                context.Attendances.Update(attendance);
            }
        }

        public Task OnRejectedAsync(AttendanceAdjustmentRequest entity, ApplicationDbContext context)
        {
            return Task.CompletedTask;
        }
    }
}
