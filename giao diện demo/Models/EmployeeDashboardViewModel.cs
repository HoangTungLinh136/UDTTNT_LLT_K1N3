using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models;

public sealed class EmployeeDashboardViewModel
{
    public string EmployeeName { get; init; } = string.Empty;
    public string EmployeeCode { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string JobTitle { get; init; } = string.Empty;
    public string EmploymentStatus { get; init; } = "Đang làm việc";
    public string? AvatarPath { get; init; }
    public int StandardWorkDaysThisMonth { get; init; } = 26;
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

    public WorkShift? TodayShift { get; init; }

    public IReadOnlyList<EmployeeDashboardAttendanceRow> RecentAttendances { get; init; } =
        Array.Empty<EmployeeDashboardAttendanceRow>();

    public IReadOnlyList<EmployeeDashboardNotificationItem> RecentNotifications { get; init; } =
        Array.Empty<EmployeeDashboardNotificationItem>();
}

public sealed class EmployeeDashboardAttendanceRow
{
    public DateTime CheckIn { get; init; }
    public DateTime? CheckOut { get; init; }
    public string? StatusNote { get; init; }
}

public sealed class EmployeeDashboardNotificationItem
{
    public int Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string? NotificationType { get; init; }
    public bool IsRead { get; init; }
    public string? SenderName { get; init; }

    public string TimeAgoVi
    {
        get
        {
            var diff = DateTime.Now - CreatedAt;
            if (diff.TotalMinutes < 1) return "Vừa xong";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} phút trước";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} giờ trước";
            if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} ngày trước";
            return CreatedAt.ToString("dd/MM/yyyy");
        }
    }
}
