using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Services;

/// <summary>Read-only dashboard queries. Scope always comes from the authenticated session's scope service.</summary>
public sealed class ManagerDashboardService(ApplicationDbContext context, IManagerScopeService scope)
{
    public async Task<ManagerWorkspaceViewModel> LoadAsync(User actor, int? year, string? unit)
    {
        var today = DateTime.Today;
        var units = scope.GetManagedOrganizationalUnits().OrderBy(x => x).ToList();
        var query = scope.GetScopedEmployeeQuery().AsNoTracking();
        // Unknown filter values fail closed, even if someone manually edits the URL.
        if (!string.IsNullOrWhiteSpace(unit))
            query = units.Contains(unit) ? query.Where(e => e.Department == unit) : query.Where(e => false);

        var employees = await EmployeeRows(query).ToListAsync();
        var ids = query.Select(e => e.Id);
        var approvable = query.Where(e => !actor.EmployeeId.HasValue || e.Id != actor.EmployeeId.Value);
        var approvableIds = approvable.Select(e => e.Id);
        var approvableCodes = approvable.Where(e => e.EmployeeCode != null).Select(e => e.EmployeeCode!);

        // Use JoinDate itself, never the NotMapped StartDate fallback or CreatedAt.
        var joins = await context.OnboardingRecords.AsNoTracking()
            .Where(o => o.EmployeeId.HasValue && ids.Contains(o.EmployeeId.Value)
                && o.JoinDate.HasValue && o.JoinDate.Value <= today
                && o.OnboardingStatus != "CANCELLED" && o.OnboardingStatus != "REJECTED")
            .Select(o => new { EmployeeId = o.EmployeeId!.Value, Date = o.JoinDate!.Value }).ToListAsync();
        var firstJoins = joins.GroupBy(o => o.EmployeeId).ToDictionary(g => g.Key, g => g.Min(o => o.Date));
        foreach (var employee in employees)
            employee.JoinDate = firstJoins.TryGetValue(employee.Id, out var joined) ? joined : null;

        // Historical series represents employees in today's authorized scope, not a reconstructed historical roster.
        var exits = await context.OffboardingRecords.AsNoTracking()
            .Where(o => ids.Contains(o.EmployeeId) && o.ApprovalStatus == "APPROVED"
                && o.CompletedAt.HasValue && o.EffectiveDate <= today)
            .Select(o => new { o.EmployeeId, Date = o.EffectiveDate }).Distinct().ToListAsync();
        var transfers = await context.TransferRecords.AsNoTracking()
            .Where(t => ids.Contains(t.EmployeeId) && t.AppliedAt.HasValue
                && (t.ApprovalStatus == "APPROVED" || t.ApprovalStatus == "EFFECTIVE") && t.EffectiveDate <= today)
            .Select(t => t.EffectiveDate).ToListAsync();
        var years = firstJoins.Values.Select(d => d.Year).Concat(exits.Select(d => d.Date.Year))
            .Concat(transfers.Select(d => d.Year)).Append(today.Year).Distinct().OrderByDescending(y => y).ToList();
        var selectedYear = year ?? today.Year;
        if (selectedYear < 1 || selectedYear > 9998) selectedYear = today.Year;
        if (!years.Contains(selectedYear)) years.Add(selectedYear);

        var model = new ManagerWorkspaceViewModel
        {
            Year = selectedYear, Unit = unit ?? "", AvailableYears = years.OrderByDescending(y => y).ToList(),
            OrganizationalUnits = units, ManagedEmployeeCount = employees.Count,
            WorkingEmployeeCount = employees.Count(e => e.Status == "Active" || e.Status == "Đang làm việc"),
            OnLeaveTodayCount = await context.Leaves.AsNoTracking()
                .Where(l => l.EmployeeId.HasValue && ids.Contains(l.EmployeeId.Value)
                    && (l.Status == "Đã duyệt" || l.Status == "APPROVED")
                    && l.FromDate < today.AddDays(1) && l.ToDate >= today)
                .Select(l => l.EmployeeId).Distinct().CountAsync(),
            AssignedLeavePendingCount = await context.Leaves.AsNoTracking()
                .CountAsync(l => l.EmployeeId.HasValue && approvableIds.Contains(l.EmployeeId.Value)
                    && (l.Status == "Chờ duyệt" || l.Status == "PENDING")),
            AssignedAdjustmentPendingCount = await context.AttendanceAdjustmentRequests.AsNoTracking()
                .CountAsync(a => approvableCodes.Contains(a.EmployeeCode) && (a.Status == "PENDING" || a.Status == "Chờ duyệt")),
            MissingJoinDateCount = employees.Count(e => e.JoinDate == null),
            RecentEmployees = employees.OrderByDescending(e => e.JoinDate.HasValue).ThenByDescending(e => e.JoinDate)
                .ThenBy(e => e.Name).ThenBy(e => e.Id).Take(5).ToList()
        };
        model.PositionGroups = employees.GroupBy(e => string.IsNullOrWhiteSpace(e.Position) ? "Chưa có thông tin vị trí" : e.Position.Trim())
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => new ManagerPositionGroup { Label = g.Key, Count = g.Count(), Percent = Math.Round(100m * g.Count() / employees.Count, 1) }).ToList();
        model.Movement = Enumerable.Range(1, 12).Select(month => new ManagerMovementMonth
        {
            Month = month,
            Joined = firstJoins.Values.Count(d => d.Year == selectedYear && d.Month == month),
            Left = exits.Count(d => d.Date.Year == selectedYear && d.Date.Month == month),
            Transferred = transfers.Count(d => d.Year == selectedYear && d.Month == month)
        }).ToList();
        return model;
    }

    public async Task<ManagerDashboardEmployee?> FindEmployeeAsync(int id)
    {
        var row = await EmployeeRows(scope.GetScopedEmployeeQuery().AsNoTracking().Where(e => e.Id == id)).SingleOrDefaultAsync();
        if (row == null) return null;
        row.JoinDate = await context.OnboardingRecords.AsNoTracking()
            .Where(o => o.EmployeeId == id && o.JoinDate.HasValue && o.JoinDate.Value <= DateTime.Today
                && o.OnboardingStatus != "CANCELLED" && o.OnboardingStatus != "REJECTED")
            .Select(o => o.JoinDate).MinAsync();
        return row;
    }

    private static IQueryable<ManagerDashboardEmployee> EmployeeRows(IQueryable<Employee> query) => query.Select(e => new ManagerDashboardEmployee
    {
        Id = e.Id, Name = e.Name, EmployeeCode = e.EmployeeCode, JobTitle = e.JobTitle,
        Position = e.Position, Department = e.Department, Status = e.Status
    });
}
