using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using giao_dien_demo.Models;
using giao_dien_demo.Data;

namespace giao_dien_demo.Services
{
    public class NotificationEngineService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly NotificationConfigService _configService;
        private readonly ILogger<NotificationEngineService> _logger;

        public NotificationEngineService(
            IServiceScopeFactory scopeFactory, 
            NotificationConfigService configService,
            ILogger<NotificationEngineService> logger)
        {
            _scopeFactory = scopeFactory;
            _configService = configService;
            _logger = logger;
        }

        public void QueueEvent(ApplicationDbContext dbContext, string eventCode, object payload, string relatedUsername = null, string relatedManagerUsername = null, string idempotencyKey = null)
        {
            try 
            {
                var outboxMsg = new EventOutboxMessage
                {
                    EventCode = eventCode,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload),
                    RelatedUsername = relatedUsername,
                    RelatedManagerUsername = relatedManagerUsername,
                    IdempotencyKey = idempotencyKey,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                };
                dbContext.EventOutboxMessages.Add(outboxMsg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queueing notification event: " + eventCode);
            }
        }

        public async Task ProcessOutboxMessageAsync(EventOutboxMessage message, ApplicationDbContext dbContext)
        {
            try 
            {
                var activeRules = await dbContext.NotificationRules
                    .Include(r => r.RecipientGroup)
                    .Include(r => r.EmailTemplate)
                    .Where(r => r.EventCode == message.EventCode && r.IsActive)
                    .ToListAsync();

                if (!activeRules.Any()) return;

                if (!string.IsNullOrEmpty(message.IdempotencyKey))
                {
                    var alreadySent = await dbContext.NotificationLogs
                        .AnyAsync(l => l.IdempotencyKey == message.IdempotencyKey && l.Status == "Sent");
                    if (alreadySent)
                    {
                        _logger.LogInformation($"Notification for idempotency key {message.IdempotencyKey} already sent. Skipping.");
                        return;
                    }
                }

                var settings = _configService.LoadSettings();
                var smtp = settings?.Smtp;
                if (smtp == null || string.IsNullOrEmpty(smtp.Host) || string.IsNullOrEmpty(smtp.Username))
                {
                    _logger.LogWarning("Cannot send notifications. SMTP is not configured.");
                    throw new Exception("SMTP is not configured.");
                }

                // Deserialize payload for template rendering
                object payload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(message.PayloadJson);

                foreach (var rule in activeRules)
                {
                    if (rule.EmailTemplate == null || rule.RecipientGroup == null) continue;

                    var canonicalUsers = await ResolveRecipientsAsync(dbContext, rule.RecipientGroup, message.RelatedUsername, message.RelatedManagerUsername);
                    
                    if (!canonicalUsers.Any()) continue;

                    string subject = RenderTemplate(rule.EmailTemplate.Subject, payload);
                    string body = RenderTemplate(rule.EmailTemplate.Body, payload);

                    var emails = await GetEmailsForCanonicalUsers(dbContext, canonicalUsers);

                    foreach (var target in emails)
                    {
                        bool success = await SendEmailAsync(smtp, target.Email, subject, body);
                        
                        var log = new NotificationLog
                        {
                            RuleId = rule.Id,
                            EventCode = message.EventCode,
                            RecipientEmail = target.Email,
                            TargetUserId = target.UserId,
                            Status = success ? "Sent" : "Failed",
                            FailureReason = success ? null : "SMTP Error",
                            SentAt = DateTime.Now,
                            IdempotencyKey = message.IdempotencyKey
                        };
                        dbContext.NotificationLogs.Add(log);
                        if (!success) throw new Exception($"Failed to send email to {target.Email}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox message: " + message.Id);
                throw; // Rethrow to let the BackgroundWorker mark it as failed
            }
        }

        // Returns a list of Canonical User Ids
        private async Task<List<string>> ResolveRecipientsAsync(ApplicationDbContext dbContext, NotificationRecipientGroup group, string relatedUsername, string relatedManagerUsername)
        {
            var userIds = new List<string>();

            if (group.Type == "AllEmployees")
            {
                var users = await dbContext.Users.Select(u => u.Username).ToListAsync();
                userIds.AddRange(users);
            }
            else if (group.Type == "SpecificRole")
            {
                string[] roles = group.TargetValues?.Split(',') ?? new string[0];
                foreach (var r in roles)
                {
                    var rTrim = r.Trim();
                    var users = await dbContext.Users.Where(u => u.Role == rTrim).Select(u => u.Username).ToListAsync();
                    userIds.AddRange(users);
                }
            }
            else if (group.Type == "DynamicEmployee")
            {
                if (!string.IsNullOrEmpty(relatedUsername)) userIds.Add(relatedUsername);
            }
            else if (group.Type == "DynamicManager")
            {
                if (!string.IsNullOrEmpty(relatedManagerUsername)) userIds.Add(relatedManagerUsername);
            }
            else if (group.Type == "DynamicHRAdmin")
            {
                var hrUsers = await dbContext.Users.Where(u => u.Role == "HR").Select(u => u.Username).ToListAsync();
                userIds.AddRange(hrUsers);
            }

            return userIds.Distinct().ToList();
        }

        private async Task<List<(string UserId, string Email)>> GetEmailsForCanonicalUsers(ApplicationDbContext dbContext, List<string> canonicalUserIds)
        {
            var users = await dbContext.Users
                .Where(u => canonicalUserIds.Contains(u.Username) && !string.IsNullOrEmpty(u.Email))
                .Select(u => new { u.Username, u.Email })
                .ToListAsync();

            return users.Select(u => (u.Username, u.Email)).ToList();
        }

        private string RenderTemplate(string template, object payload)
        {
            if (string.IsNullOrEmpty(template) || payload == null) return template;
            
            var result = template;
            var type = payload.GetType();
            foreach (var prop in type.GetProperties())
            {
                var val = prop.GetValue(payload)?.ToString() ?? "";
                result = result.Replace("{{" + prop.Name + "}}", val);
            }
            return result;
        }

        private async Task<bool> SendEmailAsync(SmtpSettings smtp, string toEmail, string subject, string body)
        {
            try
            {
                using var client = new SmtpClient(smtp.Host, smtp.Port)
                {
                    Credentials = new NetworkCredential(smtp.Username, smtp.Password),
                    EnableSsl = smtp.EnableSsl
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtp.SenderEmail, smtp.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);
                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to " + toEmail);
                return false;
            }
        }
    }
}
