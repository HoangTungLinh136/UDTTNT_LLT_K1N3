using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Services
{
    public class LeadershipScopeService : ILeadershipScopeService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LeadershipScopeService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
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
            return user?.Identity?.IsAuthenticated == true ? CurrentActor.Role(user) : null;
        }

        private string? GetUsername()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true ? CurrentActor.Username(user) : null;
        }

        public List<string> GetOverseenOrganizationalUnits()
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

            // Leader: query LeaderAssignment table for active assignments
            if (role == AppRoles.Leader)
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username))
                    return new List<string>();

                var user = _context.Users.FirstOrDefault(u => u.Username == username);
                if (user == null)
                    return new List<string>();

                var now = DateTime.Now;
                return _context.LeaderAssignments
                    .Where(la => la.UserId == user.Id
                              && la.EffectiveFrom <= now
                              && (la.EffectiveTo == null || la.EffectiveTo > now))
                    .Select(la => la.OrganizationalUnit)
                    .Distinct()
                    .ToList();
            }

            // Manager, Employee or unknown role: no leadership scope
            return new List<string>();
        }

        public bool CanAccessDepartment(string? department)
        {
            if (string.IsNullOrEmpty(department))
                return false;

            var allowedDepts = GetOverseenOrganizationalUnits();
            return allowedDepts.Contains(department);
        }

        public bool HasLeadershipAccess()
        {
            var role = GetRole();
            if (role == AppRoles.Admin || role == AppRoles.HR)
                return true;

            // A Leader role without an active assignment has no business scope.
            return role == AppRoles.Leader && GetOverseenOrganizationalUnits().Any();
        }

        public IQueryable<Employee> GetScopedEmployeeQuery()
        {
            var allowedDepts = GetOverseenOrganizationalUnits();

            if (!allowedDepts.Any())
                return _context.Employees.Where(e => false); // Empty result

            return _context.Employees
                .Where(e => allowedDepts.Contains(e.Department));
        }
    }
}
