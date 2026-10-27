using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public sealed class LeaderExecutiveViewModel
    {
        public IReadOnlyList<string> ScopeDepartments { get; init; } = Array.Empty<string>();
        public int TotalScopedEmployees { get; init; }
        public int PresentToday { get; init; }
        public int ApprovedLeaveToday { get; init; }
        public int ExpiringContracts { get; init; }
        public int PendingLeaderDecisions { get; init; }
        public IReadOnlyList<LeaderDepartmentSummary> Departments { get; init; } = Array.Empty<LeaderDepartmentSummary>();
        public IReadOnlyList<LeaderAttentionItem> AttentionItems { get; init; } = Array.Empty<LeaderAttentionItem>();
    }

    public sealed class LeaderDepartmentSummary
    {
        public string Department { get; init; } = "Chưa phân bổ";
        public int EmployeeCount { get; init; }
        public int AttendanceRecords { get; init; }
        public int LateOccurrences { get; init; }
        public int ApprovedLeaveDays { get; init; }
    }

    public sealed class LeaderAttentionItem
    {
        public string Label { get; init; } = string.Empty;
        public int Count { get; init; }
        public string TargetUrl { get; init; } = string.Empty;
        public string BadgeClass { get; init; } = "hrms-badge-info";
    }

    public sealed class LeaderAnalyticsViewModel
    {
        public IReadOnlyList<string> ScopeDepartments { get; init; } = Array.Empty<string>();
        public string? SelectedDepartment { get; init; }
        public DateTime From { get; init; }
        public DateTime To { get; init; }
        public IReadOnlyList<LeaderDepartmentSummary> Departments { get; init; } = Array.Empty<LeaderDepartmentSummary>();
        public int ScopedEmployeeCount { get; init; }
        public string? ErrorMessage { get; init; }

        public IReadOnlyList<CorrelationResult> Correlations { get; init; } = Array.Empty<CorrelationResult>();
        public IReadOnlyList<AnomalyItem> Anomalies { get; init; } = Array.Empty<AnomalyItem>();
        public IReadOnlyList<ForecastItem> Forecasts { get; init; } = Array.Empty<ForecastItem>();
        public bool HasSufficientData { get; init; } = true;
    }

    public sealed class CorrelationResult
    {
        public string MetricA { get; init; } = string.Empty;
        public string MetricB { get; init; } = string.Empty;
        public double PearsonCoefficient { get; init; }
        public string Strength { get; init; } = string.Empty;
        public int SampleSize { get; init; }
    }

    public sealed class AnomalyItem
    {
        public string Department { get; init; } = string.Empty;
        public string Metric { get; init; } = string.Empty;
        public double ActualValue { get; init; }
        public double ExpectedValue { get; init; }
        public double DifferencePercent { get; init; }
        public double ZScore { get; init; }
        public string Severity { get; init; } = string.Empty;
    }

    public sealed class ForecastItem
    {
        public string Month { get; init; } = string.Empty;
        public double ActualPersonnelCost { get; init; }
        public double? EstimatedPersonnelCost { get; init; }
        public bool IsForecast { get; init; }
    }

    public sealed class LeaderReportViewModel
    {
        public IReadOnlyList<string> ScopeDepartments { get; init; } = Array.Empty<string>();
        public string ReportType { get; init; } = "workforce";
        public string? SelectedDepartment { get; init; }
        public string? Search { get; init; }
        public DateTime From { get; init; }
        public DateTime To { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; } = 25;
        public int TotalRows { get; init; }
        public IReadOnlyList<LeaderReportRow> Rows { get; init; } = Array.Empty<LeaderReportRow>();
        public string? ErrorMessage { get; init; }
    }

    public sealed class LeaderReportRow
    {
        public string? EmployeeCode { get; init; }
        public string? EmployeeName { get; init; }
        public string? Department { get; init; }
        public DateTime? Date { get; init; }
        public string Metric { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
    }
}
