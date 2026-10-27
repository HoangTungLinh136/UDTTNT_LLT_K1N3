using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class PendingApprovalViewModel
    {
        public int TotalPending { get; set; }
        public int LeavePending { get; set; }
        public int AdjustmentPending { get; set; }
        public int ProposalPending { get; set; }

        public string? SearchKeyword { get; set; }
        public string? FilterType { get; set; }
        public string? FilterStatus { get; set; }

        public List<PendingApprovalItemDto> Items { get; set; } = new List<PendingApprovalItemDto>();
    }

    public class PendingApprovalItemDto
    {
        public string ApprovalItemId { get; set; } = ""; // e.g. "LEAVE_1", "ADJ_2"
        public int RequestId { get; set; }
        public string RequestType { get; set; } = ""; // "Nghỉ phép", "Điều chỉnh công", "Đề xuất"
        public string RequestTypeKey { get; set; } = ""; // "LEAVE", "ADJUSTMENT", "PROPOSAL"
        public string RequestCode { get; set; } = ""; 
        public string EmployeeName { get; set; } = "";
        public string EmployeeCode { get; set; } = "";
        public string DurationSummary { get; set; } = ""; 
        public string Status { get; set; } = ""; // Normalized to "Chờ duyệt" or "Yêu cầu bổ sung"
        public DateTime SubmittedAt { get; set; }
    }

    public class ApprovalDetailDto
    {
        public string ApprovalItemId { get; set; } = "";
        public string RequestTypeKey { get; set; } = "";
        public string RequestCode { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string EmployeeCode { get; set; } = "";
        public string Department { get; set; } = "";
        public string JobTitle { get; set; } = "";
        
        public string Status { get; set; } = "";
        public DateTime SubmittedAt { get; set; }

        // Dynamic content
        public Dictionary<string, string> RequestContent { get; set; } = new Dictionary<string, string>();
    }
}
