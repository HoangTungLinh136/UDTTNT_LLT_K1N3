using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    /// <summary>
    /// Công việc checklist hội nhập — tương ứng bảng OnboardingChecklistTasks
    /// </summary>
    public class OnboardingChecklistTask
    {
        public int Id { get; set; }

        public int OnboardingRecordId { get; set; }
        public OnboardingRecord? OnboardingRecord { get; set; }

        // --- COLUMNS FROM EXISTING DATABASE ---
        public string TaskName { get; set; } = string.Empty;
        public bool IsMandatory { get; set; } = true;
        public string Status { get; set; } = "PENDING";
        public DateTime? CompletedAt { get; set; }
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
        public bool IsCompleted => Status == "DONE";

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
