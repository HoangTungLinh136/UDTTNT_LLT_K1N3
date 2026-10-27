using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class PayrollValidationSummaryDto
    {
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public bool IsReadyForPayroll { get; set; }
        public int TotalIssues => ErrorCount + WarningCount;
        public List<PayrollValidationIssueDto> Issues { get; set; } = new List<PayrollValidationIssueDto>();
    }

    public class PayrollValidationIssueDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Severity { get; set; } // "ERROR", "WARNING"
        public string? EmployeeName { get; set; }
        public string? EmployeeCode { get; set; }
        public string Message { get; set; }
        public string SourceModule { get; set; } // "Hồ sơ nhân viên", "Chấm công", etc.
        public string Component { get; set; } // "Mức lương cơ bản", "CheckOut", "Mã số thuế"
        public string? CurrentValue { get; set; }
        public string? ExpectedValue { get; set; }
        public string ResolutionHint { get; set; }
        public string TargetUrl { get; set; }
    }
}
