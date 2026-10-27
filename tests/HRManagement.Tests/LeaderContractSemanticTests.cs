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

public sealed class LeaderContractSemanticTests
{
    [Fact]
    public async Task LDRCON_001_leader_contract_filter_uses_canonical_stored_status()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddContract(fixture.InScopeEmployeeId, ContractStatusSemantics.Effective, DateTime.Today.AddDays(10));
        fixture.AddContract(fixture.InScopeEmployeeId, "Active", DateTime.Today.AddDays(10));
        await fixture.Context.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await fixture.CreateLeaderContractController()
            .Index("UNIT_A", ContractStatusSemantics.Effective));
        var contracts = Assert.IsAssignableFrom<IEnumerable<Contract>>(result.Model).ToList();

        Assert.Single(contracts);
        Assert.Equal(ContractStatusSemantics.Effective, contracts[0].Status);
    }

    [Fact]
    public async Task LDRCON_002_leader_dashboard_expiring_metric_uses_effective_status_and_window()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddContract(fixture.InScopeEmployeeId, ContractStatusSemantics.Effective, DateTime.Today.AddDays(10));
        fixture.AddContract(fixture.InScopeEmployeeId, "Active", DateTime.Today.AddDays(10));
        fixture.AddContract(fixture.InScopeEmployeeId, ContractStatusSemantics.Effective, DateTime.Today.AddDays(-1));
        fixture.AddContract(fixture.InScopeEmployeeId, ContractStatusSemantics.Effective, DateTime.Today.AddDays(31));
        fixture.AddContract(fixture.InScopeEmployeeId, ContractStatusSemantics.Effective, DateTime.Today.AddDays(10), startDate: DateTime.Today.AddDays(1));
        await fixture.Context.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await fixture.CreateLeaderController().Index());

        Assert.Equal(1, result.ViewData["ExpiringContracts"]);
    }

    [Fact]
    public async Task LDRCON_003_leader_scope_supports_multiple_units_and_fails_closed_when_empty()
    {
        await using var fixture = await Fixture.CreateAsync();
        var second = new Employee { EmployeeCode = "NV-B", Name = "Second", Department = "UNIT_B", Status = "Active" };
        fixture.Context.Employees.Add(second);
        await fixture.Context.SaveChangesAsync();
        fixture.AddContract(fixture.InScopeEmployeeId, ContractStatusSemantics.Effective, DateTime.Today.AddDays(10));
        fixture.AddContract(second.Id, ContractStatusSemantics.Effective, DateTime.Today.AddDays(10));
        await fixture.Context.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await fixture.CreateLeaderContractController(new[] { "UNIT_A", "UNIT_B" })
            .Index(null!, null!));
        Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<Contract>>(result.Model).Count());

        var empty = Assert.IsType<ViewResult>(await fixture.CreateLeaderContractController(Array.Empty<string>()).Index(null!, null!));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<Contract>>(empty.Model));
    }

    [Fact]
    public async Task LDRCON_004_contract_scope_uses_employee_fk_not_snapshot_name()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddContract(fixture.InScopeEmployeeId, ContractStatusSemantics.Effective, DateTime.Today.AddDays(10), "Name from another unit");
        await fixture.Context.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await fixture.CreateLeaderContractController().Index(null!, null!));
        var contract = Assert.Single(Assert.IsAssignableFrom<IEnumerable<Contract>>(result.Model));

        Assert.Equal(fixture.InScopeEmployeeId, contract.EmployeeId);
        Assert.Equal("UNIT_A", contract.Employee!.Department);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public int InScopeEmployeeId { get; private set; }

        private Fixture(SqliteConnection connection, ApplicationDbContext context) => (_connection, Context) = (connection, context);

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            var employee = new Employee { EmployeeCode = "NV-A", Name = "Employee A", Department = "UNIT_A", Status = "Active" };
            context.Employees.Add(employee);
            await context.SaveChangesAsync();
            return new Fixture(connection, context) { InScopeEmployeeId = employee.Id };
        }

        public void AddContract(int employeeId, string status, DateTime endDate, string? snapshot = null, DateTime? startDate = null)
        {
            Context.Contracts.Add(new Contract
            {
                EmployeeId = employeeId,
                EmployeeNameSnapshot = snapshot ?? "Employee snapshot",
                Status = status,
                StartDate = startDate ?? DateTime.Today.AddMonths(-1),
                EndDate = endDate,
                Duration = "12 tháng"
            });
        }

        public LeaderContractController CreateLeaderContractController(IEnumerable<string>? departments = null)
        {
            var scope = new StaticLeadershipScope(departments ?? new[] { "UNIT_A" });
            return new LeaderContractController(Context, scope)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public LeaderController CreateLeaderController()
        {
            return new LeaderController(Context, new StaticLeadershipScope(new[] { "UNIT_A" }))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class StaticLeadershipScope : ILeadershipScopeService
    {
        private readonly List<string> _departments;
        public StaticLeadershipScope(IEnumerable<string> departments) => _departments = departments.ToList();
        public List<string> GetOverseenOrganizationalUnits() => _departments.ToList();
        public bool CanAccessDepartment(string? department) => department != null && _departments.Contains(department);
        public bool HasLeadershipAccess() => _departments.Count > 0;
        public IQueryable<Employee> GetScopedEmployeeQuery() => throw new NotSupportedException();
    }
}
