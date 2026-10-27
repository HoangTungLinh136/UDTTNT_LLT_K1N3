using System.Collections.Generic;
using System.Linq;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    /// <summary>
    /// Centralized scope resolution for Leader role.
    /// 
    /// Scope rules:
    /// - Admin / HR → all departments (global access)
    /// - Leader → departments from active LeaderAssignment records
    /// - Manager / Employee → empty (not a leader)
    /// </summary>
    public interface ILeadershipScopeService
    {
        /// <summary>
        /// Get organizational units the current user can oversee as a Leader.
        /// </summary>
        List<string> GetOverseenOrganizationalUnits();

        /// <summary>
        /// Check if the current user can access data for a specific department as a Leader.
        /// </summary>
        bool CanAccessDepartment(string? department);

        /// <summary>
        /// Check if current user has leadership access (Leader, HR, Admin).
        /// </summary>
        bool HasLeadershipAccess();

        /// <summary>
        /// Get scoped employee query — only employees within overseen units.
        /// </summary>
        IQueryable<Employee> GetScopedEmployeeQuery();
    }
}
