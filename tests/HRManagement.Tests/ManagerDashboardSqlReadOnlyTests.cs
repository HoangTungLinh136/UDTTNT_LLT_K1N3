using System.Security.Claims;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HRManagement.Tests;

public sealed class ManagerDashboardSqlReadOnlyTests
{
    // Explicit opt-in. This probe issues SELECTs only; it never starts the application,
    // lifecycle workers, migrations, seeding, or a database creation workflow.
    [LocalSqlReadOnlyFact]
    public async Task Existing_SQL_Server_dashboard_queries_match_current_manager_scope()
    {
        var root = FindRoot();
        var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true).Build();
        var accessor = new ProbeAccessor();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(config.GetConnectionString("DefaultConnection"), sql => sql.CommandTimeout(30)).Options;
        await using var context = new ApplicationDbContext(options, accessor);
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        var managers = await context.Users.Where(u => u.Role == AppRoles.Manager && u.CanLogin).OrderBy(u => u.Id).Take(2).ToListAsync();
        Assert.NotEmpty(managers);
        foreach (var manager in managers)
        {
            accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            { new Claim(ClaimTypes.NameIdentifier, manager.Username), new Claim(ClaimTypes.Role, AppRoles.Manager) }, "ReadOnlyProbe")) };
            var scope = new ManagerScopeService(context, accessor);
            var service = new ManagerDashboardService(context, scope);
            var model = await service.LoadAsync(manager, DateTime.Today.Year, null);
            var scopedIds = await scope.GetScopedEmployeeQuery().Select(e => e.Id).ToListAsync();
            Assert.Equal(scopedIds.Count, model.ManagedEmployeeCount);
            Assert.Equal(model.ManagedEmployeeCount, model.PositionGroups.Sum(g => g.Count));
            Assert.All(model.RecentEmployees, e => Assert.Contains(e.Id, scopedIds));
            Assert.InRange(model.OnLeaveTodayCount, 0, model.ManagedEmployeeCount);
            Assert.Equal(12, model.Movement.Count);
            var denied = await service.LoadAsync(manager, null, "__dashboard_probe_unassigned_unit__");
            Assert.Empty(denied.RecentEmployees);
            Assert.Equal(0, denied.ManagedEmployeeCount);
            Assert.Empty(context.ChangeTracker.Entries());
        }
    }
    private sealed class ProbeAccessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }
    private static string FindRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "giao diện demo");
            if (File.Exists(Path.Combine(candidate, "appsettings.json"))) return candidate;
        }
        throw new DirectoryNotFoundException("Web project not found");
    }
}

public sealed class LocalSqlReadOnlyFactAttribute : FactAttribute
{
    public LocalSqlReadOnlyFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MANAGER_DASHBOARD_SQL_PROBE") != "1")
            Skip = "Opt-in read-only check against the configured local SQL Server";
    }
}
