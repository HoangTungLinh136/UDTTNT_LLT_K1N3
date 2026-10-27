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

public sealed class LeaderCanonicalIdentityTests
{
    [Fact]
    public async Task LDRJOIN_001_dashboard_metrics_use_employee_fk_for_same_name_records()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var result = Assert.IsType<ViewResult>(await fixture.CreateLeaderController().Index());

        Assert.Equal(1, result.ViewData["ActiveLeaves"]);
        Assert.Equal(125m, Convert.ToDecimal(result.ViewData["TotalCost"]));
        Assert.Equal(1, result.ViewData["ExpiringContracts"]);
    }

    [Fact]
    public async Task LDRJOIN_002_leader_leave_uses_employee_id_and_excludes_null_fk()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var result = Assert.IsType<ViewResult>(await fixture.CreateLeaderLeaveController().Index(null, null));
        var leaves = Assert.IsAssignableFrom<IEnumerable<Leave>>(result.Model).ToList();

        Assert.Equal(2, leaves.Count);
        Assert.All(leaves, leave => Assert.Equal(fixture.InScopeEmployeeId, leave.EmployeeId));
    }

    [Fact]
    public async Task LDRJOIN_003_attendance_leave_linkage_ignores_snapshot_name()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var month = DateTime.Now.ToString("MM/yyyy");
        var result = Assert.IsType<ViewResult>(fixture.CreateLeaderAttendanceController().Index(month));
        var model = Assert.IsType<AttendanceReportViewModel>(result.Model);

        var employee = Assert.Single(model.EmployeeDetails);
        Assert.Equal(fixture.InScopeEmployeeId, employee.EmployeeId);
        Assert.True(employee.LeaveDays >= 1d);
    }

    [Fact]
    public async Task LDRJOIN_004_renaming_employee_does_not_break_fk_association()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var employee = await fixture.Context.Employees.SingleAsync(e => e.Id == fixture.InScopeEmployeeId);
        employee.Name = "Tên mới";
        await fixture.Context.SaveChangesAsync();

        var leaveOwner = await fixture.Context.Leaves
            .Where(l => l.EmployeeId == fixture.InScopeEmployeeId)
            .Select(l => l.EmployeeId)
            .FirstAsync();
        var contractOwner = await fixture.Context.Contracts
            .Where(c => c.EmployeeId == fixture.InScopeEmployeeId)
            .Select(c => c.EmployeeId)
            .FirstAsync();

        Assert.Equal(fixture.InScopeEmployeeId, leaveOwner);
        Assert.Equal(fixture.InScopeEmployeeId, contractOwner);
    }

    [Fact]
    public async Task LDRJOIN_005_leader_scope_and_multi_unit_fail_closed_are_preserved()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var scopedIds = await fixture.Context.Employees
            .Where(e => fixture.Scope.GetOverseenOrganizationalUnits().Contains(e.Department))
            .Select(e => e.Id)
            .ToListAsync();

        Assert.Equal(new[] { fixture.InScopeEmployeeId }, scopedIds);

        var emptyScope = new StaticLeadershipScope(Array.Empty<string>());
        var emptyResult = Assert.IsType<ViewResult>(await new LeaderController(fixture.Context, emptyScope).Index());
        Assert.Equal(0, emptyResult.ViewData["ActiveLeaves"]);
        Assert.Equal(0m, Convert.ToDecimal(emptyResult.ViewData["TotalCost"]));
    }

    private sealed class IdentityFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public StaticLeadershipScope Scope { get; }
        public int InScopeEmployeeId { get; private set; }

        private IdentityFixture(SqliteConnection connection, ApplicationDbContext context, StaticLeadershipScope scope)
        {
            _connection = connection;
            Context = context;
            Scope = scope;
        }

        public static async Task<IdentityFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var inScope = new Employee { EmployeeCode = "E_A", Name = "Trùng Tên", Department = "UNIT_A", Status = "Active" };
            var outOfScope = new Employee { EmployeeCode = "E_B", Name = "Trùng Tên", Department = "UNIT_B", Status = "Active" };
            context.Employees.AddRange(inScope, outOfScope);
            await context.SaveChangesAsync();

            var now = DateTime.Now;
            var leaveDate = FirstWeekday(now.Year, now.Month);
            context.Leaves.AddRange(
                new Leave { EmployeeId = inScope.Id, EmployeeNameSnapshot = outOfScope.Name, Status = "Đã duyệt", FromDate = now.Date, ToDate = now.Date.AddDays(1).AddTicks(-1) },
                new Leave { EmployeeId = outOfScope.Id, EmployeeNameSnapshot = inScope.Name, Status = "Đã duyệt", FromDate = leaveDate, ToDate = leaveDate },
                new Leave { EmployeeId = null, EmployeeNameSnapshot = inScope.Name, Status = "Đã duyệt", FromDate = leaveDate, ToDate = leaveDate });
            if (leaveDate.Date != now.Date)
            {
                context.Leaves.Add(new Leave
                {
                    EmployeeId = inScope.Id,
                    EmployeeNameSnapshot = outOfScope.Name,
                    Status = "Đã duyệt",
                    FromDate = leaveDate,
                    ToDate = leaveDate
                });
            }

            var salaryMonth = new DateTime(now.Year, now.Month, 1);
            context.Salaries.AddRange(
                new Salary { EmployeeId = inScope.Id, EmployeeNameSnapshot = outOfScope.Name, SalaryMonth = salaryMonth, TotalSalary = 125 },
                new Salary { EmployeeId = outOfScope.Id, EmployeeNameSnapshot = inScope.Name, SalaryMonth = salaryMonth, TotalSalary = 999 });

            context.Contracts.AddRange(
                new Contract { EmployeeId = inScope.Id, EmployeeNameSnapshot = outOfScope.Name, EndDate = now.AddDays(10), StartDate = now.AddDays(-30), Status = ContractStatusSemantics.Effective },
                new Contract { EmployeeId = outOfScope.Id, EmployeeNameSnapshot = inScope.Name, EndDate = now.AddDays(10), StartDate = now.AddDays(-30), Status = ContractStatusSemantics.Effective });

            context.Attendances.Add(new Attendance
            {
                EmployeeId = inScope.Id,
                EmployeeNameSnapshot = outOfScope.Name,
                CheckIn = leaveDate.AddHours(8),
                StandardHours = 8
            });
            await context.SaveChangesAsync();

            var scope = new StaticLeadershipScope(new[] { "UNIT_A" });
            return new IdentityFixture(connection, context, scope) { InScopeEmployeeId = inScope.Id };
        }

        public LeaderController CreateLeaderController()
        {
            var controller = new LeaderController(Context, Scope);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            return controller;
        }

        public LeaderLeaveController CreateLeaderLeaveController()
        {
            var controller = new LeaderLeaveController(Context, Scope);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            return controller;
        }

        public LeaderAttendanceController CreateLeaderAttendanceController()
        {
            var controller = new LeaderAttendanceController(Context, new AttendanceCalculationService(Context), Scope);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            return controller;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static DateTime FirstWeekday(int year, int month)
        {
            var date = new DateTime(year, month, 1);
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                date = date.AddDays(1);
            return date;
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
