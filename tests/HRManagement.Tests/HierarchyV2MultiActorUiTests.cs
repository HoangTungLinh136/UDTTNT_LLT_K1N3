using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;

namespace HRManagement.Tests
{
    public class HierarchyV2MultiActorUiTests
    {
        private sealed class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _store = new();
            public bool IsAvailable => true;
            public string Id => "test-session-id";
            public IEnumerable<string> Keys => _store.Keys;

            public void Clear() => _store.Clear();
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Remove(string key) => _store.Remove(key);
            public void Set(string key, byte[] value) => _store[key] = value;
            public bool TryGetValue(string key, out byte[]? value) => _store.TryGetValue(key, out value);
        }

        private sealed class TestFixture : IAsyncDisposable
        {
            public SqliteConnection Connection { get; }
            public ApplicationDbContext Context { get; }

            private TestFixture(SqliteConnection connection, ApplicationDbContext context)
            {
                Connection = connection;
                Context = context;
            }

            public static async Task<TestFixture> CreateAsync()
            {
                var connection = new SqliteConnection("DataSource=:memory:");
                await connection.OpenAsync();

                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connection)
                    .Options;

                var context = new ApplicationDbContext(options);
                await context.Database.EnsureCreatedAsync();

                return new TestFixture(connection, context);
            }

            public async ValueTask DisposeAsync()
            {
                await Context.DisposeAsync();
                await Connection.DisposeAsync();
            }
        }

        private static string ResolveViewPath(string relativePath)
        {
            var baseDir = AppContext.BaseDirectory;
            var current = new DirectoryInfo(baseDir);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "giao diện demo", relativePath);
                if (File.Exists(candidate)) return candidate;
                var directCandidate = Path.Combine(current.FullName, relativePath);
                if (File.Exists(directCandidate)) return directCandidate;
                current = current.Parent;
            }
            return Path.Combine(baseDir, relativePath);
        }

        private static ClaimsPrincipal CreatePrincipal(string role, string name = "test.user")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.NameIdentifier, name),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public void HIER_V2_01_Shared_Views_And_Partials_Exist()
        {
            var mainView = ResolveViewPath(Path.Combine("Views", "Employee", "Hierarchy.cshtml"));
            var treePartial = ResolveViewPath(Path.Combine("Views", "Employee", "_HierarchyTopDownTreePartial.cshtml"));
            var drawerPartial = ResolveViewPath(Path.Combine("Views", "Employee", "_HierarchyDrawerPartial.cshtml"));

            Assert.True(File.Exists(mainView), $"Hierarchy.cshtml not found at {mainView}");
            Assert.True(File.Exists(treePartial), $"_HierarchyTopDownTreePartial.cshtml not found at {treePartial}");
            Assert.True(File.Exists(drawerPartial), $"_HierarchyDrawerPartial.cshtml not found at {drawerPartial}");
        }

        [Fact]
        public void HIER_V2_02_HierarchyView_Contains_Header_Stats_Toolbar_And_Drawer()
        {
            var mainView = ResolveViewPath(Path.Combine("Views", "Employee", "Hierarchy.cshtml"));
            var content = File.ReadAllText(mainView);

            // Title & Subtitle
            Assert.Contains("Cơ cấu tổ chức &amp; Cây phân cấp quản lý trực tiếp", content);
            Assert.Contains("OD-07 &amp; OD-11", content);

            // 4 Stat Cards
            Assert.Contains("Nhân sự trong phạm vi", content);
            Assert.Contains("Cán bộ có cấp dưới", content);
            Assert.Contains("Nhánh quản lý", content);
            Assert.Contains("Chưa gán quản lý trực tiếp", content);

            // Toolbar controls
            Assert.Contains("hierarchySearchInput", content);
            Assert.Contains("toggleDepartmentView", content);
            Assert.Contains("Mở rộng tất cả", content);
            Assert.Contains("Thu gọn tất cả", content);

            // Integration of Drawer
            Assert.Contains("_HierarchyDrawerPartial", content);
        }

        [Fact]
        public void HIER_V2_03_Drawer_Contains_Three_Tabs_And_Footer_Actions()
        {
            var drawerPartial = ResolveViewPath(Path.Combine("Views", "Employee", "_HierarchyDrawerPartial.cshtml"));
            var content = File.ReadAllText(drawerPartial);

            // 3 Tabs
            Assert.Contains("Thông tin chung", content);
            Assert.Contains("Cấp dưới", content);
            Assert.Contains("Lịch sử quản lý", content);

            // Footer buttons
            Assert.Contains("Xem hồ sơ chi tiết", content);
            Assert.Contains("Xem lịch sử quản lý", content);
        }

        [Fact]
        public async Task HIER_V2_04_Admin_And_HR_Build_Full_Company_Tree_With_History_And_Permissions()
        {
            await using var fixture = await TestFixture.CreateAsync();

            var ceo = new Employee { EmployeeCode = "CEO01", Name = "CEO Long", Department = "Phòng Nhân sự", Status = "Active" };
            fixture.Context.Employees.Add(ceo);
            await fixture.Context.SaveChangesAsync();

            var mgr = new Employee { EmployeeCode = "MGR01", Name = "Manager Mai", Department = "Phòng CNTT", ManagerId = ceo.Id, Status = "Active" };
            fixture.Context.Employees.Add(mgr);
            await fixture.Context.SaveChangesAsync();

            var emp = new Employee { EmployeeCode = "EMP01", Name = "Staff Lan", Department = "Phòng CNTT", ManagerId = mgr.Id, Status = "Active" };
            fixture.Context.Employees.Add(emp);
            await fixture.Context.SaveChangesAsync();

            var userAdmin = new User { Username = "linh.admin", Role = AppRoles.Admin, EmployeeId = ceo.Id, CanLogin = true };
            fixture.Context.Users.Add(userAdmin);
            await fixture.Context.SaveChangesAsync();

            var httpContext = new DefaultHttpContext();
            var mockSession = new TestSession();
            httpContext.Session = mockSession;
            var accessor = new HttpContextAccessor { HttpContext = httpContext };

            var leadershipScope = new LeadershipScopeService(fixture.Context, accessor);
            var managerScope = new ManagerScopeService(fixture.Context, accessor);
            var service = new OrganizationHierarchyService(fixture.Context, managerScope, leadershipScope);

            var principal = CreatePrincipal(AppRoles.Admin, "linh.admin");
            httpContext.User = principal;

            var model = await service.BuildHierarchyAsync(principal);

            Assert.Equal(3, model.TotalEmployees);
            Assert.Equal(2, model.TotalManagers); // ceo and mgr
            Assert.Single(model.RootNodes);
            Assert.True(model.RootNodes[0].CanViewDetails);
            Assert.True(model.RootNodes[0].CanManageManager);
            Assert.Contains("Toàn công ty", model.DataScopeDescription);
        }

        [Fact]
        public async Task HIER_V2_05_Leader_Scope_Restricts_Employees_To_Overseen_Units()
        {
            await using var fixture = await TestFixture.CreateAsync();

            var eHR = new Employee { EmployeeCode = "E_HR", Name = "HR Emp", Department = "Phòng Nhân sự", Status = "Active" };
            var eIT = new Employee { EmployeeCode = "E_IT", Name = "IT Emp", Department = "Phòng CNTT", Status = "Active" };
            fixture.Context.Employees.AddRange(eHR, eIT);
            await fixture.Context.SaveChangesAsync();

            var leaderUser = new User { Username = "leader1", Role = AppRoles.Leader, CanLogin = true };
            fixture.Context.Users.Add(leaderUser);
            await fixture.Context.SaveChangesAsync();

            var assignment = new LeaderAssignment
            {
                UserId = leaderUser.Id,
                OrganizationalUnit = "Phòng Nhân sự",
                EffectiveFrom = DateTime.Now.AddDays(-1),
                EffectiveTo = null,
                AssignedAt = DateTime.Now
            };
            fixture.Context.LeaderAssignments.Add(assignment);
            await fixture.Context.SaveChangesAsync();

            var httpContext = new DefaultHttpContext();
            var mockSession = new TestSession();
            httpContext.Session = mockSession;
            var accessor = new HttpContextAccessor { HttpContext = httpContext };

            var leadershipScope = new LeadershipScopeService(fixture.Context, accessor);
            var managerScope = new ManagerScopeService(fixture.Context, accessor);
            var service = new OrganizationHierarchyService(fixture.Context, managerScope, leadershipScope);

            var principal = CreatePrincipal(AppRoles.Leader, "leader1");
            httpContext.User = principal;

            var model = await service.BuildHierarchyAsync(principal);

            // Leader can only see Phòng Nhân sự employee
            Assert.Contains(model.RootNodes, n => n.EmployeeCode == "E_HR");
            Assert.DoesNotContain(model.RootNodes, n => n.EmployeeCode == "E_IT");
            Assert.False(model.RootNodes.First().CanManageManager);
        }

        [Fact]
        public async Task HIER_V2_06_Unassigned_Employees_Are_Identified_Accurately()
        {
            await using var fixture = await TestFixture.CreateAsync();

            var ceo = new Employee { EmployeeCode = "CEO02", Name = "CEO Two", Department = "Ban Giám đốc", Status = "Active" };
            fixture.Context.Employees.Add(ceo);
            await fixture.Context.SaveChangesAsync();

            var child = new Employee { EmployeeCode = "CH01", Name = "Child Emp", Department = "Ban Giám đốc", ManagerId = ceo.Id, Status = "Active" };
            var unassigned1 = new Employee { EmployeeCode = "UN01", Name = "Unassigned One", Department = "Phòng Khác", ManagerId = null, Status = "Active" };
            var unassigned2 = new Employee { EmployeeCode = "UN02", Name = "Unassigned Two", Department = "Phòng Khác", ManagerId = null, Status = "Active" };
            fixture.Context.Employees.AddRange(child, unassigned1, unassigned2);
            await fixture.Context.SaveChangesAsync();

            var httpContext = new DefaultHttpContext();
            var accessor = new HttpContextAccessor { HttpContext = httpContext };
            var leadershipScope = new LeadershipScopeService(fixture.Context, accessor);
            var managerScope = new ManagerScopeService(fixture.Context, accessor);
            var service = new OrganizationHierarchyService(fixture.Context, managerScope, leadershipScope);

            var principal = CreatePrincipal(AppRoles.Admin, "admin_user");
            httpContext.User = principal;

            var model = await service.BuildHierarchyAsync(principal);

            Assert.Equal(4, model.TotalEmployees);
            Assert.Equal(1, model.TotalManagers); // only CEO
            Assert.Equal(2, model.TotalUnassigned); // UN01 and UN02
            Assert.Equal(2, model.StandaloneNodes.Count);
        }

        [Fact]
        public async Task HIER_V2_07_Manager_Scope_Enforcement_Blocks_Cross_Department_Leak()
        {
            await using var fixture = await TestFixture.CreateAsync();

            var mgr = new Employee { EmployeeCode = "MGR_IT", Name = "IT Manager", Department = "Phòng CNTT", Status = "Active" };
            fixture.Context.Employees.Add(mgr);
            await fixture.Context.SaveChangesAsync();

            var staffIT = new Employee { EmployeeCode = "STAFF_IT", Name = "IT Staff", Department = "Phòng CNTT", ManagerId = mgr.Id, Status = "Active" };
            var staffHR = new Employee { EmployeeCode = "STAFF_HR", Name = "HR Staff", Department = "Phòng Nhân sự", Status = "Active" };
            fixture.Context.Employees.AddRange(staffIT, staffHR);
            await fixture.Context.SaveChangesAsync();

            var mgrUser = new User { Username = "mgr_it_user", Role = AppRoles.Manager, EmployeeId = mgr.Id, CanLogin = true };
            fixture.Context.Users.Add(mgrUser);
            await fixture.Context.SaveChangesAsync();

            // Assign Manager to "Phòng CNTT"
            var assignment = new ManagerAssignment
            {
                UserId = mgrUser.Id,
                OrganizationalUnit = "Phòng CNTT",
                EffectiveFrom = DateTime.Now.AddDays(-1),
                EffectiveTo = null,
                AssignedAt = DateTime.Now
            };
            fixture.Context.ManagerAssignments.Add(assignment);
            await fixture.Context.SaveChangesAsync();

            var httpContext = new DefaultHttpContext();
            var mockSession = new TestSession();
            httpContext.Session = mockSession;
            var accessor = new HttpContextAccessor { HttpContext = httpContext };

            var leadershipScope = new LeadershipScopeService(fixture.Context, accessor);
            var managerScope = new ManagerScopeService(fixture.Context, accessor);
            var service = new OrganizationHierarchyService(fixture.Context, managerScope, leadershipScope);

            var principal = CreatePrincipal(AppRoles.Manager, "mgr_it_user");
            httpContext.User = principal;

            var model = await service.BuildHierarchyAsync(principal);

            Assert.Equal(2, model.TotalEmployees); // Only mgr and staffIT
            Assert.Contains(model.RootNodes, n => n.EmployeeCode == "MGR_IT");
            Assert.DoesNotContain(model.RootNodes, n => n.EmployeeCode == "STAFF_HR");
            Assert.False(model.RootNodes.First().CanManageManager);
        }
    }
}
