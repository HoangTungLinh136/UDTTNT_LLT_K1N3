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

public sealed class LeaderMonitoringUiTests
{
    [Fact]
    public async Task LDRUI02_001_personnel_scope_and_invalid_department_fail_closed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = fixture.Personnel();
        var scoped = Assert.IsType<JsonResult>(controller.GetEmployees(page: 1, pageSize: 10));
        var response = Assert.IsType<LeaderPersonnelPageResponse<LeaderEmployeeDto>>(scoped.Value);
        Assert.Single(response.Items);
        Assert.Equal("IN", response.Items[0].EmployeeCode);
        Assert.IsType<ForbidResult>(controller.GetEmployees(department: "UNIT_B"));
        Assert.IsType<ForbidResult>(controller.GetEmployeeDetail(fixture.OutOfScopeId));
    }

    [Fact]
    public async Task LDRUI02_002_leave_uses_employee_fk_and_server_paging()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Leaves.AddRange(
            new Leave { EmployeeId = fixture.InScopeId, EmployeeNameSnapshot = "Wrong snapshot", Status = "Đã duyệt", FromDate = DateTime.Today, ToDate = DateTime.Today },
            new Leave { EmployeeId = fixture.OutOfScopeId, EmployeeNameSnapshot = "In scope", Status = "Đã duyệt", FromDate = DateTime.Today, ToDate = DateTime.Today });
        await fixture.Context.SaveChangesAsync();
        var result = Assert.IsType<ViewResult>(await fixture.Leave().Index(null!, "Đã duyệt", null!, 1, 10));
        var rows = Assert.IsAssignableFrom<IEnumerable<Leave>>(result.Model).ToList();
        Assert.Single(rows);
        Assert.Equal(fixture.InScopeId, rows[0].EmployeeId);
        Assert.IsType<ForbidResult>(await fixture.Leave().Index("UNIT_B", null!, null!, 1, 10));
    }

    [Fact]
    public async Task LDRUI02_003_contract_query_is_scoped_and_canonical()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Contracts.AddRange(
            new Contract { EmployeeId = fixture.InScopeId, Status = ContractStatusSemantics.Effective, StartDate = DateTime.Today.AddDays(-1), EndDate = DateTime.Today.AddDays(5) },
            new Contract { EmployeeId = fixture.OutOfScopeId, Status = ContractStatusSemantics.Effective, StartDate = DateTime.Today.AddDays(-1), EndDate = DateTime.Today.AddDays(5) });
        await fixture.Context.SaveChangesAsync();
        var result = Assert.IsType<ViewResult>(await fixture.Contract().Index(null!, ContractStatusSemantics.Effective, null!, 1, 10));
        var rows = Assert.IsAssignableFrom<IEnumerable<Contract>>(result.Model).ToList();
        Assert.Single(rows);
        Assert.Equal(fixture.InScopeId, rows[0].EmployeeId);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public int InScopeId { get; private set; }
        public int OutOfScopeId { get; private set; }
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
            return new Fixture(connection, context) { InScopeId = inside.Id, OutOfScopeId = outside.Id };
        }
        public LeaderPersonnelController Personnel() => new(Context, new StaticScope("UNIT_A")) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        public LeaderLeaveController Leave() => new(Context, new StaticScope("UNIT_A"));
        public LeaderContractController Contract() => new(Context, new StaticScope("UNIT_A"));
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
