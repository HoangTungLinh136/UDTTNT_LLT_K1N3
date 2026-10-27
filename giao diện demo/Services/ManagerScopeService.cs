using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Services
{
    /// <summary>
    /// Centralized scope resolution service.
    /// 
    /// This service dynamically queries the database on each call (not cached in session)
    /// so that scope changes are immediately reactive:
    /// - Employee transfer → Employee.Department changes → auto-excluded/included
    /// - Manager reassignment → ManagerAssignment.EffectiveTo set → scope narrows
    /// - New assignment → new ManagerAssignment row → scope expands
    /// </summary>
    public class ManagerScopeService : IManagerScopeService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ManagerScopeService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private string? GetSessionValue(string key)
        {
            return _httpContextAccessor.HttpContext?.Session?.GetString(key);
        }

        private string? GetRole()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var roleClaim = CurrentActor.Role(user);
                if (!string.IsNullOrEmpty(roleClaim)) return roleClaim;
            }
            return GetSessionValue("Role");
        }

        private string? GetUsername()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var userClaim = CurrentActor.Username(user);
                if (!string.IsNullOrEmpty(userClaim)) return userClaim;
            }
            return GetSessionValue("User") ?? GetSessionValue("Username");
        }

        /// <summary>
        /// Get organizational units the current user can manage.
        /// 
        /// Admin/HR: all departments in the system.
        /// Manager: departments from active ManagerAssignment records.
        /// Leader: single department from linked Employee record (preserves LeaderScope).
        /// Employee: empty list.
        /// </summary>
        public List<string> GetManagedOrganizationalUnits()
        {
            var role = GetRole();

            // Admin and HR have global access
            if (role == AppRoles.Admin || role == AppRoles.HR)
            {
                return _context.Employees
                    .Where(e => !string.IsNullOrEmpty(e.Department))
                    .Select(e => e.Department!)
                    .Distinct()
                    .ToList();
            }

            // Manager: query ManagerAssignment table for active assignments
            if (role == AppRoles.Manager)
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username))
                    return new List<string>();

                var user = _context.Users.FirstOrDefault(u => u.Username == username);
                if (user == null)
                    return new List<string>();

                var now = DateTime.Now;
                return _context.ManagerAssignments
                    .Where(ma => ma.UserId == user.Id
                              && ma.EffectiveFrom <= now
                              && (ma.EffectiveTo == null || ma.EffectiveTo > now))
                    .Select(ma => ma.OrganizationalUnit)
                    .Distinct()
                    .ToList();
            }

            // Manager, Employee or unknown role: no managerial scope
            return new List<string>();


        }

        /// <summary>
        /// Check if the current user can access data for a specific department.
        /// </summary>
        public bool CanAccessDepartment(string? department)
        {
            if (string.IsNullOrEmpty(department))
                return false;

            var allowedDepts = GetManagedOrganizationalUnits();
            return allowedDepts.Contains(department);
        }

        /// <summary>
        /// Check if current user has managerial access.
        /// </summary>
        public bool HasManagerialAccess()
        {
            var role = GetRole();
            return role == AppRoles.Admin || role == AppRoles.HR || role == AppRoles.Leader || role == AppRoles.Manager;
        }

        /// <summary>
        /// Get scoped employee query — only employees within managed units.
        /// </summary>
        public IQueryable<Employee> GetScopedEmployeeQuery()
        {
            var allowedDepts = GetManagedOrganizationalUnits();

            if (!allowedDepts.Any())
                return _context.Employees.Where(e => false); // Empty result

            return _context.Employees
                .Where(e => allowedDepts.Contains(e.Department));
        }
    }
}
