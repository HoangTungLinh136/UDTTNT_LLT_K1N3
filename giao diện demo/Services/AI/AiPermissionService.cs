using System.Data;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Models.AI;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Services.AI;

public sealed class AiPermissionService(ApplicationDbContext db,
    IManagerScopeService managers, ILeadershipScopeService leaders) : IAiPermissionService
{
    public async Task<AvaUserContext> GetContextAsync(User user, CancellationToken cancellationToken = default)
    {
        var role = user.Role;
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scope = role switch { AppRoles.Admin or AppRoles.HR => "ALL", AppRoles.Leader => "DEPARTMENT", AppRoles.Manager => "ASSIGNED", _ => "SELF" };
        var payroll = false;
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT PermissionKey,IsAllowed FROM dbo.AiRolePermissions WHERE RoleName=@role";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@role"; parameter.Value = role;
            command.Parameters.Add(parameter);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) if (reader.GetBoolean(1)) permissions.Add(reader.GetString(0));
        }
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT PermissionKey,IsAllowed FROM dbo.AiAccountPermissions WHERE UserId=@id";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@id"; parameter.Value = user.Id;
            command.Parameters.Add(parameter);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetBoolean(1)) permissions.Add(reader.GetString(0));
                else permissions.Remove(reader.GetString(0));
            }
        }
        payroll = permissions.Contains("ai.hr.payroll.read");
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT DataScope,AllowSensitivePayroll FROM dbo.AiAccountDataScopes WHERE UserId=@id";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@id"; parameter.Value = user.Id;
            command.Parameters.Add(parameter);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var requested = reader.GetString(0).ToUpperInvariant();
                if (requested == "SELF" || requested == scope || scope == "ALL" && requested is "DEPARTMENT" or "ASSIGNED") scope = requested;
                payroll = payroll && reader.GetBoolean(1);
            }
        }
        if (!payroll) permissions.Remove("ai.hr.payroll.read");
        var employee = user.Employee ?? (user.EmployeeId.HasValue
            ? await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == user.EmployeeId, cancellationToken) : null);
        IReadOnlyList<string> departments = role switch
        {
            AppRoles.Leader => leaders.GetOverseenOrganizationalUnits(),
            AppRoles.Manager => managers.GetManagedOrganizationalUnits(),
            _ => string.IsNullOrWhiteSpace(employee?.Department) ? [] : [employee.Department]
        };
        IReadOnlyList<string> assigned = role == AppRoles.Manager
            ? await managers.GetScopedEmployeeQuery().Select(x => x.Id.ToString()).ToListAsync(cancellationToken) : [];
        // Scope ceilings and assignment lists always originate in HRMS, never in browser JSON.
        return new AvaUserContext
        {
            UserId = user.Id.ToString(), EmployeeId = user.EmployeeId?.ToString(), EmployeeCode = employee?.EmployeeCode,
            IsAuthenticated = user.CanLogin, Roles = [role], Permissions = permissions.ToArray(),
            DataScope = scope, DepartmentNames = departments, AssignedEmployeeIds = assigned,
            AllowSensitivePayroll = payroll
        };
    }
}
