using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class MyRequestViewModel
    {
        public int TotalRequests { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }

        public string SearchKeyword { get; set; } = string.Empty;
        public string FilterType { get; set; } = string.Empty;
        public string FilterStatus { get; set; } = string.Empty;

        public List<MyRequestItemDto> Items { get; set; } = new List<MyRequestItemDto>();
    }

    public class MyRequestItemDto
    {
        public string RequestItemId { get; set; } = string.Empty; // e.g. "LEAVE_12"
        public int RequestId { get; set; }
        public string RequestType { get; set; } = string.Empty; // "Nghỉ phép", "Giải trình công"
        public string RequestTypeKey { get; set; } = string.Empty; // "LEAVE", "ADJUSTMENT"
        public string RequestCode { get; set; } = string.Empty; // "NP0012"
        public string DurationSummary { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string RejectionReason { get; set; } = string.Empty;
    }
}
