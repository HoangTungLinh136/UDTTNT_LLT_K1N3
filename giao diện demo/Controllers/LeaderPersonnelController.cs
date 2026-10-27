using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace giao_dien_demo.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = giao_dien_demo.Authorization.AppPolicies.LeaderOnly)]
    [AutoValidateAntiforgeryToken]
    public class LeaderPersonnelController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _scopeService;

        public LeaderPersonnelController(ApplicationDbContext context, giao_dien_demo.Services.ILeadershipScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        public IActionResult Index()
        {
            ViewBag.AllowedDepartments = _scopeService.GetOverseenOrganizationalUnits().OrderBy(d => d).ToList();
            return View();
        }

        [HttpGet]
        public IActionResult GetDashboardKPIs()
        {
            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits();
            if (!allowedDepts.Any()) 
                return Json(new LeaderPersonnelDashboardDto { TotalEmployees = 0, ActiveEmployees = 0, OnLeaveEmployees = 0, NewEmployeesThisMonth = 0 });

            var now = DateTime.Now;

            var query = _context.Employees.Where(e => allowedDepts.Contains(e.Department!));

            var total = query.Count();
            
            // Trạng thái 'Đang làm việc' được tính là khác Terminated và Suspended
            var active = query.Count(e => e.Status == "Active" || e.Status == "Probation");

            // Đếm nhân viên đang nghỉ phép trong khoảng thời gian hiện tại
            var employeeIds = query.Select(e => e.Id).ToList();
            var onLeave = _context.Leaves.Count(l => 
                l.Status == "Đã duyệt" && 
                l.FromDate <= now && l.ToDate >= now && 
                l.EmployeeId.HasValue && employeeIds.Contains(l.EmployeeId.Value));

            return Json(new LeaderPersonnelDashboardDto
            {
                TotalEmployees = total,
                ActiveEmployees = active,
                OnLeaveEmployees = onLeave,
                // No authoritative hire/join date exists on Employee; do not expose a fabricated metric.
                NewEmployeesThisMonth = 0
            });
        }

        [HttpGet]
        public IActionResult GetEmployees(string search = "", string department = "", int page = 1, int pageSize = 10)
        {
            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits();
            if (!allowedDepts.Any())
            {
                return Json(new LeaderPersonnelPageResponse<LeaderEmployeeDto>
                {
                    Items = new List<LeaderEmployeeDto>(),
                    TotalCount = 0,
                    PageNumber = page,
                    PageSize = pageSize,
                    AllowedDepartments = new List<string>()
                });
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.Employees.Where(e => allowedDepts.Contains(e.Department!));

            // Bộ lọc theo phòng/ban nếu người dùng chọn (và phải nằm trong AllowedDepts)
            if (!string.IsNullOrEmpty(department) && !allowedDepts.Contains(department))
                return Forbid();
            if (!string.IsNullOrEmpty(department) && allowedDepts.Contains(department))
            {
                query = query.Where(e => e.Department == department);
            }

            // Tìm kiếm (Mã nhân viên, Tên, Email)
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(e => 
                    (e.Name != null && e.Name.ToLower().Contains(search)) ||
                    (e.EmployeeCode != null && e.EmployeeCode.ToLower().Contains(search)) ||
                    (e.Email != null && e.Email.ToLower().Contains(search)));
            }

            int totalCount = query.Count();

            // Phân trang
            var list = query.OrderBy(e => e.Id)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .Select(e => new LeaderEmployeeDto
                            {
                                Id = e.Id,
                                EmployeeCode = e.EmployeeCode ?? "",
                                Name = e.Name ?? "",
                                AvatarPath = e.AvatarPath ?? "/images/default-avatar.png",
                                Department = e.Department ?? "",
                                JobTitle = e.JobTitle ?? "",
                                Position = e.Position ?? "",
                                Status = e.Status ?? ""
                            })
                            .ToList();

            return Json(new LeaderPersonnelPageResponse<LeaderEmployeeDto>
            {
                Items = list,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                AllowedDepartments = allowedDepts
            });
        }

        [HttpGet]
        public IActionResult GetEmployeeDetail(int id)
        {
            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits();
            
            var emp = _context.Employees.FirstOrDefault(e => e.Id == id);
            if (emp == null) return NotFound("Không tìm thấy nhân viên.");

            // Kiểm tra Data Scope
            if (!allowedDepts.Contains(emp.Department!))
                return Forbid(); // 403 Forbidden

            var detail = new LeaderEmployeeDetailDto
            {
                Id = emp.Id,
                EmployeeCode = emp.EmployeeCode ?? "",
                Name = emp.Name ?? "",
                AvatarPath = emp.AvatarPath ?? "/images/default-avatar.png",
                Email = emp.Email ?? "",
                PhoneNumber = emp.PhoneNumber ?? "",
                Department = emp.Department ?? "",
                JobTitle = emp.JobTitle ?? "",
                Position = emp.Position ?? ""
                ,Status = emp.Status ?? ""
            };

            return Json(detail);
        }

        [HttpPost]
        public IActionResult ProposeEmployeeChange([FromBody] ProposeEmployeeChangeDto dto)
        {
            // PROPOSAL-SEM-01: Leader is no longer the originator of Proposals.
            // Under the standardized governance model, Proposals are strategic HR initiatives initiated by HR.
            return BadRequest(new { success = false, message = "Chức năng tạo đề xuất từ phân hệ Quản lý Nhân sự Lãnh đạo đã ngừng hoạt động theo quy định quản trị đề xuất mới." });
        }
    }
}
