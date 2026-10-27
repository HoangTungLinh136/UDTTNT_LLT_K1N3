using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Services;
using giao_dien_demo.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using giao_dien_demo.Data;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AdminNotificationConfigController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationConfigService _notificationService;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;

        public AdminNotificationConfigController(ApplicationDbContext context, NotificationConfigService notificationService, giao_dien_demo.Services.IAuditLogService auditLogService)
        {
            _context = context;
            _notificationService = notificationService;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public IActionResult Index(string tab = "rules")
        {
            var settings = _notificationService.LoadSettings();
            bool isEmailConfigured = !string.IsNullOrEmpty(settings?.Smtp?.Host) && !string.IsNullOrEmpty(settings?.Smtp?.Username);

            var vm = new AdminNotificationConfigViewModel
            {
                ActiveTab = tab,
                IsEmailConfigured = isEmailConfigured,
                Rules = _context.NotificationRules.Include(r => r.RecipientGroup).Include(r => r.EmailTemplate).ToList(),
                RecipientGroups = _context.NotificationRecipientGroups.ToList(),
                EmailTemplates = _context.EmailTemplates.ToList()
            };

            return View(vm);
        }

        // --- RULES ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveRule(NotificationRule rule)
        {
            if (rule.Id == 0)
            {
                rule.CreatedAt = DateTime.Now;
                _context.NotificationRules.Add(rule);
            }
            else
            {
                var existing = _context.NotificationRules.FirstOrDefault(r => r.Id == rule.Id);
                if (existing != null)
                {
                    existing.RuleName = rule.RuleName;
                    existing.EventCode = rule.EventCode;
                    existing.Module = rule.Module;
                    existing.RecipientGroupId = rule.RecipientGroupId;
                    existing.EmailTemplateId = rule.EmailTemplateId;
                    existing.Channel = rule.Channel;
                    existing.IsActive = rule.IsActive;
                    existing.UpdatedAt = DateTime.Now;
                }
            }

            _context.SaveChanges();
            _ = _auditLogService.LogAsync("UpdateNotificationConfig", "Notification", "NotificationRule", rule.Id.ToString(), "DataChange", "Success", null, null, System.Text.Json.JsonSerializer.Serialize(rule));

            TempData["Success"] = "Đã lưu quy tắc thông báo thành công.";
            return RedirectToAction("Index", new { tab = "rules" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteRule(int id)
        {
            var rule = _context.NotificationRules.FirstOrDefault(r => r.Id == id);
            if (rule != null)
            {
                _context.NotificationRules.Remove(rule);
                _context.SaveChanges();
                _ = _auditLogService.LogAsync("DeleteNotificationConfig", "Notification", "NotificationRule", id.ToString(), "DataChange", "Success", null, System.Text.Json.JsonSerializer.Serialize(rule), null);
                TempData["Success"] = "Đã xóa quy tắc thông báo.";
            }

            return RedirectToAction("Index", new { tab = "rules" });
        }

        // --- RECIPIENT GROUPS ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveRecipientGroup(NotificationRecipientGroup group)
        {
            if (group.Id == 0)
            {
                group.CreatedAt = DateTime.Now;
                _context.NotificationRecipientGroups.Add(group);
            }
            else
            {
                var existing = _context.NotificationRecipientGroups.FirstOrDefault(g => g.Id == group.Id);
                if (existing != null)
                {
                    existing.GroupName = group.GroupName;
                    existing.Description = group.Description;
                    existing.Type = group.Type;
                    existing.TargetValues = group.TargetValues;
                    existing.IsActive = group.IsActive;
                    existing.UpdatedAt = DateTime.Now;
                }
            }

            _context.SaveChanges();
            _ = _auditLogService.LogAsync("UpdateNotificationConfig", "Notification", "NotificationRecipientGroup", group.Id.ToString(), "DataChange", "Success", null, null, System.Text.Json.JsonSerializer.Serialize(group));

            TempData["Success"] = "Đã lưu Nhóm người nhận thành công.";
            return RedirectToAction("Index", new { tab = "recipients" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteRecipientGroup(int id)
        {
            bool isInUse = _context.NotificationRules.Any(r => r.RecipientGroupId == id);
            if (isInUse)
            {
                TempData["Error"] = "Không thể xóa nhóm này vì đang được sử dụng trong một hoặc nhiều Quy tắc.";
                return RedirectToAction("Index", new { tab = "recipients" });
            }

            var group = _context.NotificationRecipientGroups.FirstOrDefault(g => g.Id == id);
            if (group != null)
            {
                _context.NotificationRecipientGroups.Remove(group);
                _context.SaveChanges();
                _ = _auditLogService.LogAsync("DeleteNotificationConfig", "Notification", "NotificationRecipientGroup", id.ToString(), "DataChange", "Success", null, System.Text.Json.JsonSerializer.Serialize(group), null);
                TempData["Success"] = "Đã xóa Nhóm người nhận.";
            }

            return RedirectToAction("Index", new { tab = "recipients" });
        }

        // --- EMAIL TEMPLATES ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveEmailTemplate(EmailTemplate template)
        {
            if (template.Id == 0)
            {
                template.CreatedAt = DateTime.Now;
                _context.EmailTemplates.Add(template);
            }
            else
            {
                var existing = _context.EmailTemplates.FirstOrDefault(t => t.Id == template.Id);
                if (existing != null)
                {
                    existing.TemplateName = template.TemplateName;
                    existing.Subject = template.Subject;
                    existing.Body = template.Body;
                    existing.EventCode = template.EventCode;
                    existing.IsActive = template.IsActive;
                    existing.UpdatedAt = DateTime.Now;
                }
            }

            _context.SaveChanges();
            _ = _auditLogService.LogAsync("UpdateNotificationConfig", "Notification", "EmailTemplate", template.Id.ToString(), "DataChange", "Success", null, null, System.Text.Json.JsonSerializer.Serialize(template));
            TempData["Success"] = "Đã lưu Email Template thành công.";
            return RedirectToAction("Index", new { tab = "templates" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteEmailTemplate(int id)
        {
            bool isInUse = _context.NotificationRules.Any(r => r.EmailTemplateId == id);
            if (isInUse)
            {
                TempData["Error"] = "Không thể xóa Template này vì đang được sử dụng trong Quy tắc.";
                return RedirectToAction("Index", new { tab = "templates" });
            }

            var template = _context.EmailTemplates.FirstOrDefault(t => t.Id == id);
            if (template != null)
            {
                _context.EmailTemplates.Remove(template);
                _context.SaveChanges();
                _ = _auditLogService.LogAsync("DeleteNotificationConfig", "Notification", "EmailTemplate", id.ToString(), "DataChange", "Success", null, System.Text.Json.JsonSerializer.Serialize(template), null);
                TempData["Success"] = "Đã xóa Email Template.";
            }

            return RedirectToAction("Index", new { tab = "templates" });
        }
    }
}
