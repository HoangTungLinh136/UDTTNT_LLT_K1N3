using System;

namespace giao_dien_demo.Models
{
    public interface IApprovableEntity
    {
        string? Status { get; set; }
        string? ReviewedBy { get; set; }
        DateTime? ReviewedAt { get; set; }
        string? RejectionReason { get; set; }
        int GetId();
    }
}
