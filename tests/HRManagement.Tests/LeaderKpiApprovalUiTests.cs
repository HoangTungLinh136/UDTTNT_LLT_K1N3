using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using giao_dien_demo.Controllers;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRManagement.Tests;

public sealed class LeaderKpiApprovalUiTests
{
    [Fact]
    public async Task LDRUI03_001_kpi_scope_search_and_pagination_are_server_enforced()
    {
        await using var fixture = await Fixture.CreateAsync();
        var period = new KPIPeriod { Name = "Q1", StartDate = DateTime.Today.AddMonths(-3), EndDate = DateTime.Today, Status = "CLOSED" };
        fixture.Context.KPIPeriods.Add(period);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.KPIEvaluations.AddRange(
            new KPIEvaluation { PeriodId = period.Id, EmployeeId = fixture.InScopeId, Status = "COMPLETED", FinalScore = 90, FinalRating = "A", CompletedAt = DateTime.Today },
            new KPIEvaluation { PeriodId = period.Id, EmployeeId = fixture.OutScopeId, Status = "COMPLETED", FinalScore = 50, FinalRating = "C", CompletedAt = DateTime.Today });
        await fixture.Context.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await fixture.Kpi().Index(null, period.Id, "IN", 1, 1));
        var model = Assert.IsType<LeaderKPIViewModel>(result.Model);
        Assert.Equal(1, model.TotalEvaluations);
        Assert.Single(model.CompletedEvaluations);
        Assert.Equal("IN", model.CompletedEvaluations[0].EmployeeCode);
        Assert.IsType<ForbidResult>(await fixture.Kpi().Index("UNIT_B", null, null, 1, 25));
    }

    [Fact]
    public async Task LDRUI03_002_approval_includes_proposal_and_excludes_leave_and_adjustment()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Proposals.Add(new Proposal { ProposalCode = "PR-1", Title = "Chủ trương", Status = "PENDING_APPROVAL", RelatedEmployeeId = fixture.InScopeId, SubmittedAt = DateTime.Today });
        fixture.Context.Leaves.Add(new Leave { EmployeeId = fixture.InScopeId, Status = "Chờ duyệt", FromDate = DateTime.Today, ToDate = DateTime.Today });
        var attendance = new Attendance { EmployeeId = fixture.InScopeId, CheckIn = DateTime.Today };
        fixture.Context.Attendances.Add(attendance);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.AttendanceAdjustmentRequests.Add(new AttendanceAdjustmentRequest { AttendanceId = attendance.Id, EmployeeCode = "IN", Status = "PENDING", CreatedAt = DateTime.Today });
        await fixture.Context.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await fixture.Approval().Index(null));
        var model = Assert.IsType<LeaderApprovalCenterViewModel>(result.Model);
        Assert.Equal(1, model.ProposalPendingCount);
        Assert.Single(model.PendingItems);
        Assert.Equal("PROPOSAL", model.PendingItems[0].WorkflowType);
    }

    [Fact]
    public void LDRUI03_003_total_pending_count_includes_proposal()
    {
        var model = new LeaderApprovalCenterViewModel { AppointmentPendingCount = 2, DismissalPendingCount = 1, TransferPendingCount = 1, RewardPendingCount = 2, ProposalPendingCount = 3 };
        Assert.Equal(9, model.TotalPendingCount);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public int InScopeId { get; private set; }
        public int OutScopeId { get; private set; }
        private Fixture(SqliteConnection connection, ApplicationDbContext context) { _connection = connection; Context = context; }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            var inside = new Employee { EmployeeCode = "IN", Name = "Inside", Department = "UNIT_A", Status = "Active" };
            var outside = new Employee { EmployeeCode = "OUT", Name = "Outside", Department = "UNIT_B", Status = "Active" };
            context.Employees.AddRange(inside, outside);
            await context.SaveChangesAsync();
            return new Fixture(connection, context) { InScopeId = inside.Id, OutScopeId = outside.Id };
        }
        public LeaderKPIController Kpi() => new(Context, new StaticScope("UNIT_A")) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        public LeaderApprovalController Approval() => new(Context, new StaticScope("UNIT_A")) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await _connection.DisposeAsync(); }
    }
    private sealed class StaticScope : ILeadershipScopeService
    {
        private readonly List<string> _units;
        public StaticScope(params string[] units) => _units = units.ToList();
        public List<string> GetOverseenOrganizationalUnits() => _units.ToList();
        public bool CanAccessDepartment(string? department) => department != null && _units.Contains(department);
        public bool HasLeadershipAccess() => _units.Count > 0;
        public IQueryable<Employee> GetScopedEmployeeQuery() => Contextless();
        private static IQueryable<Employee> Contextless() => throw new NotSupportedException();
    }
}
