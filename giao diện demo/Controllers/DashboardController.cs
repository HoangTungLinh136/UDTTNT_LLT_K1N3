using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Collections.Generic;
using giao_dien_demo.Models;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;
using giao_dien_demo.Services;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IManagerScopeService _managerScope;
        private readonly ILeadershipScopeService _leadershipScope;

        public DashboardController(
            ApplicationDbContext context,
            IManagerScopeService managerScope,
            ILeadershipScopeService leadershipScope)
        {
            _context = context;
            _managerScope = managerScope;
            _leadershipScope = leadershipScope;
        }

        private IQueryable<Employee>? GetScopedEmployeeQuery()
        {
            var role = CurrentActor.Role(User);
            if (role == AppRoles.HR)
            {
                return _context.Employees.AsQueryable();
            }
            if (role == AppRoles.Manager)
            {
                return _managerScope.GetScopedEmployeeQuery();
            }
            if (role == AppRoles.Leader)
            {
                return _leadershipScope.GetScopedEmployeeQuery();
            }
            return null; // Employee and Admin are not granted directory access on this HR business dashboard
        }

        // ================== DASHBOARD ==================
        public async Task<IActionResult> Index()
        {
            var query = GetScopedEmployeeQuery();
            if (query == null)
            {
                return Forbid();
            }

            var employees = await query.ToListAsync();
            var totalEmployees = employees.Count;

            var workingEmployees = employees.Count(e => e.Status == "Active" || e.Status == "Probation");
            var resignedEmployees = employees.Count(e => e.Status == "Offboarded" || e.Status == "Terminated");

            ViewBag.Total = totalEmployees;
            ViewBag.Working = workingEmployees;
            ViewBag.Leave = resignedEmployees;

            return View();
        }

        // ================== EMPLOYEE LIST ==================
        public async Task<IActionResult> EmployeeList(string status)
        {
            var query = GetScopedEmployeeQuery();
            if (query == null)
            {
                return Forbid();
            }

            var employees = await query.ToListAsync();

            List<Employee> result;

            if (status == "working")
            {
                result = employees
                    .Where(e => e.Status == "Active" || e.Status == "Probation")
                    .ToList();
            }
            else if (status == "quit")
            {
                result = employees
                    .Where(e => e.Status == "Offboarded" || e.Status == "Terminated")
                    .ToList();
            }
            else
            {
                result = employees;
            }

            return View(result);
        }
    }
}