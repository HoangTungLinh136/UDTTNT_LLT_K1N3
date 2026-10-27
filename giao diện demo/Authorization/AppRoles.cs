using System.Collections.Generic;

namespace giao_dien_demo.Authorization
{
    /// <summary>Canonical role names used by authentication and role administration.</summary>
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string HR = "HR";
        public const string Leader = "Leader";
        public const string Manager = "Manager";
        public const string Employee = "Employee";

        public static IReadOnlyList<string> All { get; } =
            new[] { Admin, HR, Leader, Manager, Employee };
    }

    public static class AppPolicies
    {
        public const string ManagerOnly = "ManagerOnly";
        public const string LeaderOnly = "LeaderOnly";
        public const string HROnly = "HROnly";
        public const string AdminOnly = "AdminOnly";
        public const string EmployeeOnly = "EmployeeOnly";
    }
}
