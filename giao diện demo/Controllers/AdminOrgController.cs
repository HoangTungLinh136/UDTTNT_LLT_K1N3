using Microsoft.AspNetCore.Mvc;
using giao_dien_demo.Data;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AdminOrgController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;

        public AdminOrgController(ApplicationDbContext context, giao_dien_demo.Services.IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
        }

        public IActionResult Index()
        {

            // Real data derived from Employee's Department string since there is no OrgUnit table
            var departmentsQuery = _context.Employees
                .Where(e => !string.IsNullOrEmpty(e.Department))
                .GroupBy(e => e.Department)
                .Select(g => new {
                    Name = g.Key,
                    EmployeeCount = g.Count(),
                    // Heuristic for manager
                    ManagerName = g.OrderByDescending(e => e.JobTitle != null && (e.JobTitle.Contains("Trưởng") || e.JobTitle.Contains("Quản lý") || e.JobTitle.Contains("Giám đốc")))
                                   .ThenBy(e => e.Id)
                                   .Select(e => e.Name)
                                   .FirstOrDefault()
                })
                .OrderBy(d => d.Name)
                .ToList();

            ViewBag.Departments = departmentsQuery;
            ViewBag.TotalDepartments = departmentsQuery.Count;
            ViewBag.TotalEmployees = departmentsQuery.Sum(d => d.EmployeeCount);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RenameDepartment(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName))
            {
                TempData["ErrorMessage"] = "Tên đơn vị cũ không hợp lệ.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(newName)) 
            {
                TempData["ErrorMessage"] = "Tên đơn vị mới không hợp lệ.";
                return RedirectToAction("Index");
            }
            newName = newName.Trim();

            bool oldNameExists = _context.Employees.Any(e => e.Department == oldName);
            if (!oldNameExists)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn vị để đổi tên (không có nhân viên nào).";
                return RedirectToAction("Index");
            }

            bool duplicateExists = _context.Employees.Any(e => e.Department != null && e.Department.ToLower() == newName.ToLower());
            if (duplicateExists && !oldName.Equals(newName, System.StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = $"Đơn vị '{newName}' đã tồn tại. Vui lòng chọn tên khác.";
                return RedirectToAction("Index");
            }

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // 1. Employee
                var employees = _context.Employees.Where(e => e.Department == oldName).ToList();
                foreach (var emp in employees) 
                {
                    emp.Department = newName;
                }

                // 2. ManagerAssignment
                var managerAssignments = _context.ManagerAssignments.Where(m => m.OrganizationalUnit == oldName).ToList();
                foreach (var ma in managerAssignments)
                {
                    ma.OrganizationalUnit = newName;
                }

                // 3. LeaderAssignment
                var leaderAssignments = _context.LeaderAssignments.Where(l => l.OrganizationalUnit == oldName).ToList();
                foreach (var la in leaderAssignments)
                {
                    la.OrganizationalUnit = newName;
                }

                // 4. AppointmentRecord (Target future workflow)
                var appointments = _context.AppointmentRecords
                    .Where(a => a.NewDepartment == oldName && a.ApprovalStatus != "EFFECTIVE" && a.AppliedAt == null && a.ApprovalStatus != "REJECTED" && a.ApprovalStatus != "CANCELLED")
                    .ToList();
                foreach(var a in appointments)
                {
                    a.NewDepartment = newName;
                }

                // 5. TransferRecord (Target future workflow)
                var transfers = _context.TransferRecords
                    .Where(t => t.TargetDepartment == oldName && t.ApprovalStatus != "EFFECTIVE" && t.AppliedAt == null && t.ApprovalStatus != "REJECTED" && t.ApprovalStatus != "CANCELLED")
                    .ToList();
                foreach(var t in transfers)
                {
                    t.TargetDepartment = newName;
                }

                // 6. OnboardingRecord (Active workflows)
                var onboardings = _context.OnboardingRecords
                    .Where(o => o.Department == oldName && (o.OnboardingStatus == "PENDING" || o.OnboardingStatus == "IN_PROGRESS" || o.OnboardingStatus == null))
                    .ToList();
                foreach(var o in onboardings)
                {
                    o.Department = newName;
                }

                // 7. SuccessionPlan (Active plans)
                var successionPlans = _context.SuccessionPlans
                    .Where(s => s.Department == oldName && s.Status != "CLOSED" && s.Status != "COMPLETED")
                    .ToList();
                foreach(var s in successionPlans)
                {
                    s.Department = newName;
                }

                _context.SaveChanges();
                transaction.Commit();
                
                _ = _auditLogService.LogAsync("RenameDepartment", "Organization", "Department", newName, "DataChange", "Success", null, oldName, newName);

                TempData["SuccessMessage"] = $"Đã đổi tên đơn vị '{oldName}' thành '{newName}' (cập nhật {employees.Count} NV, {managerAssignments.Count} QL, {leaderAssignments.Count} LĐ).";
            }
            catch (System.Exception ex)
            {
                transaction.Rollback();
                TempData["ErrorMessage"] = "Có lỗi xảy ra trong quá trình đổi tên. Đã rollback toàn bộ thay đổi.";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult GetDepartmentEmployees(string deptName)
        {

            var employees = _context.Employees
                .Where(e => e.Department == deptName)
                .Select(e => new { 
                    e.Id, 
                    e.EmployeeCode, 
                    e.Name, 
                    e.JobTitle, 
                    e.Email,
                    e.AvatarPath
                })
                .OrderBy(e => e.Name)
                .ToList();

            return Json(employees);
        }
    }
}
