using System;
using System.Threading.Tasks;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Http;

namespace giao_dien_demo.Services
{
    public interface IAuditLogService
    {
        Task LogAsync(string action, string module, string resource, string resourceId, string eventType, string status, string failureReason = null, string oldValues = null, string newValues = null, string changes = null);
        Task LogLoginAsync(string username, bool isSuccess, string failureReason = null);
        Task LogLogoutAsync(string username);
        Task LogSecurityEventAsync(string action, string resource, string resourceId, string status, string failureReason = null);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string module, string resource, string resourceId, string eventType, string status, string failureReason = null, string oldValues = null, string newValues = null, string changes = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User?.Identity?.IsAuthenticated == true
                ? (httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? httpContext.User.Identity.Name)
                : "System";
            var role = httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var ip = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "";
            var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "";
            var requestId = httpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();
            var correlationId = httpContext?.Request?.Headers["X-Correlation-ID"].ToString();
            if (string.IsNullOrEmpty(correlationId)) correlationId = requestId;

            var audit = new AuditLog
            {
                Timestamp = DateTime.Now,
                UserId = user,
                Username = user,
                Role = role,
                Module = module,
                Action = action,
                Resource = resource,
                ResourceId = resourceId,
                EventType = eventType,
                Status = status,
                IPAddress = ip,
                UserAgent = userAgent,
                FailureReason = failureReason ?? string.Empty,
                OldValues = MaskSecrets(oldValues),
                NewValues = MaskSecrets(newValues),
                Changes = MaskSecrets(changes),
                RequestId = requestId,
                CorrelationId = correlationId,
                Details =
                    $"Module={module}; EventType={eventType}; Status={status}; " +
                    $"Role={role}; IP={ip}; RequestId={requestId}; CorrelationId={correlationId}; " +
                    $"FailureReason={failureReason ?? string.Empty}; " +
                    $"OldValues={MaskSecrets(oldValues)}; NewValues={MaskSecrets(newValues)}; Changes={MaskSecrets(changes)}"
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }

        private string MaskSecrets(string json)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;
            // A simple string replace for demonstration. Real implementation would parse JSON and mask specific keys.
            return System.Text.RegularExpressions.Regex.Replace(json, @"""(password|secret|token)""\s*:\s*""[^""]+""", @"""$1"": ""***""");
        }

        public async Task LogLoginAsync(string username, bool isSuccess, string failureReason = null)
        {
            var ip = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "";
            var userAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString() ?? "";

            var audit = new AuditLog
            {
                Timestamp = DateTime.Now,
                UserId = username,
                Username = username,
                Role = "",
                Module = "Authentication",
                Action = "Login",
                Resource = "User",
                ResourceId = username,
                EventType = "Login",
                Status = isSuccess ? "Success" : "Failed",
                IPAddress = ip,
                UserAgent = userAgent,
                FailureReason = failureReason ?? string.Empty,
                Details =
                    $"Module=Authentication; EventType=Login; " +
                    $"Status={(isSuccess ? "Success" : "Failed")}; " +
                    $"IP={ip}; FailureReason={failureReason ?? string.Empty}"
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }

        public async Task LogLogoutAsync(string username)
        {
            await LogAsync("Logout", "Authentication", "User", username, "Login", "Success");
        }

        public async Task LogSecurityEventAsync(string action, string resource, string resourceId, string status, string failureReason = null)
        {
            await LogAsync(action, "Security", resource, resourceId, "Security", status, failureReason);
        }
    }
}

