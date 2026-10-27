using System.Collections.Generic;
using System.Linq;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    /// <summary>
    /// Centralized scope resolution for managerial roles (Manager, Leader, HR, Admin).
    /// Replaces all duplicated GetLeaderDepartments() methods scattered across controllers.
    /// 
    /// Scope rules:
    /// - Admin / HR → all departments
    /// - Manager → departments from active ManagerAssignment records
    /// - Leader → department from linked Employee record (legacy behavior preserved)
    /// - Employee → empty (no managerial scope)
    /// </summary>
    public interface IManagerScopeService
    {
        /// <summary>
        /// Get organizational units the current user can manage.
        /// </summary>
        List<string> GetManagedOrganizationalUnits();

        /// <summary>
        /// Check if the current user can access data for a specific department.
        /// </summary>
        bool CanAccessDepartment(string? department);

        /// <summary>
        /// Check if current user has managerial access (Manager, Leader, HR, Admin).
        /// </summary>
        bool HasManagerialAccess();

        /// <summary>
        /// Get scoped employee query — only employees within managed units.
        /// </summary>
        IQueryable<Employee> GetScopedEmployeeQuery();
    }
}
