using System.Collections.Generic;
using giao_dien_demo.Models;

namespace giao_dien_demo.Models
{
    public class AdminNotificationConfigViewModel
    {
        public string ActiveTab { get; set; } = "rules";
        public bool IsEmailConfigured { get; set; }
        
        public List<NotificationRule> Rules { get; set; } = new List<NotificationRule>();
        public List<NotificationRecipientGroup> RecipientGroups { get; set; } = new List<NotificationRecipientGroup>();
        public List<EmailTemplate> EmailTemplates { get; set; } = new List<EmailTemplate>();
    }
}
