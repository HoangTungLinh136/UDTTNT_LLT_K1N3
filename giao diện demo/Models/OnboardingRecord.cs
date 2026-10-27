using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    /// <summary>
    /// Hồ sơ hội nhập — tương ứng bảng OnboardingRecords trong database
    /// </summary>
    public class OnboardingRecord : IApprovableEntity
    {
        public int Id { get; set; }

        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        // --- COLUMNS FROM EXISTING DATABASE ---
        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public string? Department { get; set; }
        public string? JobTitle { get; set; }
        public DateTime? JoinDate { get; set; }
        public string? DocumentStatus { get; set; }
        public string? OnboardingStatus { get; set; }
        public string? ResponsiblePerson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string? Address { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? DirectManager { get; set; }
        public string? Email { get; set; }
        public DateTime? EvaluationDate { get; set; }
        public string? EvaluationNotes { get; set; }
        public string? EvaluationResult { get; set; }
        public string? Evaluator { get; set; }
        public DateTime? ExpectedCompletionDate { get; set; }
        public string? Gender { get; set; }
        public string? Notes { get; set; }
        public string? PhoneNumber { get; set; }

        // --- NAVIGATION ---
        public ICollection<OnboardingChecklistTask> ChecklistTasks { get; set; }
            = new List<OnboardingChecklistTask>();

        // --- COMPUTED FIELDS USED BY UI (NOT MAPPED IN DB) ---
        [NotMapped]
        public string? Status 
        { 
            get => OnboardingStatus ?? "PENDING"; 
            set => OnboardingStatus = value; 
        }
        public int GetId() => Id;
        
        [NotMapped] public string? ReviewedBy { get => Evaluator; set => Evaluator = value; }
        [NotMapped] public DateTime? ReviewedAt { get => EvaluationDate; set => EvaluationDate = value; }
        [NotMapped] public string? RejectionReason { get => EvaluationNotes; set => EvaluationNotes = value; }

        [NotMapped]
        public DateTime StartDate 
        { 
            get => JoinDate ?? DateTime.Today; 
            set => JoinDate = value; 
        }

        [NotMapped]
        public DateTime? ExpectedEndDate 
        { 
            get => ExpectedCompletionDate; 
            set => ExpectedCompletionDate = value; 
        }

        [NotMapped]
        public DateTime? ActualEndDate 
        { 
            get => EvaluationDate; 
            set => EvaluationDate = value; 
        }

        [NotMapped]
        public string? AssignedToName 
        { 
            get => ResponsiblePerson; 
            set => ResponsiblePerson = value; 
        }

        [NotMapped]
        public int ProgressPercent { get; set; } = 0;

        [NotMapped]
        public string StatusLabel => Status switch
        {
            "PENDING"     => "Chờ bắt đầu",
            "IN_PROGRESS" => "Đang hội nhập",
            "COMPLETED"   => "Hoàn thành",
            "CANCELLED"   => "Đã hủy",
            _             => Status
        };

        [NotMapped]
        public string StatusBadgeClass => Status switch
        {
            "PENDING"     => "badge-ob-pending",
            "IN_PROGRESS" => "badge-ob-inprogress",
            "COMPLETED"   => "badge-ob-completed",
            "CANCELLED"   => "badge-ob-cancelled",
            _             => "badge-ob-pending"
        };
    }
}
