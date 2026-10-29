using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using giao_dien_demo.Authorization;
using giao_dien_demo.Controllers;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRManagement.Tests;

public sealed class EmployeeHierarchySecurityTests
{
    private sealed class TestFixture : IAsyncDisposable
    {
        public SqliteConnection Connection { get; }
        public ApplicationDbContext Context { get; }
        public IManagerScopeService ManagerScope { get; }
        public ILeadershipScopeService LeadershipScope { get; }
        public OrganizationHierarchyService HierarchyService { get; }
        public EmployeeController EmployeeController { get; }

        private TestFixture(
            SqliteConnection connection,
            ApplicationDbContext context,
            IManagerScopeService managerScope,
            ILeadershipScopeService leadershipScope,
            OrganizationHierarchyService hierarchyService,
            EmployeeController employeeController)
        {
            Connection = connection;
            Context = context;
            ManagerScope = managerScope;
            LeadershipScope = leadershipScope;
            HierarchyService = hierarchyService;
            EmployeeController = employeeController;
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

            var httpContextAccessor = new HttpContextAccessor();
            var managerScope = new ManagerScopeService(context, httpContextAccessor);
            var leadershipScope = new LeadershipScopeService(context, httpContextAccessor);
            var hierarchyService = new OrganizationHierarchyService(context, managerScope, leadershipScope);

            var auditLog = new AuditLogService(context, httpContextAccessor);
            var directMgrService = new DirectManagerService(context, auditLog);
            var employeeController = new EmployeeController(
                context,
                null!,
                null!,
                auditLog,
                null!,
                null,
                directMgrService,
                hierarchyService);

            return new TestFixture(connection, context, managerScope, leadershipScope, hierarchyService, employeeController);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private static string ResolveViewPath(string relativeViewPath)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\giao diện demo", relativeViewPath)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\giao diện demo", relativeViewPath)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\giao diện demo", relativeViewPath))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        return candidates[0];
    }

    [Fact]
    public void EMP_HIER_01_Sidebar_Layout_Contains_Hierarchy_Mapping_For_Employee()
    {
        var layoutPath = ResolveViewPath(Path.Combine("Views", "Shared", "_Layout.cshtml"));
        Assert.True(File.Exists(layoutPath), $"Layout file not found at {layoutPath}");
        var content = File.ReadAllText(layoutPath);

        // Verify Employee sidebar section has Cây phân cấp
        Assert.Contains("@if (majorModule == \"Employee\")", content);
        Assert.Contains("Url.Action(\"Hierarchy\", \"Employee\")", content);
        Assert.Contains("fa-diagram-project", content);
        Assert.Contains("Cây phân cấp", content);
    }

    [Fact]
    public async Task EMP_HIER_02_EmployeeController_Hierarchy_Action_Is_Accessible_By_Authenticated_Employee()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var emp = new Employee { EmployeeCode = "NV010", Name = "Nguyen Van A", Department = "IT", Status = "Active" };
        fixture.Context.Employees.Add(emp);
        await fixture.Context.SaveChangesAsync();

        var user = new User { Username = "emp.a", Role = AppRoles.Employee, EmployeeId = emp.Id, CanLogin = true };
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "emp.a"),
            new Claim(ClaimTypes.Name, "Nguyen Van A"),
            new Claim(ClaimTypes.Role, AppRoles.Employee)
        }, "TestAuth"));

        fixture.EmployeeController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var result = await fixture.EmployeeController.Hierarchy();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrganizationHierarchyViewModel>(viewResult.Model);

        Assert.Equal(AppRoles.Employee, model.CurrentUserRole);
        Assert.Equal(1, model.TotalEmployees);
    }

    [Fact]
    public async Task EMP_HIER_03_Employee_Only_Receives_Scoped_Data_Self_And_Direct_Manager()
    {
        await using var fixture = await TestFixture.CreateAsync();

        // Seed 1 CEO, 2 Managers, 5 Employees
        var ceo = new Employee { EmployeeCode = "NV001", Name = "CEO", Department = "BOD", Status = "Active" };
        fixture.Context.Employees.Add(ceo);
        await fixture.Context.SaveChangesAsync();

        var itMgr = new Employee { EmployeeCode = "NV002", Name = "IT Manager", Department = "IT", ManagerId = ceo.Id, Status = "Active" };
        var hrMgr = new Employee { EmployeeCode = "NV003", Name = "HR Manager", Department = "HR", ManagerId = ceo.Id, Status = "Active" };
        fixture.Context.Employees.AddRange(itMgr, hrMgr);
        await fixture.Context.SaveChangesAsync();

        var dev1 = new Employee { EmployeeCode = "NV004", Name = "Dev 1", Department = "IT", ManagerId = itMgr.Id, Status = "Active" };
        var dev2 = new Employee { EmployeeCode = "NV005", Name = "Dev 2", Department = "IT", ManagerId = itMgr.Id, Status = "Active" };
        var hrStaff = new Employee { EmployeeCode = "NV006", Name = "HR Staff", Department = "HR", ManagerId = hrMgr.Id, Status = "Active" };
        fixture.Context.Employees.AddRange(dev1, dev2, hrStaff);
        await fixture.Context.SaveChangesAsync();

        var dev1User = new User { Username = "dev1", Role = AppRoles.Employee, EmployeeId = dev1.Id, CanLogin = true };
        fixture.Context.Users.Add(dev1User);
        await fixture.Context.SaveChangesAsync();

        var dev1Principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "dev1"),
            new Claim(ClaimTypes.Role, AppRoles.Employee)
        }, "TestAuth"));

        var model = await fixture.HierarchyService.BuildHierarchyAsync(dev1Principal);

        // Scope MUST contain strictly dev1 and itMgr (2 employees total)
        Assert.Equal(2, model.TotalEmployees);
        Assert.Equal(dev1.Id, model.CurrentEmployeeId);

        // dev2, hrMgr, hrStaff, and ceo must NOT be included in the employee hierarchy
        var allIdsInTree = new HashSet<int>();
        void CollectIds(HierarchyNodeViewModel node)
        {
            allIdsInTree.Add(node.Id);
            foreach (var child in node.DirectReports) CollectIds(child);
        }
        foreach (var root in model.RootNodes) CollectIds(root);

        Assert.Contains(dev1.Id, allIdsInTree);
        Assert.Contains(itMgr.Id, allIdsInTree);
        Assert.DoesNotContain(dev2.Id, allIdsInTree);
        Assert.DoesNotContain(hrStaff.Id, allIdsInTree);
        Assert.DoesNotContain(hrMgr.Id, allIdsInTree);
        Assert.DoesNotContain(ceo.Id, allIdsInTree);
    }

    [Fact]
    public async Task EMP_HIER_04_Employee_Cannot_View_Full_Organization_Tree()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var ceo = new Employee { EmployeeCode = "NV001", Name = "CEO", Department = "BOD", Status = "Active" };
        fixture.Context.Employees.Add(ceo);
        await fixture.Context.SaveChangesAsync();

        var dev = new Employee { EmployeeCode = "NV002", Name = "Dev", Department = "IT", ManagerId = ceo.Id, Status = "Active" };
        var other = new Employee { EmployeeCode = "NV003", Name = "Other", Department = "Sales", Status = "Active" };
        fixture.Context.Employees.AddRange(dev, other);
        await fixture.Context.SaveChangesAsync();

        var devUser = new User { Username = "dev", Role = AppRoles.Employee, EmployeeId = dev.Id, CanLogin = true };
        var adminUser = new User { Username = "admin", Role = AppRoles.Admin, CanLogin = true };
        fixture.Context.Users.AddRange(devUser, adminUser);
        await fixture.Context.SaveChangesAsync();

        var devPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "dev"),
            new Claim(ClaimTypes.Role, AppRoles.Employee)
        }, "TestAuth"));

        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin"),
            new Claim(ClaimTypes.Role, AppRoles.Admin)
        }, "TestAuth"));

        var devModel = await fixture.HierarchyService.BuildHierarchyAsync(devPrincipal);
        var adminModel = await fixture.HierarchyService.BuildHierarchyAsync(adminPrincipal);

        Assert.Equal(3, adminModel.TotalEmployees);
        Assert.Equal(2, devModel.TotalEmployees); // Only dev + ceo
        Assert.True(devModel.TotalEmployees < adminModel.TotalEmployees);
    }

    [Fact]
    public async Task EMP_HIER_05_Department_QueryParameter_Cannot_Expand_Employee_Data_Scope()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var itEmp = new Employee { EmployeeCode = "NV01", Name = "IT Emp", Department = "IT", Status = "Active" };
        var salesEmp = new Employee { EmployeeCode = "NV02", Name = "Sales Emp", Department = "Sales", Status = "Active" };
        fixture.Context.Employees.AddRange(itEmp, salesEmp);
        await fixture.Context.SaveChangesAsync();

        var itUser = new User { Username = "itemp", Role = AppRoles.Employee, EmployeeId = itEmp.Id, CanLogin = true };
        fixture.Context.Users.Add(itUser);
        await fixture.Context.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "itemp"),
            new Claim(ClaimTypes.Role, AppRoles.Employee)
        }, "TestAuth"));

        // Attempting to filter by "Sales" while authenticated as IT Employee
        var model = await fixture.HierarchyService.BuildHierarchyAsync(principal, departmentFilter: "Sales");

        // Returns 0 employees, NEVER leaks Sales employees
        Assert.Equal(0, model.TotalEmployees);
        Assert.Empty(model.RootNodes);
    }

    [Fact]
    public void EMP_HIER_06_Search_Operates_Only_On_Scoped_Nodes_In_Razor()
    {
        var viewPath = ResolveViewPath(Path.Combine("Views", "Employee", "Hierarchy.cshtml"));
        Assert.True(File.Exists(viewPath), $"Hierarchy view not found at {viewPath}");
        var content = File.ReadAllText(viewPath);

        // Ensure search only operates on client-rendered node cards (which are server-scoped)
        Assert.Contains("hierarchySearchInput", content);
        Assert.Contains("querySelectorAll('.node-card')", content);
    }

    [Fact]
    public async Task EMP_HIER_07_Hierarchy_ViewModel_Does_Not_Leak_Restricted_Nodes()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var e1 = new Employee { EmployeeCode = "E01", Name = "User 1", Department = "DeptA", Status = "Active" };
        var e2 = new Employee { EmployeeCode = "E02", Name = "User 2", Department = "DeptB", Status = "Active" };
        fixture.Context.Employees.AddRange(e1, e2);
        await fixture.Context.SaveChangesAsync();

        var u1 = new User { Username = "u1", Role = AppRoles.Employee, EmployeeId = e1.Id, CanLogin = true };
        fixture.Context.Users.Add(u1);
        await fixture.Context.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "u1"),
            new Claim(ClaimTypes.Role, AppRoles.Employee)
        }, "TestAuth"));

        var model = await fixture.HierarchyService.BuildHierarchyAsync(principal);

        Assert.Single(model.RootNodes);
        Assert.Equal("E01", model.RootNodes[0].EmployeeCode);
        Assert.True(model.RootNodes[0].IsCurrentUser);
        Assert.DoesNotContain(model.RootNodes, n => n.EmployeeCode == "E02");
    }

    [Fact]
    public void EMP_HIER_08_Employee_Details_Action_Protected_By_HROnly_Policy()
    {
        var method = typeof(EmployeeController).GetMethod(nameof(EmployeeController.Details));
        Assert.NotNull(method);

        var authorizeAttr = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorizeAttr);
        Assert.Equal(AppPolicies.HROnly, authorizeAttr.Policy);
    }

    [Fact]
    public void EMP_HIER_09_Employee_Cannot_Call_ChangeDirectManager_Endpoint()
    {
        var method = typeof(EmployeeController).GetMethod(nameof(EmployeeController.ChangeDirectManager));
        Assert.NotNull(method);

        var authorizeAttr = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorizeAttr);
        Assert.Equal(AppPolicies.HROnly, authorizeAttr.Policy);
    }

    [Fact]
    public async Task EMP_HIER_10_User_Without_Valid_EmployeeId_Is_Handled_FailClosed()
    {
        await using var fixture = await TestFixture.CreateAsync();

        // User without EmployeeId
        var userNoEmp = new User { Username = "guest.emp", Role = AppRoles.Employee, EmployeeId = null, CanLogin = true };
        fixture.Context.Users.Add(userNoEmp);
        await fixture.Context.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "guest.emp"),
            new Claim(ClaimTypes.Role, AppRoles.Employee)
        }, "TestAuth"));

        var model = await fixture.HierarchyService.BuildHierarchyAsync(principal);

        Assert.Equal(0, model.TotalEmployees);
        Assert.Empty(model.RootNodes);
        Assert.Null(model.CurrentEmployeeId);
    }

    [Fact]
    public async Task EMP_HIER_11_Employee_With_Null_ManagerId_Displays_Safely_As_Standalone()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var emp = new Employee { EmployeeCode = "NV999", Name = "Lone Ranger", Department = "IT", ManagerId = null, Status = "Active" };
        fixture.Context.Employees.Add(emp);
        await fixture.Context.SaveChangesAsync();

        var user = new User { Username = "lone.ranger", Role = AppRoles.Employee, EmployeeId = emp.Id, CanLogin = true };
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "lone.ranger"),
            new Claim(ClaimTypes.Role, AppRoles.Employee)
        }, "TestAuth"));

        var model = await fixture.HierarchyService.BuildHierarchyAsync(principal);

        Assert.Equal(1, model.TotalEmployees);
        Assert.Equal(1, model.TotalUnassigned);
        Assert.Single(model.RootNodes);
        Assert.True(model.RootNodes[0].IsCurrentUser);
        Assert.False(model.RootNodes[0].ManagerId.HasValue);
    }

    [Fact]
    public async Task EMP_HIER_12_HR_Admin_Leader_Manager_Scopes_Remain_Intact()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var ceo = new Employee { EmployeeCode = "NV01", Name = "CEO", Department = "BOD", Status = "Active" };
        fixture.Context.Employees.Add(ceo);
        await fixture.Context.SaveChangesAsync();

        var hr = new Employee { EmployeeCode = "NV02", Name = "HR", Department = "HR", ManagerId = ceo.Id, Status = "Active" };
        fixture.Context.Employees.Add(hr);
        await fixture.Context.SaveChangesAsync();

        var hrUser = new User { Username = "hr_test", Role = AppRoles.HR, EmployeeId = hr.Id, CanLogin = true };
        fixture.Context.Users.Add(hrUser);
        await fixture.Context.SaveChangesAsync();

        var hrPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "hr_test"),
            new Claim(ClaimTypes.Role, AppRoles.HR)
        }, "TestAuth"));

        var hrModel = await fixture.HierarchyService.BuildHierarchyAsync(hrPrincipal);

        Assert.Equal(2, hrModel.TotalEmployees);
        Assert.Equal("Toàn công ty (Quyền Quản trị / Nhân sự)", hrModel.DataScopeDescription);
    }
}

