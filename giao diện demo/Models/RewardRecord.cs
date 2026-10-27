using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class RewardRecord : IApprovableEntity
    {
        [Key]
        public int Id { get; set; }

        // --- ĐỐI TƯỢNG KHEN THƯỞNG ---
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        // Snapshot thông tin tại thời điểm tạo để giữ lịch sử
        public string? EmployeeCodeSnapshot { get; set; }
        public string? EmployeeNameSnapshot { get; set; }
        public string? DepartmentSnapshot { get; set; }
        public string? PositionSnapshot { get; set; }

        // --- THÔNG TIN KHEN THƯỞNG ---
        public string? RewardType { get; set; } // Loại khen thưởng (Thành tích, Cống hiến...)
        
        [Required]
        public string? Title { get; set; } // Tiêu đề
        
        [Required]
        public string? Achievement { get; set; } // Thành tích / Lý do

        public string? Description { get; set; } // Nội dung / Mô tả chi tiết

        public DateTime ProposedDate { get; set; } = DateTime.Now; // Ngày đề xuất
        public DateTime? EffectiveDate { get; set; } // Ngày hiệu lực / khen thưởng

        // --- THƯỞNG TIỀN ---
        public bool HasMonetaryReward { get; set; } // Có thưởng tiền hay không
        public double RewardAmount { get; set; } // Số tiền thưởng
        public string? PayrollPeriod { get; set; } // Kỳ lương áp dụng (vd: "T08/2026")
        
        // Trạng thái đã chuyển sang tính lương chưa (để đảm bảo Idempotency - chống cộng 2 lần)
        public bool IsPayrollSynced { get; set; } = false;
        public DateTime? PayrollSyncedAt { get; set; }
        
        // --- QUYẾT ĐỊNH KHEN THƯỞNG ---
        public string? DecisionNumber { get; set; }
        public string? DecisionDocumentUrl { get; set; }

        // --- PHÊ DUYỆT / TRẠNG THÁI ---
        public string ApprovalStatus { get; set; } = "DRAFT"; // DRAFT, PENDING_APPROVAL, APPROVED, REJECTED, CANCELLED

        public string? ProposedBy { get; set; }
        
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public string? RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectionReason { get; set; }

        public string? CancelledBy { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }

        // --- THÔNG TIN HỆ THỐNG ---
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public ICollection<RewardHistory> Histories { get; set; } = new List<RewardHistory>();

        // --- IApprovableEntity Implementation ---
        [NotMapped]
        public string? Status 
        { 
            get => ApprovalStatus; 
            set => ApprovalStatus = value ?? "DRAFT"; 
        }

        [NotMapped]
        public string? ReviewedBy 
        { 
            get => ApprovalStatus == "REJECTED" ? RejectedBy : ApprovedBy; 
            set 
            {
                if (ApprovalStatus == "REJECTED") RejectedBy = value;
                else ApprovedBy = value;
            }
        }

        [NotMapped]
        public DateTime? ReviewedAt 
        { 
            get => ApprovalStatus == "REJECTED" ? RejectedAt : ApprovedAt; 
            set 
            {
                if (ApprovalStatus == "REJECTED") RejectedAt = value;
                else ApprovedAt = value;
            }
        }

        public int GetId() => Id;
    }
}
