using giao_dien_demo.Hubs;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace giao_dien_demo.Services;

public interface IAdminAccountNotificationService
{
    Task NotifyCreatedAsync(User user, int totalUsers);
}

/// <summary>Publishes the Admin user-list update without blocking persistence on SignalR.</summary>
public sealed class AdminAccountNotificationService : IAdminAccountNotificationService
{
    private readonly IHubContext<DashboardHub>? _hubContext;
    private readonly ILogger<AdminAccountNotificationService>? _logger;

    public AdminAccountNotificationService(IHubContext<DashboardHub>? hubContext, ILogger<AdminAccountNotificationService>? logger = null)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyCreatedAsync(User user, int totalUsers)
    {
        if (_hubContext is null) return;
        try
        {
            await _hubContext.Clients.Group("role:Admin").SendAsync("NewUserAdded", new
            {
                id = user.Id,
                fullName = user.FullName,
                username = user.Username,
                role = user.Role,
                totalEmployees = totalUsers
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Admin account notification failed after User {UserId} was persisted.", user.Id);
        }
    }
}
