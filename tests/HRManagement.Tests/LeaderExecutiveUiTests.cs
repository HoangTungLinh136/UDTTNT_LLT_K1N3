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

public sealed class LeaderExecutiveUiTests
{
    [Fact]
    public async Task LDRUI_001_overview_uses_scoped_employee_fk_and_leader_decisions()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Attendances.Add(new Attendance { EmployeeId = fixture.InScopeId, CheckIn = DateTime.Today.AddHours(8) });
        fixture.Context.Leaves.Add(new Leave { EmployeeId = fixture.InScopeId, Status = "Đã duyệt", FromDate = DateTime.Today, ToDate = DateTime.Today });
        fixture.Context.AppointmentRecords.Add(new AppointmentRecord { EmployeeId = fixture.InScopeId, ApprovalStatus = "PENDING_LEADER_APPROVAL", PreviousDepartment = "UNIT_A", NewDepartment = "UNIT_B", EffectiveDate = DateTime.Today });
        await fixture.Context.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await fixture.Leader().Index());
        var model = Assert.IsType<LeaderExecutiveViewModel>(result.Model);
        Assert.Equal(1, model.TotalScopedEmployees);
        Assert.Equal(1, model.PresentToday);
        Assert.Equal(1, model.ApprovedLeaveToday);
        Assert.Equal(1, model.PendingLeaderDecisions);
    }

    [Fact]
    public async Task LDRUI_002_analytics_rejects_department_outside_leadership_scope()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Analytics().Index(null, null, "UNIT_B");
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task LDRUI_003_report_workforce_is_scoped_and_server_paged()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Employees.Add(new Employee { EmployeeCode = "OUT", Name = "Outside", Department = "UNIT_B", Status = "Active" });
        await fixture.Context.SaveChangesAsync();
        var result = Assert.IsType<ViewResult>(await fixture.Reports().Index(page: 1));
        var model = Assert.IsType<LeaderReportViewModel>(result.Model);
        Assert.Equal(1, model.TotalRows);
        Assert.Single(model.Rows);
        Assert.Equal("IN", model.Rows[0].EmployeeCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public int InScopeId { get; private set; }
        private Fixture(SqliteConnection connection, ApplicationDbContext context) { _connection = connection; Context = context; }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            var employee = new Employee { EmployeeCode = "IN", Name = "In scope", Department = "UNIT_A", Status = "Active" };
            context.Employees.Add(employee);
            await context.SaveChangesAsync();
            return new Fixture(connection, context) { InScopeId = employee.Id };
        }
        public LeaderController Leader() => new(Context, new StaticScope("UNIT_A")) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        public LeaderAnalyticsController Analytics() => new(Context, new StaticScope("UNIT_A"), new PayrollCalculationService(Context));
        public LeaderReportController Reports() => new(Context, new StaticScope("UNIT_A"), new PayrollCalculationService(Context));
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await _connection.DisposeAsync(); }
    }
    private sealed class StaticScope : ILeadershipScopeService
    {
        private readonly List<string> _units;
        public StaticScope(params string[] units) => _units = units.ToList();
        public List<string> GetOverseenOrganizationalUnits() => _units.ToList();
        public bool CanAccessDepartment(string? department) => department != null && _units.Contains(department);
        public bool HasLeadershipAccess() => _units.Count > 0;
        public IQueryable<Employee> GetScopedEmployeeQuery() => throw new NotSupportedException();
    }
}
