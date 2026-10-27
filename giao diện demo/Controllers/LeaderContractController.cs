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
    public class LeaderContractController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _scopeService;

        public LeaderContractController(ApplicationDbContext context, giao_dien_demo.Services.ILeadershipScopeService scopeService)
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
                return View(new List<Contract>());
            }

            if (!string.IsNullOrEmpty(department) && !allowedDepts.Contains(department)) return Forbid();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Contracts.Include(c => c.Employee).Where(c => c.Employee != null && allowedDepts.Contains(c.Employee!.Department!));

            if (!string.IsNullOrEmpty(department) && allowedDepts.Contains(department))
            {
                query = query.Where(c => c.Employee!.Department == department);
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (status == ContractStatusSemantics.ExpiredFilter)
                {
                    var today = DateTime.Today;
                    query = query.Where(c => c.Status == ContractStatusSemantics.Effective
                        && c.StartDate <= today
                        && c.EndDate != default
                        && c.EndDate < today);
                }
                else
                {
                    query = query.Where(c => c.Status == status);
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(c => (c.Employee!.EmployeeCode != null && c.Employee.EmployeeCode.Contains(search)) || (c.Employee.Name != null && c.Employee.Name.Contains(search)));
            }

            var totalCount = await query.CountAsync();
            var contracts = await query.OrderBy(c => c.EndDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.AllowedDepts = allowedDepts;
            ViewBag.CurrentDept = department;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;

            return View(contracts);
        }
    }
}
