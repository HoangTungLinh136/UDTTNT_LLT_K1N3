namespace giao_dien_demo.Authorization;

/// <summary>
/// Roles that may use the personal attendance surface. The permission is
/// intentionally separate from team/monitoring attendance scope: identity
/// still requires a linked Employee record at the controller boundary.
/// </summary>
public static class PersonalAttendanceAccess
{
    public static bool IsAllowedRole(string? role) =>
        role is AppRoles.Employee or AppRoles.HR or AppRoles.Manager;
}
