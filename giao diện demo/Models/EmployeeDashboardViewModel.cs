using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models;

public sealed class EmployeeDashboardViewModel
{
    public string EmployeeName { get; init; } = string.Empty;
    public string EmployeeCode { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public DateTime Today { get; init; }

    public EmployeeDashboardAttendanceRow? TodayAttendance { get; init; }
    public int AttendanceCountThisMonth { get; init; }
    public int PendingLeaveCount { get; init; }
    public int ContractCount { get; init; }
    public int ContractsAwaitingSignature { get; init; }
    public int UnreadNotificationCount { get; init; }

    public int TrainingInProgressCount { get; init; }
    public int TrainingCompletedCount { get; init; }
    public int TrainingCertificateCount { get; init; }
    public int RewardCount { get; init; }
    public int ActiveBenefitCount { get; init; }

    public string? LatestKpiPeriod { get; init; }
    public string? LatestKpiStatus { get; init; }
    public decimal? LatestKpiScore { get; init; }

    public IReadOnlyList<EmployeeDashboardAttendanceRow> RecentAttendances { get; init; } =
        Array.Empty<EmployeeDashboardAttendanceRow>();
}

public sealed class EmployeeDashboardAttendanceRow
{
    public DateTime CheckIn { get; init; }
    public DateTime? CheckOut { get; init; }
}
