using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class NotificationRecipientGroup
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string GroupName { get; set; }
        
        [StringLength(255)]
        public string Description { get; set; }

        [Required]
        [StringLength(50)]
        public string Type { get; set; } // e.g. "AllEmployees", "Role", "Department", "SpecificUsers"
        
        // This will store IDs based on the Type. e.g. Role names, Department IDs, or User IDs, separated by commas.
        public string TargetValues { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }

    public class EmailTemplate
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string TemplateName { get; set; }
        
        [Required]
        [StringLength(255)]
        public string Subject { get; set; }
        
        [Required]
        public string Body { get; set; } // Support HTML and Variables like {{EmployeeName}}
        
        [StringLength(50)]
        public string EventCode { get; set; } // To link template to specific events (e.g. LeaveRequested)
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }

    public class NotificationRule
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string RuleName { get; set; }
        
        [StringLength(255)]
        public string Description { get; set; }
        
        [Required]
        [StringLength(50)]
        public string EventCode { get; set; } // e.g. "LeaveRequested", "ContractExpiring"
        
        [StringLength(50)]
        public string Module { get; set; } // e.g. "Leave", "Contract"
        
        public int? RecipientGroupId { get; set; }
        public NotificationRecipientGroup RecipientGroup { get; set; }
        
        public int? EmailTemplateId { get; set; }
        public EmailTemplate EmailTemplate { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Channel { get; set; } = "Email";
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
    
    public class NotificationLog
    {
        [Key]
        public int Id { get; set; }
        
        public int RuleId { get; set; }
        [ForeignKey("RuleId")]
        public NotificationRule? NotificationRule { get; set; }
        
        [StringLength(50)]
        public string EventCode { get; set; }
        
        [StringLength(100)]
        public string RecipientEmail { get; set; }
        
        [StringLength(50)]
        public string Status { get; set; } // "Sent", "Failed"
        
        public string FailureReason { get; set; }
        
        public DateTime SentAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string IdempotencyKey { get; set; }

        [StringLength(100)]
        public string TargetUserId { get; set; }
    }
}
