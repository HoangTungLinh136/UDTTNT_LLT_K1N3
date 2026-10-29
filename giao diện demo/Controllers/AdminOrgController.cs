using Microsoft.AspNetCore.Mvc;
using giao_dien_demo.Data;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.HR}")]
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
            // CR-01: Standard departments from Departments table (HR, IT, MKT, FIN, SALES, OPS)
            var standardDepartments = _context.Departments
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.Id)
                .ToList();

            var allEmployees = _context.Employees
                .AsNoTracking()
                .ToList();

            var departmentList = standardDepartments.Select(d => {
                var deptEmployees = allEmployees
                    .Where(e => e.DepartmentId == d.Id || (!e.DepartmentId.HasValue && string.Equals(e.Department, d.Name, System.StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                var manager = deptEmployees
                    .OrderByDescending(e => e.JobTitle != null && (e.JobTitle.Contains("Trưởng") || e.JobTitle.Contains("Quản lý") || e.JobTitle.Contains("Giám đốc") || e.JobTitle.Contains("Leader") || e.JobTitle.Contains("Manager")))
                    .ThenBy(e => e.Id)
                    .FirstOrDefault();

                return new {
                    Id = d.Id,
                    Code = d.Code,
                    Name = d.Name,
                    EmployeeCount = deptEmployees.Count,
                    ManagerName = manager != null ? manager.Name : "Chưa bổ nhiệm"
                };
            }).ToList();

            // Unassigned employee group (DepartmentId is null and not matching standard departments)
            var unassignedEmployees = allEmployees
                .Where(e => !e.DepartmentId.HasValue && (string.IsNullOrEmpty(e.Department) || e.Department == "Chưa xếp phòng" || !standardDepartments.Any(sd => string.Equals(sd.Name, e.Department, System.StringComparison.OrdinalIgnoreCase))))
                .ToList();

            ViewBag.Departments = departmentList;
            ViewBag.TotalDepartments = standardDepartments.Count;
            ViewBag.TotalEmployees = allEmployees.Count - unassignedEmployees.Count;
            ViewBag.UnassignedCount = unassignedEmployees.Count;
            ViewBag.TotalAllEmployees = allEmployees.Count;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPolicies.AdminOnly)]
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

            var deptEntity = _context.Departments.FirstOrDefault(d => d.Name == oldName || d.Code == oldName);
            bool oldNameExists = deptEntity != null || _context.Employees.Any(e => e.Department == oldName);
            if (!oldNameExists)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn vị để đổi tên.";
                return RedirectToAction("Index");
            }

            bool duplicateExists = _context.Departments.Any(d => d.Name.ToLower() == newName.ToLower() && d.Name.ToLower() != oldName.ToLower())
                || _context.Employees.Any(e => e.Department != null && e.Department.ToLower() == newName.ToLower() && e.Department.ToLower() != oldName.ToLower());
            if (duplicateExists)
            {
                TempData["ErrorMessage"] = $"Đơn vị '{newName}' đã tồn tại. Vui lòng chọn tên khác.";
                return RedirectToAction("Index");
            }

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // 0. Update Department Entity if standard department
                if (deptEntity != null)
                {
                    deptEntity.Name = newName;
                }

                // 1. Employee
                var employees = _context.Employees.Where(e => (deptEntity != null && e.DepartmentId == deptEntity.Id) || e.Department == oldName).ToList();
                foreach (var emp in employees) 
                {
                    emp.Department = newName;
                    if (deptEntity != null && !emp.DepartmentId.HasValue)
                    {
                        emp.DepartmentId = deptEntity.Id;
                    }
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
            if (string.Equals(deptName, "Chưa xếp phòng", System.StringComparison.OrdinalIgnoreCase) || 
                string.Equals(deptName, "Chưa phân bổ", System.StringComparison.OrdinalIgnoreCase) || 
                string.Equals(deptName, "UNASSIGNED", System.StringComparison.OrdinalIgnoreCase))
            {
                var standardDeptNames = _context.Departments.Where(d => d.IsActive).Select(d => d.Name).ToList();
                var unassigned = _context.Employees
                    .Where(e => !e.DepartmentId.HasValue && (string.IsNullOrEmpty(e.Department) || e.Department == "Chưa xếp phòng" || !standardDeptNames.Contains(e.Department)))
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

                return Json(unassigned);
            }

            var dept = _context.Departments.FirstOrDefault(d => d.Name == deptName || d.Code == deptName);
            var employees = _context.Employees
                .Where(e => (dept != null && e.DepartmentId == dept.Id) || e.Department == deptName)
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
