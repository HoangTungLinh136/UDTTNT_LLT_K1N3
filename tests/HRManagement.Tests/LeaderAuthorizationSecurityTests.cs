using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRManagement.Tests;

public sealed class LeaderAuthorizationSecurityTests
{
    [Fact]
    public async Task LDRSEC_001_session_role_cannot_create_leader_scope_without_claims()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var context = fixture.CreateHttpContext(new ClaimsPrincipal(new ClaimsIdentity()),
            new Dictionary<string, string> { ["Role"] = "Leader", ["User"] = fixture.Username });

        var scope = new LeadershipScopeService(fixture.Context, new HttpContextAccessor { HttpContext = context });

        Assert.Empty(scope.GetOverseenOrganizationalUnits());
    }

    [Fact]
    public async Task LDRSEC_002_claims_are_authoritative_over_conflicting_session_values()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, fixture.Username),
            new Claim(ClaimTypes.Role, "Leader")
        }, "test");
        var context = fixture.CreateHttpContext(new ClaimsPrincipal(identity),
            new Dictionary<string, string> { ["Role"] = "Manager", ["User"] = "different-user" });

        var scope = new LeadershipScopeService(fixture.Context, new HttpContextAccessor { HttpContext = context });

        Assert.Equal(new[] { "UNIT_A" }, scope.GetOverseenOrganizationalUnits());
    }

    [Fact]
    public async Task LDRSEC_003_expired_assignment_is_not_authorized()
    {
        await using var fixture = await SecurityFixture.CreateAsync(expired: true);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, fixture.Username),
            new Claim(ClaimTypes.Role, "Leader")
        }, "test");
        var context = fixture.CreateHttpContext(new ClaimsPrincipal(identity), null);
        var scope = new LeadershipScopeService(fixture.Context, new HttpContextAccessor { HttpContext = context });

        Assert.Empty(scope.GetOverseenOrganizationalUnits());
        Assert.False(scope.HasLeadershipAccess());
        Assert.False(scope.CanAccessDepartment("UNIT_A"));
    }

    private sealed class SecurityFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public string Username { get; } = "leader-security";

        private SecurityFixture(SqliteConnection connection, ApplicationDbContext context) => (_connection, Context) = (connection, context);

        public static async Task<SecurityFixture> CreateAsync(bool expired = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            var user = new User { Username = "leader-security", Role = "Leader", Email = "leader-security@example.test", CanLogin = true };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            context.LeaderAssignments.Add(new LeaderAssignment
            {
                UserId = user.Id,
                OrganizationalUnit = "UNIT_A",
                EffectiveFrom = System.DateTime.Now.AddDays(-2),
                EffectiveTo = expired ? System.DateTime.Now.AddMinutes(-1) : null
            });
            await context.SaveChangesAsync();
            return new SecurityFixture(connection, context);
        }

        public DefaultHttpContext CreateHttpContext(ClaimsPrincipal user, IDictionary<string, string>? values)
        {
            var context = new DefaultHttpContext { User = user, Session = new TestSession(values) };
            return context;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();
        public TestSession(IDictionary<string, string>? values)
        {
            if (values == null) return;
            foreach (var pair in values)
                _values[pair.Key] = System.Text.Encoding.UTF8.GetBytes(pair.Value);
        }
        public bool IsAvailable => true;
        public IEnumerable<string> Keys => _values.Keys;
        public string Id => "security-test";
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[]? value) => _values.TryGetValue(key, out value);
    }
}
