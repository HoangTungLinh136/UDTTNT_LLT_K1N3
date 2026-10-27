using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace giao_dien_demo.Hubs;

[Authorize]
public class DashboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (!string.IsNullOrWhiteSpace(role))
        {
            // Canonical role group (e.g. role:HR, role:Employee, role:Manager, role:Leader, role:Admin)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");
        }

        var userName = Context.User?.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userName);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// RTSEC-007: Prevents arbitrary client-selected privileged attendance/role groups.
    /// Only allows user's personal identity group.
    /// </summary>
    public async Task JoinGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;

        var normalized = groupName.Trim();
        if (normalized.StartsWith("role:", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("attendance:", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("HR", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Leader", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var currentUserName = Context.User?.Identity?.Name;
        if (string.Equals(normalized, currentUserName, StringComparison.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, normalized);
        }
    }

    /// <summary>
    /// RTSEC-010: Verifies role strictly against server authenticated claims.
    /// </summary>
    public async Task JoinRoleGroup(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) return;

        var serverRole = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (string.Equals(serverRole, role, StringComparison.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{serverRole}");
        }
    }
}
