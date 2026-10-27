using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.HROnly)]
    public class HRController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HRController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string search = "", string category = "", int page = 1)
        {
            const int pageSize = 9;
            page = Math.Max(1, page);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var soon = today.AddDays(ContractStatusSemantics.ExpiringWindowDays);

            var activeEmployees = await _context.Employees.CountAsync(e => e.Status == "Active" || e.Status == "Probation");
            var presentToday = await _context.Attendances
                .Where(a => a.CheckIn >= today && a.CheckIn < tomorrow
                    && a.EmployeeId != null
                    && a.Employee != null
                    && (a.Employee.Status == "Active" || a.Employee.Status == "Probation"))
                .Select(a => a.EmployeeId)
                .Distinct()
                .CountAsync();
            // HR-actionable states only. Attendance adjustments remain Manager-owned.
            var pendingContracts = await _context.Contracts.CountAsync(c => c.Status == ContractStatusSemantics.AwaitingFinalApproval);
            var pendingAppointments = await _context.AppointmentRecords.CountAsync(a => a.ApprovalStatus == "PENDING_HR_REVIEW");
            var pendingDismissals = await _context.DismissalRecords.CountAsync(d => d.ApprovalStatus == "PENDING_HR_REVIEW");
            var pendingTransfers = await _context.TransferRecords.CountAsync(t => t.ApprovalStatus == "PENDING_HR_REVIEW");
            var pendingRewards = await _context.RewardRecords.CountAsync(r => r.ApprovalStatus == "PENDING_APPROVAL");
            var pendingTraining = await _context.CourseEnrollments.CountAsync(e => e.ApprovalStatus == "PENDING_APPROVAL");
            var pendingBenefits = await _context.BenefitEnrollments.CountAsync(e => e.ApprovalStatus == "PENDING_APPROVAL");

            var modules = ExistingModules();
            var moduleCategories = modules.Select(m => m.Category).Distinct().OrderBy(x => x).ToList();
            if (!string.IsNullOrWhiteSpace(search))
            {
                modules = modules.Where(m => m.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || m.Description.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(category))
            {
                modules = modules.Where(m => m.Category == category).ToList();
            }
            var totalModules = modules.Count;
            var totalModulePages = Math.Max(1, (int)Math.Ceiling(totalModules / (double)pageSize));
            page = Math.Min(page, totalModulePages);

            var pendingWork = new List<HRPendingWorkItem>();
            pendingWork.AddRange(await _context.AppointmentRecords.AsNoTracking()
                .Where(a => a.ApprovalStatus == "PENDING_HR_REVIEW").OrderByDescending(a => a.CreatedAt).Take(5)
                .Select(a => new HRPendingWorkItem { Type = "Bổ nhiệm", Code = "BN" + a.Id, EmployeeName = a.EmployeeNameSnapshot ?? "", Summary = a.Reason ?? "Hồ sơ bổ nhiệm", Status = a.ApprovalStatus, SubmittedAt = a.CreatedAt, Controller = "Appointment", Action = "Index", RouteQuery = "?status=PENDING_HR_REVIEW" }).ToListAsync());
            pendingWork.AddRange(await _context.DismissalRecords.AsNoTracking()
                .Where(d => d.ApprovalStatus == "PENDING_HR_REVIEW").OrderByDescending(d => d.CreatedAt).Take(5)
                .Select(d => new HRPendingWorkItem { Type = "Miễn nhiệm", Code = "MN" + d.Id, EmployeeName = d.EmployeeNameSnapshot ?? "", Summary = d.Reason ?? "Hồ sơ miễn nhiệm", Status = d.ApprovalStatus, SubmittedAt = d.CreatedAt, Controller = "Dismissal", Action = "Index", RouteQuery = "?status=PENDING_HR_REVIEW" }).ToListAsync());
            pendingWork.AddRange(await _context.TransferRecords.AsNoTracking()
                .Where(t => t.ApprovalStatus == "PENDING_HR_REVIEW").OrderByDescending(t => t.CreatedAt).Take(5)
                .Select(t => new HRPendingWorkItem { Type = "Thuyên chuyển", Code = "TC" + t.Id, EmployeeName = t.EmployeeNameSnapshot ?? "", Summary = t.Reason ?? "Hồ sơ thuyên chuyển", Status = t.ApprovalStatus, SubmittedAt = t.CreatedAt, Controller = "Transfer", Action = "Index", RouteQuery = "?status=PENDING_HR_REVIEW" }).ToListAsync());
            pendingWork.AddRange(await _context.RewardRecords.AsNoTracking()
                .Where(r => r.ApprovalStatus == "PENDING_APPROVAL").OrderByDescending(r => r.CreatedAt).Take(5)
                .Select(r => new HRPendingWorkItem { Type = "Khen thưởng", Code = "KT" + r.Id, EmployeeName = r.EmployeeNameSnapshot ?? "", Summary = r.Title ?? r.Achievement ?? "Hồ sơ khen thưởng", Status = r.ApprovalStatus, SubmittedAt = r.CreatedAt, Controller = "Reward", Action = "Index", RouteQuery = "?status=PENDING_APPROVAL" }).ToListAsync());
            pendingWork.AddRange(await _context.CourseEnrollments.AsNoTracking()
                .Where(e => e.ApprovalStatus == "PENDING_APPROVAL").OrderByDescending(e => e.RegistrationDate).Take(5)
                .Select(e => new HRPendingWorkItem { Type = "Đào tạo", Code = "DT" + e.Id, EmployeeName = e.EmployeeNameSnapshot, Summary = e.Course != null ? e.Course.Name : "Đăng ký đào tạo", Status = e.ApprovalStatus, SubmittedAt = e.RegistrationDate, Controller = "Training", Action = "HrIndex" }).ToListAsync());
            pendingWork.AddRange(await _context.BenefitEnrollments.AsNoTracking()
                .Where(e => e.ApprovalStatus == "PENDING_APPROVAL").OrderByDescending(e => e.RegistrationDate).Take(5)
                .Select(e => new HRPendingWorkItem { Type = "Phúc lợi", Code = "PL" + e.Id, EmployeeName = e.EmployeeNameSnapshot, Summary = e.Program != null ? e.Program.Name : "Đăng ký phúc lợi", Status = e.ApprovalStatus, SubmittedAt = e.RegistrationDate, Controller = "Benefit", Action = "HrIndex" }).ToListAsync());
            pendingWork.AddRange(await _context.Contracts.AsNoTracking()
                .Where(c => c.Status == ContractStatusSemantics.AwaitingFinalApproval)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new HRPendingWorkItem
                {
                    Type = "Hợp đồng",
                    Code = "HD" + c.Id,
                    EmployeeName = c.EmployeeNameSnapshot ?? "",
                    Summary = c.Duration ?? "Hợp đồng lao động",
                    Status = c.Status,
                    SubmittedAt = c.CreatedAt,
                    Controller = "Contract",
                    Action = "Index"
                }).ToListAsync());

            var departments = await _context.Employees.AsNoTracking()
                .Where(e => (e.Status == "Active" || e.Status == "Probation") && e.Department != null && e.Department != "")
                .GroupBy(e => e.Department!)
                .Select(g => new HRDepartmentSummary { Department = g.Key, Employees = g.Count() })
                .OrderByDescending(x => x.Employees)
                .ThenBy(x => x.Department)
                .ToListAsync();

            return View(new HRWorkspaceViewModel
            {
                ActiveEmployeeCount = activeEmployees,
                PresentTodayCount = presentToday,
                PendingActionCount = pendingContracts + pendingAppointments + pendingDismissals + pendingTransfers + pendingRewards + pendingTraining + pendingBenefits,
                ExpiringContractCount = await _context.Contracts.CountAsync(c => c.Status == ContractStatusSemantics.Effective && c.StartDate <= today && c.EndDate >= today && c.EndDate <= soon),
                Search = search,
                Category = category,
                Page = page,
                PageSize = pageSize,
                TotalModules = totalModules,
                ModuleCategories = moduleCategories,
                Modules = modules.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                PendingWork = pendingWork.OrderByDescending(x => x.SubmittedAt).Take(8).ToList(),
                Departments = departments
            });
        }

        private static List<HRWorkspaceModule> ExistingModules() => new()
        {
            new() { Name = "Nhân viên", Description = "Hồ sơ và danh sách nhân viên", Category = "Nhân sự", Controller = "Employee", Icon = "fa-address-card" },
            new() { Name = "Chấm công", Description = "Dữ liệu chấm công hiện có", Category = "Thời gian", Controller = "Attendance", Icon = "fa-clock" },
            new() { Name = "Điều chỉnh chấm công", Description = "Xử lý yêu cầu điều chỉnh", Category = "Thời gian", Controller = "AttendanceAdjustment", Action = "Manage", Icon = "fa-clock-rotate-left" },
            new() { Name = "Nghỉ phép", Description = "Đơn nghỉ phép và phê duyệt", Category = "Thời gian", Controller = "Leave", Icon = "fa-umbrella-beach" },
            new() { Name = "Hợp đồng", Description = "Hợp đồng lao động", Category = "Nhân sự", Controller = "Contract", Icon = "fa-file-contract" },
            new() { Name = "Thanh toán lương", Description = "Kỳ và trạng thái thanh toán", Category = "Tiền lương", Controller = "PayrollPayment", Icon = "fa-money-check-dollar" },
            new() { Name = "Phúc lợi", Description = "Chương trình và đăng ký phúc lợi", Category = "Phát triển", Controller = "Benefit", Icon = "fa-hand-holding-heart" },
            new() { Name = "Đào tạo", Description = "Khóa học và hồ sơ đào tạo", Category = "Phát triển", Controller = "Training", Icon = "fa-graduation-cap" },
            new() { Name = "Hội nhập", Description = "Quy trình nhân viên mới", Category = "Vòng đời", Controller = "Onboarding", Icon = "fa-handshake-angle" },
            new() { Name = "Bổ nhiệm", Description = "Hồ sơ bổ nhiệm", Category = "Vòng đời", Controller = "Appointment", Icon = "fa-user-plus" },
            new() { Name = "Miễn nhiệm", Description = "Hồ sơ miễn nhiệm", Category = "Vòng đời", Controller = "Dismissal", Icon = "fa-user-minus" },
            new() { Name = "Thuyên chuyển", Description = "Điều chuyển nhân viên", Category = "Vòng đời", Controller = "Transfer", Icon = "fa-right-left" },
            new() { Name = "Nghỉ việc", Description = "Quy trình offboarding", Category = "Vòng đời", Controller = "Offboarding", Icon = "fa-person-walking-arrow-right" },
            new() { Name = "Khen thưởng", Description = "Hồ sơ khen thưởng", Category = "Quan hệ lao động", Controller = "Reward", Icon = "fa-award" },
            new() { Name = "Sự cố", Description = "Theo dõi sự cố nhân sự", Category = "Quan hệ lao động", Controller = "Incident", Icon = "fa-triangle-exclamation" },
            new() { Name = "Quy hoạch kế nhiệm", Description = "Kế hoạch và ứng viên kế nhiệm", Category = "Phát triển", Controller = "Succession", Icon = "fa-sitemap" },
            new() { Name = "Công việc HR", Description = "Theo dõi công việc nhân sự", Category = "Điều hành", Controller = "TaskTracking", Icon = "fa-list-check" },
            new() { Name = "Đề xuất", Description = "Đề xuất nghiệp vụ nhân sự", Category = "Điều hành", Controller = "Proposal", Icon = "fa-lightbulb" }
        };
    }
}



