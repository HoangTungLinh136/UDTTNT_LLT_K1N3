using System.Security.Claims;
using giao_dien_demo.Authorization;
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

public sealed class LeaderAttendanceScopeTests
{
    [Fact]
    public async Task LDRSCOPE_001_active_multi_unit_scope_is_applied_to_index_late_report_and_export()
    {
        await using var fixture = await ScopeFixture.CreateAsync("leader-a");
        var controller = fixture.CreateController();
        var month = DateTime.Now.ToString("MM/yyyy");

        var index = Assert.IsType<ViewResult>(controller.Index(month));
        var indexModel = Assert.IsType<AttendanceReportViewModel>(index.Model);
        Assert.Equal(new[] { "E_A1", "E_B1" }, indexModel.EmployeeDetails.Select(x => x.EmployeeCode).OrderBy(x => x));
        Assert.DoesNotContain(indexModel.EmployeeDetails, x => x.EmployeeCode == "E_C1");
        Assert.Equal(2, indexModel.TotalEmployees);

        var late = Assert.IsType<ViewResult>(controller.LateReport(month));
        var lateModel = Assert.IsType<LateReportViewModel>(late.Model);
        Assert.DoesNotContain(lateModel.EmployeeSummaries, x => x.EmployeeCode == "E_C1");

        var export = Assert.IsType<FileContentResult>(controller.ExportExcel(month));
        var csv = System.Text.Encoding.UTF8.GetString(export.FileContents);
        Assert.Contains("E_A1", csv);
        Assert.Contains("E_B1", csv);
        Assert.DoesNotContain("E_C1", csv);
    }

    [Fact]
    public async Task LDRSCOPE_002_department_filter_only_narrows_and_cannot_broaden_scope()
    {
        await using var fixture = await ScopeFixture.CreateAsync("leader-a");
        var controller = fixture.CreateController();
        var month = DateTime.Now.ToString("MM/yyyy");

        var allowed = Assert.IsType<ViewResult>(controller.Index(month, "UNIT_A"));
        var allowedModel = Assert.IsType<AttendanceReportViewModel>(allowed.Model);
        Assert.Equal(new[] { "E_A1" }, allowedModel.EmployeeDetails.Select(x => x.EmployeeCode));

        var invalid = Assert.IsType<ViewResult>(controller.Index(month, "UNIT_C"));
        var invalidModel = Assert.IsType<AttendanceReportViewModel>(invalid.Model);
        Assert.Empty(invalidModel.EmployeeDetails);
        Assert.Empty(invalidModel.DepartmentSummaries);
    }

    [Fact]
    public async Task LDRSCOPE_003_no_expired_or_future_assignment_fails_closed()
    {
        await using var fixture = await ScopeFixture.CreateAsync("leader-empty", includeAssignments: false);
        var now = DateTime.Now;
        fixture.Context.LeaderAssignments.AddRange(
            new LeaderAssignment { UserId = fixture.CurrentUserId, OrganizationalUnit = "UNIT_A", EffectiveFrom = now.AddDays(-10), EffectiveTo = now.AddMinutes(-1) },
            new LeaderAssignment { UserId = fixture.CurrentUserId, OrganizationalUnit = "UNIT_B", EffectiveFrom = now.AddMinutes(1) });
        await fixture.Context.SaveChangesAsync();

        Assert.Empty(fixture.Scope.GetOverseenOrganizationalUnits());
        var result = Assert.IsType<ViewResult>(fixture.CreateController().Index(DateTime.Now.ToString("MM/yyyy")));
        var model = Assert.IsType<AttendanceReportViewModel>(result.Model);
        Assert.Empty(model.EmployeeDetails);
    }

    [Fact]
    public async Task LDRSCOPE_004_detail_outside_active_scope_is_denied()
    {
        await using var fixture = await ScopeFixture.CreateAsync("leader-a");
        var outsideId = await fixture.Context.Employees.Where(e => e.EmployeeCode == "E_C1").Select(e => e.Id).SingleAsync();

        var result = fixture.CreateController().GetLateDetails(outsideId, DateTime.Now.ToString("MM/yyyy"));

        Assert.True(result is ForbidResult or NotFoundResult);
    }

    [Fact]
    public async Task LDRSCOPE_005_manager_hr_and_personal_queries_keep_their_existing_boundaries()
    {
        await using var fixture = await ScopeFixture.CreateAsync("leader-a");
        var service = new AttendanceCalculationService(fixture.Context);
        var employees = await fixture.Context.Employees.OrderBy(e => e.EmployeeCode).ToListAsync();

        var hrRows = await service.GetScopedAttendanceQuery("HR", null, Array.Empty<string>()).Select(a => a.Employee!.EmployeeCode).ToListAsync();
        Assert.Equal(new[] { "E_A1", "E_B1", "E_C1" }, hrRows.OrderBy(x => x));

        var ownRows = await service.GetScopedAttendanceQuery("Employee", employees[0].Id, Array.Empty<string>()).Select(a => a.Employee!.EmployeeCode).ToListAsync();
        Assert.Equal(new[] { "E_A1" }, ownRows);

        var managerRows = await service.GetScopedAttendanceQuery("Manager", null, Array.Empty<string>()).Select(a => a.Employee!.EmployeeCode).ToListAsync();
        Assert.Equal(new[] { "E_A1", "E_B1", "E_C1" }, managerRows.OrderBy(x => x));
    }

    private sealed class ScopeFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public LeadershipScopeService Scope { get; }
        public IHttpContextAccessor Accessor { get; }
        public int CurrentUserId { get; }

        private ScopeFixture(SqliteConnection connection, ApplicationDbContext context, LeadershipScopeService scope, IHttpContextAccessor accessor, int currentUserId)
        {
            _connection = connection;
            Context = context;
            Scope = scope;
            Accessor = accessor;
            CurrentUserId = currentUserId;
        }

        public static async Task<ScopeFixture> CreateAsync(string username, bool includeAssignments = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var http = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, username),
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, AppRoles.Leader)
                }, "Test"))
            };
            var accessor = new FixedHttpContextAccessor(http);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var context = new ApplicationDbContext(options, accessor);
            await context.Database.EnsureCreatedAsync();

            var leader = new User { Username = username, FullName = username, Email = username + "@test", Role = AppRoles.Leader, CanLogin = true };
            context.Users.Add(leader);
            var employees = new[]
            {
                new Employee { EmployeeCode = "E_A1", Name = "A1", Department = "UNIT_A", Status = "Active" },
                new Employee { EmployeeCode = "E_B1", Name = "B1", Department = "UNIT_B", Status = "Active" },
                new Employee { EmployeeCode = "E_C1", Name = "C1", Department = "UNIT_C", Status = "Active" }
            };
            context.Employees.AddRange(employees);
            await context.SaveChangesAsync();

            if (includeAssignments)
            {
                var now = DateTime.Now;
                context.LeaderAssignments.AddRange(
                    new LeaderAssignment { UserId = leader.Id, OrganizationalUnit = "UNIT_A", EffectiveFrom = now.AddDays(-10) },
                    new LeaderAssignment { UserId = leader.Id, OrganizationalUnit = "UNIT_B", EffectiveFrom = now.AddDays(-10) });
            }

            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 8, 0, 0);
            context.Attendances.AddRange(employees.Select((employee, index) => new Attendance
            {
                EmployeeId = employee.Id,
                EmployeeNameSnapshot = employee.Name,
                CheckIn = monthStart.AddDays(index),
                StandardHours = 8,
                LateMinutes = index == 2 ? 15 : 0
            }));
            await context.SaveChangesAsync();

            var scope = new LeadershipScopeService(context, accessor);
            return new ScopeFixture(connection, context, scope, accessor, leader.Id);
        }

        public LeaderAttendanceController CreateController()
        {
            var controller = new LeaderAttendanceController(Context, new AttendanceCalculationService(Context), Scope);
            controller.ControllerContext = new ControllerContext { HttpContext = Accessor.HttpContext! };
            return controller;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public FixedHttpContextAccessor(HttpContext context) => HttpContext = context;
        public HttpContext? HttpContext { get; set; }
    }
}
