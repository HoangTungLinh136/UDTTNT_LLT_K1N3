using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class HrTask
    {
        public int Id { get; set; }
        
        public string? TaskCode { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        
        // Soft link to other modules (e.g. Hội nhập, Nghỉ việc)
        public string? RelatedModule { get; set; }
        public int? RelatedRecordId { get; set; }
        public string? RelatedRecordCode { get; set; }
        
        // Employee this task is about (if any)
        public int? RelatedEmployeeId { get; set; }
        [ForeignKey("RelatedEmployeeId")]
        public Employee? RelatedEmployee { get; set; }
        
        // Employee assigned to this task
        public int AssigneeEmployeeId { get; set; }
        [ForeignKey("AssigneeEmployeeId")]
        public Employee? AssigneeEmployee { get; set; }

        public string? AssigneeNameSnapshot { get; set; }
        
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime? StartDate { get; set; }
        public DateTime Deadline { get; set; }
        
        public string? Priority { get; set; } // LOW, MEDIUM, HIGH, URGENT
        public string? Status { get; set; } // OPEN, IN_PROGRESS, COMPLETED, CANCELLED
        
        public int ProgressPercent { get; set; } = 0;
        
        public bool ReminderEnabled { get; set; } = false;
        public int? ReminderBeforeValue { get; set; }
        public string? ReminderBeforeUnit { get; set; } // MINUTES, HOURS, DAYS
        
        public DateTime? CompletedAt { get; set; }
        public string? CompletedBy { get; set; }
        
        public DateTime? CancelledAt { get; set; }
        public string? CancelledBy { get; set; }
        public string? CancellationReason { get; set; }
        
        public string? Notes { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
