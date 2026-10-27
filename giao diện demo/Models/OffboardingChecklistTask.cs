using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class OffboardingChecklistTask
    {
        public int Id { get; set; }

        public int OffboardingRecordId { get; set; }
        public OffboardingRecord? OffboardingRecord { get; set; }

        public string TaskName { get; set; } = string.Empty;
        public bool IsMandatory { get; set; } = true;
        public string Status { get; set; } = "PENDING"; // PENDING, IN_PROGRESS, DONE, SKIPPED
        
        public DateTime? CompletedAt { get; set; }
        public string? CompletedBy { get; set; }
        
        public string? Notes { get; set; }
        public string? Assignee { get; set; }
        public string? Category { get; set; }
        public DateTime? Deadline { get; set; }

        // --- COMPUTED FIELDS USED BY UI (NOT MAPPED IN DB) ---
        [NotMapped]
        public string? AssigneeName 
        { 
            get => Assignee; 
            set => Assignee = value; 
        }

        [NotMapped]
        public int SortOrder { get; set; } = 0;

        [NotMapped]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public bool IsCompleted => Status == "DONE" || Status == "SKIPPED";

        [NotMapped]
        public string StatusLabel => Status switch
        {
            "PENDING"     => "Chưa làm",
            "IN_PROGRESS" => "Đang làm",
            "DONE"        => "Hoàn thành",
            "SKIPPED"     => "Bỏ qua",
            _             => Status
        };
    }
}
