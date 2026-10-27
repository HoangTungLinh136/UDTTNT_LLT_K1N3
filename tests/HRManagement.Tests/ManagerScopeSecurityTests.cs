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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HRManagement.Tests;

public sealed class ManagerScopeSecurityTests
{
    [Fact]
    public async Task MGR04C_SCOPE_001_active_assignment_returns_only_unit_employees()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var ids = fixture.Scope.GetScopedEmployeeQuery().Select(e => e.EmployeeCode).ToList();

        Assert.Equal(new[] { "E_A1", "E_A2", "E_B1" }, ids.OrderBy(x => x));
    }

    [Fact]
    public async Task MGR04C_SCOPE_004_005_006_008_ended_future_and_unassigned_are_excluded()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var ids = fixture.Scope.GetScopedEmployeeQuery().Select(e => e.EmployeeCode).ToList();

        Assert.DoesNotContain("E_C1", ids);
        Assert.DoesNotContain("E_D1", ids);
        Assert.DoesNotContain("E_E1", ids);
    }

    [Fact]
    public async Task MGR04C_SCOPE_007_zero_active_assignments_returns_empty_scope()
    {
        await using var fixture = await SecurityFixture.CreateAsync(manager: "manager-zero");
        var ids = fixture.Scope.GetScopedEmployeeQuery().Select(e => e.EmployeeCode).ToList();

        Assert.Empty(ids);
    }

    [Fact]
    public async Task MGR04C_SCOPE_010_manager_cannot_inherit_another_managers_scope()
    {
        await using var fixture = await SecurityFixture.CreateAsync(manager: "manager-b");
        var ids = fixture.Scope.GetScopedEmployeeQuery().Select(e => e.EmployeeCode).ToList();

        Assert.Equal(new[] { "E_C1" }, ids);
    }

    [Fact]
    public async Task MGR04C_ATT_001_to_005_attendance_query_intersects_manager_scope()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var scopedIds = fixture.Scope.GetScopedEmployeeQuery().Select(e => e.Id);
        var rows = await fixture.Context.Attendances
            .Where(a => a.EmployeeId.HasValue && scopedIds.Contains(a.EmployeeId.Value))
            .Select(a => a.Employee!.EmployeeCode)
            .ToListAsync();

        Assert.Contains("E_A1", rows);
        Assert.Contains("E_B1", rows);
        Assert.DoesNotContain("E_C1", rows);
        Assert.DoesNotContain("E_D1", rows);
        Assert.DoesNotContain("E_E1", rows);
    }

    [Fact]
    public async Task MGR04C_ATT_006_007_search_and_department_filters_cannot_expand_scope()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var scopedIds = fixture.Scope.GetScopedEmployeeQuery().Select(e => e.Id);
        var outOfScopeSearch = await fixture.Context.Attendances
            .Where(a => a.Employee != null && a.Employee.EmployeeCode == "E_E1")
            .Where(a => a.EmployeeId.HasValue && scopedIds.Contains(a.EmployeeId.Value))
            .CountAsync();
        var outOfScopeDepartment = await fixture.Context.Attendances
            .Where(a => a.Employee != null && a.Employee.Department == "UNIT_E")
            .Where(a => a.EmployeeId.HasValue && scopedIds.Contains(a.EmployeeId.Value))
            .CountAsync();

        Assert.Equal(0, outOfScopeSearch);
        Assert.Equal(0, outOfScopeDepartment);
    }

    [Fact]
    public async Task MGR05_ATT_001_attendance_view_exposes_only_current_scope_units()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var controller = new ManagerWorkspaceController(fixture.Context, fixture.Scope, null!, null!, null!, null!);
        controller.ControllerContext = new ControllerContext { HttpContext = fixture.Http };

        var result = await controller.Attendance();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ManagerListViewModel<ManagerAttendanceRow>>(view.Model);
        Assert.Equal(new[] { "UNIT_A", "UNIT_B" }, model.OrganizationalUnits);
        Assert.DoesNotContain("UNIT_C", model.OrganizationalUnits);
        Assert.DoesNotContain("UNIT_D", model.OrganizationalUnits);
        Assert.DoesNotContain("UNIT_E", model.OrganizationalUnits);
    }

    [Fact]
    public async Task MGR05_KPI_001_direct_url_to_evaluation_outside_active_scope_is_denied()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var managerId = await fixture.Context.Users.Where(u => u.Username == "manager-a").Select(u => u.Id).SingleAsync();
        var employeeId = await fixture.Context.Employees.Where(e => e.EmployeeCode == "E_C1").Select(e => e.Id).SingleAsync();
        var period = new KPIPeriod { Name = "MGR05 scope period", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(30), Status = "ACTIVE" };
        fixture.Context.KPIPeriods.Add(period);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.KPIEvaluations.Add(new KPIEvaluation
        {
            PeriodId = period.Id, EmployeeId = employeeId, ManagerUserId = managerId,
            ManagerUsernameSnapshot = "manager-a", Status = "DRAFT", CreatedBy = "manager-a"
        });
        await fixture.Context.SaveChangesAsync();
        var evaluationId = await fixture.Context.KPIEvaluations.Select(e => e.Id).SingleAsync();
        var controller = new ManagerKPIController(fixture.Context, null!, fixture.Scope);
        controller.ControllerContext = new ControllerContext { HttpContext = fixture.Http };

        var result = await controller.Details(evaluationId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task MGR04C_DIS_004_out_of_scope_submit_is_denied()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var employeeId = await fixture.Context.Employees.Where(e => e.EmployeeCode == "E_C1").Select(e => e.Id).SingleAsync();
        var record = new DismissalRecord
        {
            EmployeeId = employeeId, EmployeeCodeSnapshot = "E_C1", EmployeeNameSnapshot = "C1",
            CurrentDepartmentSnapshot = "UNIT_C", ApprovalStatus = "DRAFT", CreatedBy = "manager-a",
            EffectiveDate = DateTime.Today.AddDays(30)
        };
        fixture.Context.DismissalRecords.Add(record);
        await fixture.Context.SaveChangesAsync();
        var controller = new DismissalController(fixture.Context, null!, null!, fixture.Scope, null!, null!, null!);
        controller.ControllerContext = new ControllerContext { HttpContext = fixture.Http };

        var result = await controller.Submit(record.Id);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        Assert.Equal("DRAFT", (await fixture.Context.DismissalRecords.FindAsync(record.Id))!.ApprovalStatus);
    }

    [Fact]
    public async Task MGR04C_TRF_004_out_of_scope_update_is_denied()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var employeeId = await fixture.Context.Employees.Where(e => e.EmployeeCode == "E_C1").Select(e => e.Id).SingleAsync();
        var record = new TransferRecord
        {
            EmployeeId = employeeId, EmployeeCodeSnapshot = "E_C1", EmployeeNameSnapshot = "C1",
            CurrentDepartmentSnapshot = "UNIT_C", ApprovalStatus = "DRAFT", CreatedBy = "manager-a",
            EffectiveDate = DateTime.Today.AddDays(30)
        };
        fixture.Context.TransferRecords.Add(record);
        await fixture.Context.SaveChangesAsync();
        var controller = new TransferController(fixture.Context, null!, null!, null!, fixture.Scope, null!, null!, null!);
        controller.ControllerContext = new ControllerContext { HttpContext = fixture.Http };

        var result = await controller.Update(new TransferRecord { Id = record.Id, TargetDepartment = "UNIT_A" }, "UPDATE");

        var json = Assert.IsType<JsonResult>(result);
        Assert.False((bool)json.Value!.GetType().GetProperty("success")!.GetValue(json.Value)!);
        Assert.Equal("DRAFT", (await fixture.Context.TransferRecords.FindAsync(record.Id))!.ApprovalStatus);
    }

    [Fact]
    public async Task MGR04C_APP_007_out_of_scope_cancel_is_denied()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var employeeId = await fixture.Context.Employees.Where(e => e.EmployeeCode == "E_C1").Select(e => e.Id).SingleAsync();
        var record = new AppointmentRecord
        {
            EmployeeId = employeeId, EmployeeCodeSnapshot = "E_C1", EmployeeNameSnapshot = "C1",
            PreviousDepartment = "UNIT_C", ApprovalStatus = "DRAFT", CreatedBy = "manager-a",
            EffectiveDate = DateTime.Today.AddDays(30)
        };
        fixture.Context.AppointmentRecords.Add(record);
        await fixture.Context.SaveChangesAsync();
        var controller = new AppointmentController(fixture.Context, null!, null!, fixture.Scope, null!, null!, null!);
        controller.ControllerContext = new ControllerContext { HttpContext = fixture.Http };

        var result = await controller.Cancel(record.Id, "scope test");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        Assert.Equal("DRAFT", (await fixture.Context.AppointmentRecords.FindAsync(record.Id))!.ApprovalStatus);
    }

    [Fact]
    public async Task MGR04C_DIS_005_cross_manager_submit_is_denied()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var employeeId = await fixture.Context.Employees.Where(e => e.EmployeeCode == "E_A1").Select(e => e.Id).SingleAsync();
        var record = new DismissalRecord
        {
            EmployeeId = employeeId, EmployeeCodeSnapshot = "E_A1", EmployeeNameSnapshot = "A1",
            CurrentDepartmentSnapshot = "UNIT_A", ApprovalStatus = "DRAFT", CreatedBy = "manager-b",
            EffectiveDate = DateTime.Today.AddDays(30)
        };
        fixture.Context.DismissalRecords.Add(record);
        await fixture.Context.SaveChangesAsync();
        var controller = new DismissalController(fixture.Context, null!, null!, fixture.Scope, null!, null!, null!);
        controller.ControllerContext = new ControllerContext { HttpContext = fixture.Http };

        var result = await controller.Submit(record.Id);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal("DRAFT", (await fixture.Context.DismissalRecords.FindAsync(record.Id))!.ApprovalStatus);
    }

    [Fact]
    public async Task MGR04C_TRF_005_cross_manager_update_is_denied()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var employeeId = await fixture.Context.Employees.Where(e => e.EmployeeCode == "E_A1").Select(e => e.Id).SingleAsync();
        var record = new TransferRecord
        {
            EmployeeId = employeeId, EmployeeCodeSnapshot = "E_A1", EmployeeNameSnapshot = "A1",
            CurrentDepartmentSnapshot = "UNIT_A", ApprovalStatus = "DRAFT", CreatedBy = "manager-b",
            EffectiveDate = DateTime.Today.AddDays(30)
        };
        fixture.Context.TransferRecords.Add(record);
        await fixture.Context.SaveChangesAsync();
        var controller = new TransferController(fixture.Context, null!, null!, null!, fixture.Scope, null!, null!, null!);
        controller.ControllerContext = new ControllerContext { HttpContext = fixture.Http };

        var result = await controller.Update(new TransferRecord { Id = record.Id, TargetDepartment = "UNIT_B" }, "UPDATE");

        var json = Assert.IsType<JsonResult>(result);
        Assert.False((bool)json.Value!.GetType().GetProperty("success")!.GetValue(json.Value)!);
        Assert.Equal("DRAFT", (await fixture.Context.TransferRecords.FindAsync(record.Id))!.ApprovalStatus);
    }

    [Fact]
    public async Task MGR04D_DIS_001_in_scope_submit_advances_to_hr_without_employee_mutation()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var employee = await fixture.Context.Employees.SingleAsync(e => e.EmployeeCode == "E_A1");
        var before = (employee.Department, employee.Position, employee.JobTitle);
        var record = new DismissalRecord
        {
            EmployeeId = employee.Id, EmployeeCodeSnapshot = employee.EmployeeCode, EmployeeNameSnapshot = employee.Name,
            CurrentDepartmentSnapshot = employee.Department, CurrentPositionSnapshot = employee.Position,
            CurrentJobTitleSnapshot = employee.JobTitle, ApprovalStatus = "DRAFT", CreatedBy = "manager-a",
            EffectiveDate = DateTime.Today.AddDays(30)
        };
        fixture.Context.DismissalRecords.Add(record);
        await fixture.Context.SaveChangesAsync();
        var controller = new DismissalController(fixture.Context, null!, null!, fixture.Scope, null!, null!, fixture.Notifications);
        controller.ControllerContext = new ControllerContext { HttpContext = fixture.Http };

        var result = await controller.Submit(record.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("PENDING_HR_REVIEW", (await fixture.Context.DismissalRecords.FindAsync(record.Id))!.ApprovalStatus);
        var persistedEmployee = await fixture.Context.Employees.FindAsync(employee.Id);
        Assert.Equal(before, (persistedEmployee!.Department, persistedEmployee.Position, persistedEmployee.JobTitle));
        Assert.True((await fixture.Context.Users.SingleAsync(u => u.Username == "manager-a")).CanLogin);
    }

    [Fact]
    public async Task MGR04D_TRF_001_in_scope_update_persists_proposal_only()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var employee = await fixture.Context.Employees.SingleAsync(e => e.EmployeeCode == "E_A1");
        var before = (employee.Department, employee.Position, employee.JobTitle);
        var record = new TransferRecord
        {
            EmployeeId = employee.Id, EmployeeCodeSnapshot = employee.EmployeeCode, EmployeeNameSnapshot = employee.Name,
            CurrentDepartmentSnapshot = employee.Department, CurrentPositionSnapshot = employee.Position,
            CurrentJobTitleSnapshot = employee.JobTitle, ApprovalStatus = "DRAFT", CreatedBy = "manager-a",
            EffectiveDate = DateTime.Today.AddDays(30)
        };
        fixture.Context.TransferRecords.Add(record);
        await fixture.Context.SaveChangesAsync();
        var controller = new TransferController(fixture.Context, null!, null!, new NoopAuditLogService(), fixture.Scope, null!, null!, fixture.Notifications);
        controller.ControllerContext = new ControllerContext { HttpContext = fixture.Http };

        var result = await controller.Update(new TransferRecord { Id = record.Id, TargetDepartment = "UNIT_B", TargetPosition = "Target Position", TargetJobTitle = "Target Title", Reason = "test" }, "UPDATE");

        var json = Assert.IsType<JsonResult>(result);
        Assert.True((bool)json.Value!.GetType().GetProperty("success")!.GetValue(json.Value)!);
        var persisted = await fixture.Context.TransferRecords.FindAsync(record.Id);
        Assert.Equal("UNIT_B", persisted!.TargetDepartment);
        var persistedEmployee = await fixture.Context.Employees.FindAsync(employee.Id);
        Assert.Equal(before, (persistedEmployee!.Department, persistedEmployee.Position, persistedEmployee.JobTitle));
    }

    [Fact]
    public async Task MGR04D_APP_003_in_scope_submit_advances_to_hr_without_employee_mutation()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var employee = await fixture.Context.Employees.SingleAsync(e => e.EmployeeCode == "E_A1");
        var before = (employee.Department, employee.Position, employee.JobTitle);
        var record = new AppointmentRecord
        {
            EmployeeId = employee.Id, EmployeeCodeSnapshot = employee.EmployeeCode, EmployeeNameSnapshot = employee.Name,
            PreviousDepartment = employee.Department, PreviousPosition = employee.Position,
            PreviousJobTitle = employee.JobTitle, ApprovalStatus = "DRAFT", CreatedBy = "manager-a",
            EffectiveDate = DateTime.Today.AddDays(30)
        };
        fixture.Context.AppointmentRecords.Add(record);
        await fixture.Context.SaveChangesAsync();
        var controller = new AppointmentController(fixture.Context, null!, null!, fixture.Scope, null!, null!, fixture.Notifications);
        controller.ControllerContext = new ControllerContext { HttpContext = fixture.Http };

        var result = await controller.Submit(record.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("PENDING_HR_REVIEW", (await fixture.Context.AppointmentRecords.FindAsync(record.Id))!.ApprovalStatus);
        var persistedEmployee = await fixture.Context.Employees.FindAsync(employee.Id);
        Assert.Equal(before, (persistedEmployee!.Department, persistedEmployee.Position, persistedEmployee.JobTitle));
    }

    private sealed class SecurityFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public ManagerScopeService Scope { get; }
        public HttpContext Http { get; }
        public NotificationEngineService Notifications { get; }

        private SecurityFixture(SqliteConnection connection, ApplicationDbContext context, ManagerScopeService scope, HttpContext http, NotificationEngineService notifications)
        {
            _connection = connection;
            Context = context;
            Scope = scope;
            Http = http;
            Notifications = notifications;
        }

        public static async Task<SecurityFixture> CreateAsync(string manager = "manager-a")
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var http = new DefaultHttpContext();
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, manager),
                new Claim(ClaimTypes.Name, manager),
                new Claim(ClaimTypes.Role, AppRoles.Manager)
            }, "Test"));
            var accessor = new FixedHttpContextAccessor(http);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options, accessor);
            await context.Database.EnsureCreatedAsync();

            var managerA = new User { Username = "manager-a", FullName = "Manager A", Email = "manager-a@test", Role = AppRoles.Manager, CanLogin = true };
            var managerB = new User { Username = "manager-b", FullName = "Manager B", Email = "manager-b@test", Role = AppRoles.Manager, CanLogin = true };
            var zero = new User { Username = "manager-zero", FullName = "Manager Zero", Email = "manager-zero@test", Role = AppRoles.Manager, CanLogin = true };
            context.Users.AddRange(managerA, managerB, zero);

            var employees = new[]
            {
                new Employee { EmployeeCode = "E_A1", Name = "A1", Department = "UNIT_A", Status = "Active" },
                new Employee { EmployeeCode = "E_A2", Name = "A2", Department = "UNIT_A", Status = "Active" },
                new Employee { EmployeeCode = "E_B1", Name = "B1", Department = "UNIT_B", Status = "Active" },
                new Employee { EmployeeCode = "E_C1", Name = "C1", Department = "UNIT_C", Status = "Active" },
                new Employee { EmployeeCode = "E_D1", Name = "D1", Department = "UNIT_D", Status = "Active" },
                new Employee { EmployeeCode = "E_E1", Name = "E1", Department = "UNIT_E", Status = "Active" }
            };
            context.Employees.AddRange(employees);
            await context.SaveChangesAsync();

            // Manager identity is linked to an Employee independently from
            // ManagerAssignment scope.  This also lets profile tests verify
            // that a zero-scope manager can still see their own profile.
            managerA.EmployeeId = employees[0].Id;
            managerB.EmployeeId = employees[3].Id;
            zero.EmployeeId = employees[5].Id;
            await context.SaveChangesAsync();

            var now = DateTime.Now;
            context.ManagerAssignments.AddRange(
                new ManagerAssignment { UserId = managerA.Id, OrganizationalUnit = "UNIT_A", EffectiveFrom = now.AddDays(-30) },
                new ManagerAssignment { UserId = managerA.Id, OrganizationalUnit = "UNIT_B", EffectiveFrom = now.AddDays(-30) },
                new ManagerAssignment { UserId = managerA.Id, OrganizationalUnit = "UNIT_C", EffectiveFrom = now.AddDays(-30), EffectiveTo = now.AddDays(-1) },
                new ManagerAssignment { UserId = managerA.Id, OrganizationalUnit = "UNIT_D", EffectiveFrom = now.AddDays(1) },
                new ManagerAssignment { UserId = managerB.Id, OrganizationalUnit = "UNIT_C", EffectiveFrom = now.AddDays(-30) });
            context.Attendances.AddRange(
                employees.Select((e, i) => new Attendance { EmployeeId = e.Id, EmployeeCode = e.EmployeeCode, EmployeeNameSnapshot = e.Name, CheckIn = now.Date.AddHours(8).AddMinutes(i) }));
            await context.SaveChangesAsync();

            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<NotificationConfigService>()
                .AddSingleton<NotificationEngineService>()
                .BuildServiceProvider();
            return new SecurityFixture(connection, context, new ManagerScopeService(context, accessor), http,
                services.GetRequiredService<NotificationEngineService>());
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task LogAsync(string action, string module, string resource, string resourceId, string eventType, string status, string? failureReason = null, string? oldValues = null, string? newValues = null, string? changes = null) => Task.CompletedTask;
        public Task LogLoginAsync(string username, bool isSuccess, string? failureReason = null) => Task.CompletedTask;
        public Task LogLogoutAsync(string username) => Task.CompletedTask;
        public Task LogSecurityEventAsync(string action, string resource, string resourceId, string status, string? failureReason = null) => Task.CompletedTask;
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public FixedHttpContextAccessor(HttpContext context) => HttpContext = context;
        public HttpContext? HttpContext { get; set; }
    }
}
