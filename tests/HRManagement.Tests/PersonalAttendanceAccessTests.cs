using giao_dien_demo.Authorization;
using Xunit;

namespace HRManagement.Tests;

public sealed class PersonalAttendanceAccessTests
{
    [Theory]
    [InlineData(AppRoles.Employee)]
    [InlineData(AppRoles.HR)]
    [InlineData(AppRoles.Manager)]
    public void ATTSELF_AUTH_allowed_roles_require_personal_employee_capability(string role)
    {
        Assert.True(PersonalAttendanceAccess.IsAllowedRole(role));
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.Leader)]
    [InlineData(null)]
    public void ATTSELF_AUTH_monitoring_or_unknown_roles_cannot_use_personal_surface(string? role)
    {
        Assert.False(PersonalAttendanceAccess.IsAllowedRole(role));
    }
}
