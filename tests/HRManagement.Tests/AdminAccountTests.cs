using System.Security.Claims;
using giao_dien_demo.Authorization;
using giao_dien_demo.Controllers;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRManagement.Tests;

public sealed class AdminAccountTests
{
    [Fact]
    public async Task ADM_ACCOUNT_001_admin_get_account_creation_page_succeeds()
    {
        await using var fixture = await AccountFixture.CreateAsync(AppRoles.Admin);
        var result = fixture.Controller.Register();
        Assert.IsType<ViewResult>(result);
        Assert.IsType<User>(Assert.IsType<ViewResult>(result).Model);
    }

    [Fact]
    public async Task ADM_ACCOUNT_002_003_valid_admin_post_creates_once_and_redirects_to_register_not_login()
    {
        await using var fixture = await AccountFixture.CreateAsync(AppRoles.Admin);
        var before = await fixture.Context.Users.CountAsync();
        var result = await fixture.Controller.Register(Account("created-admin-flow"));
        var redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(nameof(AccountController.Register), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Equal(before + 1, await fixture.Context.Users.CountAsync());
        Assert.Equal(0, await fixture.Context.Employees.CountAsync());
        var created = await fixture.Context.Users.SingleAsync(u => u.Username == "created-admin-flow");
        Assert.Null(created.Password);
    }

    [Fact]
    public async Task ADM_ACCOUNT_004_005_admin_session_is_preserved_and_new_user_is_not_authenticated()
    {
        await using var fixture = await AccountFixture.CreateAsync(AppRoles.Admin);
        await fixture.Controller.Register(Account("session-safe"));

        Assert.True(fixture.Controller.User.Identity?.IsAuthenticated);
        Assert.Equal("admin.actor", fixture.Controller.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(AppRoles.Admin, fixture.Controller.User.FindFirstValue(ClaimTypes.Role));
        Assert.NotEqual("session-safe", fixture.Controller.User.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task ADM_ACCOUNT_006_007_success_message_and_clean_form_are_available_after_prg()
    {
        await using var fixture = await AccountFixture.CreateAsync(AppRoles.Admin);
        await fixture.Controller.Register(Account("success-message"));
        Assert.Contains("Tạo tài khoản thành công", fixture.Controller.TempData["SuccessMessage"]?.ToString());

        var get = Assert.IsType<ViewResult>(fixture.Controller.Register());
        var clean = Assert.IsType<User>(get.Model);
        Assert.True(string.IsNullOrEmpty(clean.Username));
        Assert.True(string.IsNullOrEmpty(clean.Password));
        Assert.DoesNotContain("secret", fixture.Controller.TempData.Values.Select(v => v?.ToString() ?? ""));
    }

    [Fact]
    public async Task ADM_ACCOUNT_008_invalid_post_stays_on_create_and_creates_zero_users()
    {
        await using var fixture = await AccountFixture.CreateAsync(AppRoles.Admin);
        var before = await fixture.Context.Users.CountAsync();
        fixture.Controller.ModelState.AddModelError(nameof(User.Username), "required");

        var result = await fixture.Controller.Register(Account("invalid-admin-flow"));

        Assert.IsType<ViewResult>(result);
        Assert.Equal(before, await fixture.Context.Users.CountAsync());
    }

    [Fact]
    public async Task ADM_ACCOUNT_009_duplicate_username_stays_on_create_without_duplicate_user()
    {
        await using var fixture = await AccountFixture.CreateAsync(AppRoles.Admin);
        fixture.Context.Users.Add(new User { Username = "taken", Email = "taken@example.com", FullName = "Existing", Role = AppRoles.Employee });
        await fixture.Context.SaveChangesAsync();
        var before = await fixture.Context.Users.CountAsync();

        var result = await fixture.Controller.Register(Account("taken"));

        Assert.IsType<ViewResult>(result);
        Assert.Equal(before, await fixture.Context.Users.CountAsync());
    }

    [Fact]
    public void ADM_ACCOUNT_010_011_back_action_targets_admin_users_and_is_not_submit()
    {
        var path = FindRepositoryFile("giao diện demo", "Views", "Account", "Register.cshtml");
        var source = File.ReadAllText(path);
        Assert.Contains("asp-controller=\"Admin\" asp-action=\"Users\"", source);
        Assert.Contains("Quay lại", source);
        Assert.DoesNotContain("asp-controller=\"Admin\" asp-action=\"Users\" type=\"submit\"", source);
    }

    [Fact]
    public async Task ADM_ACCOUNT_012_public_register_flow_remains_login_redirect_and_creates_its_existing_employee()
    {
        await using var fixture = await AccountFixture.CreateAsync(null);
        var beforeEmployees = await fixture.Context.Employees.CountAsync();
        var result = await fixture.Controller.Register(Account("public-flow"));

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);
        Assert.Equal(beforeEmployees + 1, await fixture.Context.Employees.CountAsync());
    }

    [Fact]
    public async Task ADM_ACCOUNT_02_001_002_003_successful_post_completes_and_returns_prg_register()
    {
        await using var fixture = await AccountFixture.CreateAsync(AppRoles.Admin);
        var result = await fixture.Controller.Register(Account("adm02-prg"));
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.Register), redirect.ActionName);
        var get = Assert.IsType<ViewResult>(fixture.Controller.Register());
        Assert.IsType<User>(get.Model);
    }

    [Fact]
    public async Task ADM_ACCOUNT_02_004_005_admin_identity_and_single_user_persistence_are_preserved()
    {
        await using var fixture = await AccountFixture.CreateAsync(AppRoles.Admin);
        var before = await fixture.Context.Users.CountAsync();
        await fixture.Controller.Register(Account("adm02-identity"));
        Assert.Equal(before + 1, await fixture.Context.Users.CountAsync());
        Assert.Equal("admin.actor", fixture.Controller.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(AppRoles.Admin, fixture.Controller.User.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public async Task ADM_ACCOUNT_02_006_nonblocking_notification_does_not_hold_post_response_open()
    {
        await using var fixture = await AccountFixture.CreateAsync(AppRoles.Admin, new HangingNotifier());
        var post = fixture.Controller.Register(Account("adm02-notify"));
        var completed = await Task.WhenAny(post, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(post, completed);
        Assert.NotNull(await fixture.Context.Users.SingleOrDefaultAsync(u => u.Username == "adm02-notify"));
    }

    [Fact]
    public void ADM_ACCOUNT_02_007_client_submit_uses_native_form_post_and_keeps_retry_path()
    {
        var path = FindRepositoryFile("giao diện demo", "Views", "Account", "Register.cshtml");
        var source = File.ReadAllText(path);
        Assert.Contains("registerForm", source);
        Assert.Contains("addEventListener('submit'", source);
        Assert.DoesNotContain("preventDefault()", source);
    }

    private static User Account(string username) => new()
    {
        Username = username,
        FullName = "Created Account",
        Email = $"{username}@example.com",
        Password = "StrongPass!123"
    };

    private static string FindRepositoryFile(params string[] parts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        throw new FileNotFoundException("Register view not found.");
    }

    private sealed class AccountFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public AccountController Controller { get; }

        private AccountFixture(SqliteConnection connection, ApplicationDbContext context, AccountController controller)
        {
            _connection = connection;
            Context = context;
            Controller = controller;
        }

        public static async Task<AccountFixture> CreateAsync(string? role, IAdminAccountNotificationService? notifier = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var http = new DefaultHttpContext();
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "admin.actor") };
            if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
            http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            var accessor = new FixedAccessor(http);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var context = new ApplicationDbContext(options, accessor);
            await context.Database.EnsureCreatedAsync();
            var controller = new AccountController(context, null!, new NoopAudit(), null, notifier)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
                TempData = new TempDataDictionary(http, new NoopTempDataProvider())
            };
            return new AccountFixture(connection, context, controller);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class NoopAudit : IAuditLogService
    {
        public Task LogAsync(string action, string module, string resource, string resourceId, string eventType, string status, string? failureReason = null, string? oldValues = null, string? newValues = null, string? changes = null) => Task.CompletedTask;
        public Task LogLoginAsync(string username, bool isSuccess, string? failureReason = null) => Task.CompletedTask;
        public Task LogLogoutAsync(string username) => Task.CompletedTask;
        public Task LogSecurityEventAsync(string action, string resource, string resourceId, string status, string? failureReason = null) => Task.CompletedTask;
    }

    private sealed class NoopTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private sealed class HangingNotifier : IAdminAccountNotificationService
    {
        public Task NotifyCreatedAsync(User user, int totalUsers) => Task.Delay(Timeout.InfiniteTimeSpan);
    }

    private sealed class FixedAccessor : IHttpContextAccessor
    {
        public FixedAccessor(HttpContext context) => HttpContext = context;
        public HttpContext? HttpContext { get; set; }
    }
}
