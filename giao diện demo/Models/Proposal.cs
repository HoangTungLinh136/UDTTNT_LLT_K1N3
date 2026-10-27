using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    public class Proposal : IApprovableEntity
    {
        public int Id { get; set; }
        
        public string? ProposalCode { get; set; }
        
        public string? Title { get; set; }
        public string? ProposalType { get; set; } // APPOINTMENT, TRANSFER, REWARD, SALARY_ADJUSTMENT, OTHER
        
        public string? CurrentSituation { get; set; }
        public string? ProposalContent { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        
        // Dynamic Fields
        public int? RelatedEmployeeId { get; set; }
        [ForeignKey("RelatedEmployeeId")]
        public Employee? RelatedEmployee { get; set; }

        public string? RelatedEmployeeNameSnapshot { get; set; }
        public string? RelatedDepartment { get; set; }
        public int? TargetPositionId { get; set; }
        public string? TargetPositionSnapshot { get; set; }
        public decimal? EstimatedCost { get; set; }
        public DateTime? ProposedEffectiveDate { get; set; }
        
        public string? Status { get; set; } // DRAFT, PENDING_APPROVAL, NEEDS_REVISION, APPROVED, REJECTED, CANCELLED
        
        public int CurrentApprovalLevel { get; set; } = 1;
        public int TotalApprovalLevels { get; set; } = 1;
        
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAt { get; set; }
        
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        
        public string? RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectionReason { get; set; }
        
        public string? RevisionRequestedBy { get; set; }
        public DateTime? RevisionRequestedAt { get; set; }
        public string? RevisionRequestReason { get; set; }
        
        public string? CancelledBy { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        
        public int? SourceProposalId { get; set; }
        [ForeignKey("SourceProposalId")]
        public Proposal? SourceProposal { get; set; }
        
        // Relational reference after approved
        public string? GeneratedBusinessType { get; set; } // e.g. "TRANSFER"
        public int? GeneratedBusinessRecordId { get; set; } // e.g. TransferRecord.Id

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        [NotMapped]
        public string? ReviewedBy
        {
            get => Status == "REJECTED" ? RejectedBy : Status == "NEEDS_REVISION" ? RevisionRequestedBy : ApprovedBy;
            set
            {
                if (Status == "REJECTED") RejectedBy = value;
                else if (Status == "NEEDS_REVISION") RevisionRequestedBy = value;
                else ApprovedBy = value;
            }
        }

        [NotMapped]
        public DateTime? ReviewedAt
        {
            get => Status == "REJECTED" ? RejectedAt : Status == "NEEDS_REVISION" ? RevisionRequestedAt : ApprovedAt;
            set
            {
                if (Status == "REJECTED") RejectedAt = value;
                else if (Status == "NEEDS_REVISION") RevisionRequestedAt = value;
                else ApprovedAt = value;
            }
        }

        public int GetId() => Id;
    }
}
