using giao_dien_demo.Data;
using giao_dien_demo.Hubs;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Services;

public interface IInAppNotificationService
{
    Task<IReadOnlyList<Notification>> SendAsync(
        string recipientSelector,
        string message,
        string notificationType,
        string? targetUrl = null,
        int? referenceId = null,
        int? senderUserId = null,
        string? senderName = null,
        string? deduplicationKey = null,
        IReadOnlyCollection<int>? allowedRecipientUserIds = null);
}

public sealed class InAppNotificationService : IInAppNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<DashboardHub> _hubContext;

    public InAppNotificationService(ApplicationDbContext context, IHubContext<DashboardHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<IReadOnlyList<Notification>> SendAsync(
        string recipientSelector,
        string message,
        string notificationType,
        string? targetUrl = null,
        int? referenceId = null,
        int? senderUserId = null,
        string? senderName = null,
        string? deduplicationKey = null,
        IReadOnlyCollection<int>? allowedRecipientUserIds = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return Array.Empty<Notification>();

        var selector = recipientSelector?.Trim() ?? string.Empty;
        var users = _context.Users.Include(x => x.Employee).Where(x => x.CanLogin);
        if (selector is "Tất cả" or "Tất cả nhân viên")
            users = users.Where(x => x.Role == "Employee");
        else if (selector is "HR" or "Admin" or "Manager")
            users = users.Where(x => x.Role == selector);
        else if (selector is "Ban lãnh đạo" or "Leader")
            users = users.Where(x => x.Role == "Leader");
        else
            users = users.Where(x => x.Username == selector);

        if (allowedRecipientUserIds != null)
            users = users.Where(x => allowedRecipientUserIds.Contains(x.Id));

        var recipients = await users.AsNoTracking().ToListAsync();
        var created = new List<Notification>();
        foreach (var recipient in recipients)
        {
            var key = string.IsNullOrWhiteSpace(deduplicationKey) ? null : $"{deduplicationKey}:{recipient.Id}";
            if (key != null && await _context.Notifications.AnyAsync(x => x.DeduplicationKey == key))
                continue;

            var item = new Notification
            {
                RecipientUserId = recipient.Id,
                SenderUserId = senderUserId,
                SenderName = string.IsNullOrWhiteSpace(senderName) ? "Hệ thống" : senderName,
                NguoiNhan = recipient.Username,
                Message = message,
                NotificationType = notificationType,
                TargetUrl = targetUrl,
                ReferenceId = referenceId,
                CreatedAt = DateTime.Now,
                DeduplicationKey = key
            };
            _context.Notifications.Add(item);
            created.Add(item);
        }

        if (created.Count == 0) return created;
        await _context.SaveChangesAsync();

        foreach (var item in created)
        {
            var username = recipients.First(x => x.Id == item.RecipientUserId).Username;
            await _hubContext.Clients.User(username).SendAsync("ReceiveNotification", new
            {
                id = item.Id,
                content = item.Message,
                sender = item.SenderName,
                receiver = username,
                time = item.CreatedAt.ToString("HH:mm"),
                targetUrl = item.TargetUrl,
                isRead = item.IsRead,
                referenceId = item.ReferenceId
            });
        }

        return created;
    }
}
