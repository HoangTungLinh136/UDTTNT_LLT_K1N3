using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using giao_dien_demo.Models;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.LeaderOnly)]
    public class LeaderLeaveController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _scopeService;

        public LeaderLeaveController(ApplicationDbContext context, giao_dien_demo.Services.ILeadershipScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        public async Task<IActionResult> Index(string department, string status, string search = "", int page = 1, int pageSize = 25)
        {
            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits();
            
            if (!allowedDepts.Any())
            {
                ViewBag.AllowedDepts = new List<string>();
                ViewBag.NoScope = true;
                return View(new List<Leave>());
            }

            if (!string.IsNullOrEmpty(department) && !allowedDepts.Contains(department)) return Forbid();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.Leaves.AsNoTracking().Include(l => l.Employee)
                .Where(l => l.EmployeeId.HasValue && l.Employee != null && allowedDepts.Contains(l.Employee!.Department!));
            if (!string.IsNullOrEmpty(department)) query = query.Where(l => l.Employee!.Department == department);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(l => (l.Employee!.EmployeeCode != null && l.Employee.EmployeeCode.Contains(search)) || (l.Employee.Name != null && l.Employee.Name.Contains(search)));
            }
            var totalCount = await query.CountAsync();
            var leaves = await query.OrderByDescending(l => l.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.AllowedDepts = allowedDepts;
            ViewBag.CurrentDept = department;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.NoScope = false;

            return View(leaves);
        }
    }
}
