using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using giao_dien_demo.Authorization;
using giao_dien_demo.Controllers;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRManagement.Tests;

public sealed class ManagerDashboardTests
{
    [Fact]
    public async Task Dashboard_aggregates_real_scoped_data_and_excludes_self_approvals()
    {
        await using var f = await Fixture.CreateAsync();
        var vm = await f.Service.LoadAsync(f.Manager, f.EventDate.Year, null);
        Assert.Equal(3, vm.ManagedEmployeeCount);
        Assert.Equal(2, vm.WorkingEmployeeCount);
        Assert.Equal(1, vm.OnLeaveTodayCount); // duplicate approved leave rows count as one person
        Assert.Equal(1, vm.AssignedLeavePendingCount);
        Assert.Equal(1, vm.AssignedAdjustmentPendingCount);
        Assert.Equal(2, vm.PendingApprovalCount);
        Assert.Equal(3, vm.PositionGroups.Sum(g => g.Count));
        Assert.Equal(66.7m, vm.WorkingPercent);
        Assert.DoesNotContain(vm.RecentEmployees, e => e.Name == "Outside");
        Assert.Null(vm.RecentEmployees.Single(e => e.Id == f.MissingDate.Id).JoinDate);
        Assert.Equal(1, vm.MissingJoinDateCount);
        Assert.Equal(2, vm.Movement.Sum(m => m.Joined));
        Assert.Equal(1, vm.Movement.Sum(m => m.Left));
        Assert.Equal(1, vm.Movement.Sum(m => m.Transferred));
        Assert.Equal(1, vm.Movement.Single(m => m.Month == f.EventDate.Month).Transferred);
        Assert.Equal(0, f.Context.ChangeTracker.Entries().Count(e => e.State == EntityState.Modified));
    }

    [Theory]
    [InlineData("UNIT_B")]
    [InlineData("not-assigned")]
    public async Task Tampered_unit_cannot_expand_any_dashboard_dataset(string unit)
    {
        await using var f = await Fixture.CreateAsync();
        var vm = await f.Service.LoadAsync(f.Manager, f.EventDate.Year, unit);
        Assert.Equal(0, vm.ManagedEmployeeCount);
        Assert.Equal(0, vm.PendingApprovalCount);
        Assert.Empty(vm.RecentEmployees);
        Assert.Empty(vm.PositionGroups);
        Assert.All(vm.Movement, m => Assert.Equal(0, m.Joined + m.Left + m.Transferred));
    }

    [Fact]
    public async Task Manager_B_receives_only_B_scope_and_zero_scope_manager_is_empty()
    {
        await using var f = await Fixture.CreateAsync();
        f.SetActor("manager-b");
        var managerB = await f.Context.Users.SingleAsync(u => u.Username == "manager-b");
        var vm = await f.Service.LoadAsync(managerB, null, null);
        Assert.Equal(1, vm.ManagedEmployeeCount);
        Assert.Equal("Outside", Assert.Single(vm.RecentEmployees).Name);
        f.SetActor("manager-zero");
        var zero = await f.Context.Users.SingleAsync(u => u.Username == "manager-zero");
        var empty = await f.Service.LoadAsync(zero, null, null);
        Assert.Equal(0, empty.ManagedEmployeeCount);
        Assert.Equal(0, empty.WorkingPercent);
        Assert.Equal(0, empty.LeavePercent);
        Assert.Empty(empty.PositionGroups);
        Assert.Equal(12, empty.Movement.Count);
    }

    [Fact]
    public async Task Employee_detail_rechecks_current_scope_and_exposes_only_work_fields()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.IsType<NotFoundResult>(await f.Controller().EmployeeDetails(f.Outside.Id));
        var view = Assert.IsType<ViewResult>(await f.Controller().EmployeeDetails(f.MissingDate.Id));
        Assert.Null(Assert.IsType<ManagerDashboardEmployee>(view.Model).JoinDate);
        Assert.Null(typeof(ManagerDashboardEmployee).GetProperty("Salary"));
        Assert.Null(typeof(ManagerDashboardEmployee).GetProperty("CitizenId"));
        Assert.Null(typeof(ManagerDashboardEmployee).GetProperty("BankAccountNumber"));
        var assignment = await f.Context.ManagerAssignments.SingleAsync(a => a.UserId == f.Manager.Id);
        assignment.EffectiveTo = DateTime.Now.AddMinutes(-1);
        await f.Context.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await f.Controller().EmployeeDetails(f.MissingDate.Id));
    }

    [Fact]
    public async Task Filtering_year_uses_effective_dates_and_never_record_creation_dates()
    {
        await using var f = await Fixture.CreateAsync();
        var vm = await f.Service.LoadAsync(f.Manager, f.EventDate.Year - 2, null);
        Assert.All(vm.Movement, m => Assert.Equal(0, m.Joined + m.Left + m.Transferred));
        var invalid = await f.Service.LoadAsync(f.Manager, int.MaxValue, null);
        Assert.Equal(DateTime.Today.Year, invalid.Year);
    }

    [Fact]
    public async Task Failed_database_query_returns_503_error_view_instead_of_zero_KPIs()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Context.Database.ExecuteSqlRawAsync("DROP TABLE OnboardingRecords");
        var controller = f.Controller();
        var view = Assert.IsType<ViewResult>(await controller.Index());
        Assert.Equal(503, f.Http.Response.StatusCode);
        Assert.Equal("DashboardUnavailable", view.ViewName);
        Assert.NotNull(Assert.IsType<ManagerWorkspaceViewModel>(view.Model).ErrorMessage);
    }

    [Fact]
    public async Task Employee_and_primary_non_manager_role_do_not_gain_manager_access()
    {
        await using var f = await Fixture.CreateAsync();
        f.SetActor("manager-a", AppRoles.Employee);
        Assert.IsType<ForbidResult>(await f.Controller().Index());
        Assert.IsType<ForbidResult>(await f.Controller().EmployeeDetails(f.Outside.Id));
        ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim(ClaimTypes.Role, AppRoles.Manager));
        Assert.IsType<ForbidResult>(await f.Controller().Index());
    }

    [Fact]
    public async Task Http_routes_render_views_filters_details_and_enforce_manager_policy()
    {
        await using var f = await Fixture.CreateAsync();
        await using var app = await f.StartWebAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Add("X-Dashboard-Test-Actor", "manager-a");
        var response = await client.GetAsync("/ManagerWorkspace?year=" + f.EventDate.Year);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("manager-dashboard", html);
        await CaptureAsync("populated", html);
        Assert.Contains("md-donut", html);
        Assert.Contains("viewBox=", html);
        Assert.Contains(">T12</span>", html);
        Assert.DoesNotContain("T@month", html);
        Assert.Contains("/ManagerWorkspace/EmployeeDetails/", html);
        Assert.DoesNotContain(">Outside<", html);
        Assert.Equal(1, html.Split("aria-label=\"Breadcrumb\"").Length - 1);
        Assert.DoesNotContain("chart.js", html.ToLowerInvariant());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/ManagerWorkspace/EmployeeDetails/" + f.Outside.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ManagerWorkspace/EmployeeDetails/" + f.MissingDate.Id)).StatusCode);
        var emptyResponse = await client.GetAsync("/ManagerWorkspace?unit=UNIT_B&year=2001");
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        await CaptureAsync("empty", await emptyResponse.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Remove("X-Dashboard-Test-Actor");
        client.DefaultRequestHeaders.Add("X-Dashboard-Test-Actor", "employee");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/ManagerWorkspace")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/ManagerWorkspace/EmployeeDetails/" + f.MissingDate.Id)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Dashboard-Test-Actor");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/ManagerWorkspace")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Dashboard-Test-Actor", "manager-a");
        await f.Context.Database.ExecuteSqlRawAsync("DROP TABLE OnboardingRecords");
        var unavailable = await client.GetAsync("/ManagerWorkspace");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        var errorHtml = await unavailable.Content.ReadAsStringAsync();
        Assert.DoesNotContain("md-kpi", errorHtml);
        await CaptureAsync("error", errorHtml);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ApplicationDbContext Context { get; }
        public DefaultHttpContext Http { get; } = new();
        private readonly FixedAccessor accessor = new();
        private readonly ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
        public ManagerDashboardService Service => new(Context, new ManagerScopeService(Context, accessor));
        public User Manager { get; private set; } = null!;
        public Employee Outside { get; private set; } = null!;
        public Employee MissingDate { get; private set; } = null!;
        public DateTime EventDate { get; } = DateTime.Today.AddDays(-2);

        private Fixture(SqliteConnection connection)
        {
            this.connection = connection;
            accessor.HttpContext = Http;
            Http.RequestServices = services;
            SetActor("manager-a");
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options, accessor);
        }

        public void SetActor(string name, string role = AppRoles.Manager) => Http.User = Principal(name, role);
        public ManagerWorkspaceController Controller() => new(Context, new ManagerScopeService(Context, accessor), null!, null!, null!, null!)
        { ControllerContext = new ControllerContext { HttpContext = Http }, TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(Http, new TestTempDataProvider()) };

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var f = new Fixture(connection);
            await f.Context.Database.EnsureCreatedAsync();
            var self = new Employee { Name = "Manager profile", EmployeeCode = "MD-M", Department = "UNIT_A", Status = "Active", Position = "Chính thức" };
            var worker = new Employee { Name = "Scoped worker", EmployeeCode = "MD-A", Department = "UNIT_A", Status = "Active", Position = "Thử việc" };
            f.MissingDate = new Employee { Name = "No join date", EmployeeCode = "MD-N", Department = "UNIT_A", Status = "Offboarded" };
            f.Outside = new Employee { Name = "Outside", EmployeeCode = "MD-B", Department = "UNIT_B", Status = "Active", Position = "Outside position" };
            f.Context.Employees.AddRange(self, worker, f.MissingDate, f.Outside);
            await f.Context.SaveChangesAsync();
            f.Manager = new User { Username = "manager-a", Role = AppRoles.Manager, CanLogin = true, EmployeeId = self.Id };
            var managerB = new User { Username = "manager-b", Role = AppRoles.Manager, CanLogin = true, EmployeeId = f.Outside.Id };
            var zero = new User { Username = "manager-zero", Role = AppRoles.Manager, CanLogin = true };
            f.Context.Users.AddRange(f.Manager, managerB, zero);
            await f.Context.SaveChangesAsync();
            f.Context.ManagerAssignments.AddRange(
                new ManagerAssignment { UserId = f.Manager.Id, OrganizationalUnit = "UNIT_A", EffectiveFrom = DateTime.Today.AddDays(-30) },
                new ManagerAssignment { UserId = managerB.Id, OrganizationalUnit = "UNIT_B", EffectiveFrom = DateTime.Today.AddDays(-30) });
            foreach (var employee in new[] { self, worker, f.Outside })
            {
                f.Context.OnboardingRecords.Add(new OnboardingRecord { EmployeeId = employee.Id, JoinDate = f.EventDate, OnboardingStatus = "COMPLETED", CreatedAt = f.EventDate.AddYears(-2) });
                f.Context.Leaves.Add(new Leave { EmployeeId = employee.Id, Status = "PENDING", FromDate = DateTime.Today, ToDate = DateTime.Today.AddDays(1) });
                f.Context.AttendanceAdjustmentRequests.Add(new AttendanceAdjustmentRequest { Attendance = new Attendance { EmployeeId = employee.Id, CheckIn = DateTime.Today.AddHours(8) }, EmployeeCode = employee.EmployeeCode!, Status = "PENDING", Reason = "Test" });
            }
            f.Context.OnboardingRecords.Add(new OnboardingRecord { EmployeeId = worker.Id, JoinDate = f.EventDate.AddDays(1), OnboardingStatus = "COMPLETED" });
            f.Context.OnboardingRecords.Add(new OnboardingRecord { EmployeeId = f.MissingDate.Id, JoinDate = f.EventDate, OnboardingStatus = "CANCELLED" });
            f.Context.OnboardingRecords.Add(new OnboardingRecord { EmployeeId = f.MissingDate.Id, JoinDate = DateTime.Today.AddDays(20), OnboardingStatus = "PENDING" });
            f.Context.Leaves.AddRange(
                new Leave { EmployeeId = worker.Id, Status = "APPROVED", FromDate = DateTime.Today, ToDate = DateTime.Today },
                new Leave { EmployeeId = worker.Id, Status = "Đã duyệt", FromDate = DateTime.Today.AddDays(-1), ToDate = DateTime.Today },
                new Leave { EmployeeId = f.Outside.Id, Status = "APPROVED", FromDate = DateTime.Today, ToDate = DateTime.Today });
            foreach (var employee in new[] { worker, f.Outside })
            {
                f.Context.TransferRecords.Add(new TransferRecord { EmployeeId = employee.Id, ApprovalStatus = "APPROVED", AppliedAt = DateTime.Now, EffectiveDate = f.EventDate, CreatedAt = f.EventDate.AddYears(-2) });
                f.Context.TransferRecords.Add(new TransferRecord { EmployeeId = employee.Id, ApprovalStatus = "PENDING_APPROVAL", EffectiveDate = f.EventDate });
                f.Context.OffboardingRecords.Add(new OffboardingRecord { EmployeeId = employee.Id, ApprovalStatus = "APPROVED", CompletedAt = DateTime.Now, EffectiveDate = f.EventDate, CreatedAt = f.EventDate.AddYears(-2) });
                f.Context.OffboardingRecords.Add(new OffboardingRecord { EmployeeId = employee.Id, ApprovalStatus = "APPROVED", EffectiveDate = f.EventDate });
            }
            await f.Context.SaveChangesAsync();
            return f;
        }

        public async Task<WebApplication> StartWebAsync()
        {
            var root = FindWebRoot();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = root, ApplicationName = typeof(ManagerWorkspaceController).Assembly.GetName().Name });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(Context);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IManagerScopeService, ManagerScopeService>();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession();
            builder.Services.AddAuthentication("DashboardTest").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("DashboardTest", _ => { });
            builder.Services.AddAuthorization(o => o.AddPolicy(AppPolicies.ManagerOnly, p => p.RequireAuthenticatedUser().RequireRole(AppRoles.Manager)));
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ManagerWorkspaceController).Assembly).AddControllersAsServices();
            builder.Services.AddTransient(sp => new ManagerWorkspaceController(Context, sp.GetRequiredService<IManagerScopeService>(), null!, null!, null!, null!));
            var app = builder.Build();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseSession();
            app.UseAuthentication();
            app.Use(async (http, next) => { http.Session.SetString("Role", CurrentActor.Role(http.User) ?? ""); http.Session.SetString("User", CurrentActor.Username(http.User) ?? ""); await next(); });
            app.UseAuthorization();
            app.MapControllerRoute("default", "{controller=ManagerWorkspace}/{action=Index}/{id?}");
            await app.StartAsync();
            return app;
        }
        private static string FindWebRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
        {
            for (var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "giao diện demo");
                if (File.Exists(Path.Combine(candidate, "Views", "ManagerWorkspace", "Index.cshtml"))) return candidate;
            }
            throw new DirectoryNotFoundException("Cannot locate web content root");
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); await services.DisposeAsync(); }
    }

    private static async Task CaptureAsync(string name, string html)
    {
        var directory = Environment.GetEnvironmentVariable("MANAGER_DASHBOARD_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, name + ".html"), html);
    }

    private sealed class FixedAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
    private sealed class TestTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static ClaimsPrincipal Principal(string name, string role) => new(new ClaimsIdentity(new[]
    { new Claim(ClaimTypes.NameIdentifier, name), new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.Role, role) }, "DashboardTest"));

    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var name = Request.Headers["X-Dashboard-Test-Actor"].ToString();
            if (name == "") return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(Principal(name, name == "employee" ? AppRoles.Employee : AppRoles.Manager), Scheme.Name)));
        }
    }
}
