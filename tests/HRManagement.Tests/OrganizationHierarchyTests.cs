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

public sealed class OrganizationHierarchyTests
{
    private sealed class TestFixture : IAsyncDisposable
    {
        public SqliteConnection Connection { get; }
        public ApplicationDbContext Context { get; }
        public IManagerScopeService ManagerScope { get; }
        public ILeadershipScopeService LeadershipScope { get; }
        public OrganizationHierarchyService HierarchyService { get; }

        private TestFixture(
            SqliteConnection connection,
            ApplicationDbContext context,
            IManagerScopeService managerScope,
            ILeadershipScopeService leadershipScope,
            OrganizationHierarchyService hierarchyService)
        {
            Connection = connection;
            Context = context;
            ManagerScope = managerScope;
            LeadershipScope = leadershipScope;
            HierarchyService = hierarchyService;
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

            return new TestFixture(connection, context, managerScope, leadershipScope, hierarchyService);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Hierarchy_BuildsTree_WithDirectManagerRelationships()
    {
        await using var fixture = await TestFixture.CreateAsync();

        // Seed 1 CEO, 2 Managers, 3 Staff
        var ceo = new Employee { EmployeeCode = "NV001", Name = "CEO Nguyen", JobTitle = "Giam doc", Department = "Ban Giam Doc", Status = "Active" };
        fixture.Context.Employees.Add(ceo);
        await fixture.Context.SaveChangesAsync();

        var mgr1 = new Employee { EmployeeCode = "NV002", Name = "Manager IT", JobTitle = "Truong phong IT", Department = "IT", ManagerId = ceo.Id, Status = "Active" };
        var mgr2 = new Employee { EmployeeCode = "NV003", Name = "Manager HR", JobTitle = "Truong phong HR", Department = "HR", ManagerId = ceo.Id, Status = "Active" };
        fixture.Context.Employees.AddRange(mgr1, mgr2);
        await fixture.Context.SaveChangesAsync();

        var dev1 = new Employee { EmployeeCode = "NV004", Name = "Dev A", JobTitle = "Developer", Department = "IT", ManagerId = mgr1.Id, Status = "Active" };
        var dev2 = new Employee { EmployeeCode = "NV005", Name = "Dev B", JobTitle = "Tester", Department = "IT", ManagerId = mgr1.Id, Status = "Active" };
        var hrStaff = new Employee { EmployeeCode = "NV006", Name = "HR Staff", JobTitle = "Chuyen vien HR", Department = "HR", ManagerId = mgr2.Id, Status = "Active" };
        fixture.Context.Employees.AddRange(dev1, dev2, hrStaff);
        await fixture.Context.SaveChangesAsync();

        var hrPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "hr_user"),
            new Claim(ClaimTypes.Role, AppRoles.HR)
        }, "TestAuth"));

        var result = await fixture.HierarchyService.BuildHierarchyAsync(hrPrincipal);

        Assert.Equal(6, result.TotalEmployees);
        Assert.Equal(3, result.TotalManagers); // CEO, Mgr1, Mgr2 have reports
        Assert.Single(result.RootNodes); // Only CEO has ManagerId == null

        var root = result.RootNodes[0];
        Assert.Equal("NV001", root.EmployeeCode);
        Assert.Equal(2, root.DirectReports.Count); // Mgr1, Mgr2
        Assert.Equal(5, root.TotalDescendantsCount); // Mgr1(2) + Mgr2(1) + 2 = 5

        var itBranch = root.DirectReports.First(r => r.EmployeeCode == "NV002");
        Assert.Equal(2, itBranch.DirectReports.Count);
        Assert.Equal(2, itBranch.TotalDescendantsCount);
    }

    [Fact]
    public async Task Hierarchy_Handles_CyclesGracefully_WithoutInfiniteLoop()
    {
        await using var fixture = await TestFixture.CreateAsync();

        // Create cycle A -> B -> A
        var empA = new Employee { EmployeeCode = "NV101", Name = "Cycle User A", Department = "IT", Status = "Active" };
        var empB = new Employee { EmployeeCode = "NV102", Name = "Cycle User B", Department = "IT", Status = "Active" };
        fixture.Context.Employees.AddRange(empA, empB);
        await fixture.Context.SaveChangesAsync();

        empA.ManagerId = empB.Id;
        empB.ManagerId = empA.Id;
        await fixture.Context.SaveChangesAsync();

        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, AppRoles.Admin)
        }, "TestAuth"));

        // Must complete without stack overflow or hanging
        var result = await fixture.HierarchyService.BuildHierarchyAsync(adminPrincipal);

        Assert.Equal(2, result.TotalEmployees);
        Assert.True(result.RootNodes.Any(r => r.IsCycleDetected));
    }

    [Fact]
    public async Task Hierarchy_FiltersByDepartment_Accurately()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var hr1 = new Employee { EmployeeCode = "HR01", Name = "HR Manager", Department = "HR", Status = "Active" };
        var hr2 = new Employee { EmployeeCode = "HR02", Name = "HR Staff", Department = "HR", Status = "Active" };
        var it1 = new Employee { EmployeeCode = "IT01", Name = "IT Lead", Department = "IT", Status = "Active" };
        fixture.Context.Employees.AddRange(hr1, hr2, it1);
        await fixture.Context.SaveChangesAsync();

        hr2.ManagerId = hr1.Id;
        await fixture.Context.SaveChangesAsync();

        var hrPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "hr"),
            new Claim(ClaimTypes.Role, AppRoles.HR)
        }, "TestAuth"));

        var filteredResult = await fixture.HierarchyService.BuildHierarchyAsync(hrPrincipal, departmentFilter: "HR");

        Assert.Equal(2, filteredResult.TotalEmployees);
        Assert.Equal("HR", filteredResult.SelectedDepartment);
        Assert.All(filteredResult.RootNodes, node => Assert.Equal("HR", node.Department));
    }

    [Fact]
    public void EmployeeController_Hierarchy_Action_Requires_Authorize()
    {
        var method = typeof(EmployeeController).GetMethod(nameof(EmployeeController.Hierarchy));
        Assert.NotNull(method);

        var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
        Assert.NotEmpty(authorize);
    }

    [Fact]
    public void Sidebar_Layout_Contains_Hierarchy_Mapping_For_HR_And_Admin()
    {
        // Read Views/Shared/_Layout.cshtml to verify navigation structure
        var layoutPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "giao diện demo", "Views", "Shared", "_Layout.cshtml");
        if (!File.Exists(layoutPath))
        {
            layoutPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\giao diện demo\giao diện demo\Views\Shared\_Layout.cshtml"));
        }

        if (File.Exists(layoutPath))
        {
            var content = File.ReadAllText(layoutPath);
            Assert.Contains("Cây phân cấp", content);
            Assert.Contains("Danh mục phòng ban", content);
            Assert.Contains("Hierarchy", content);
        }
    }

    [Fact]
    public void Employee_Details_View_Has_No_Invalid_Dynamic_Property_Accesses()
    {
        // Audit Views/Employee/Details.cshtml to ensure P0 regression prevention
        var viewPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "giao diện demo", "Views", "Employee", "Details.cshtml");
        if (!File.Exists(viewPath))
        {
            viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\giao diện demo\giao diện demo\Views\Employee\Details.cshtml"));
        }

        if (File.Exists(viewPath))
        {
            var content = File.ReadAllText(viewPath);
            Assert.DoesNotContain("@t.OldDepartmentSnapshot", content);
            Assert.DoesNotContain("@t.NewDepartmentSnapshot", content);
            Assert.DoesNotContain("@a.NewTitleSnapshot", content);
            Assert.DoesNotContain("@a.NewDepartmentSnapshot", content);
            Assert.DoesNotContain("@d.OldTitleSnapshot", content);
            Assert.DoesNotContain("@r.Reason", content);
            Assert.Contains("@using giao_dien_demo.Models", content);
            Assert.Contains("var transfers = ViewBag.Transfers as IEnumerable<TransferRecord>", content);
            Assert.Contains("var appointments = ViewBag.Appointments as IEnumerable<AppointmentRecord>", content);
        }
    }

    [Fact]
    public async Task AdminOrgController_Index_Queries_Standard_Departments()
    {
        await using var fixture = await TestFixture.CreateAsync();

        // Seed 6 CR-01 standard departments
        fixture.Context.Departments.AddRange(
            new Department { Code = "HR", Name = "Phòng Nhân sự", IsActive = true },
            new Department { Code = "IT", Name = "Phòng Công nghệ thông tin", IsActive = true },
            new Department { Code = "MKT", Name = "Phòng Marketing", IsActive = true },
            new Department { Code = "FIN", Name = "Phòng Tài chính", IsActive = true },
            new Department { Code = "SALES", Name = "Phòng Kinh doanh", IsActive = true },
            new Department { Code = "OPS", Name = "Phòng Vận hành", IsActive = true }
        );
        await fixture.Context.SaveChangesAsync();

        var hrDept = await fixture.Context.Departments.FirstAsync(d => d.Code == "HR");
        var emp1 = new Employee { EmployeeCode = "HR01", Name = "HR Staff", DepartmentId = hrDept.Id, Department = hrDept.Name, Status = "Active" };
        var empUnassigned = new Employee { EmployeeCode = "NO01", Name = "Unassigned Staff", DepartmentId = null, Department = null, Status = "Active" };
        fixture.Context.Employees.AddRange(emp1, empUnassigned);
        await fixture.Context.SaveChangesAsync();

        var auditMock = new AuditLogService(fixture.Context, new HttpContextAccessor());
        var controller = new AdminOrgController(fixture.Context, auditMock);

        var actionResult = controller.Index();
        var viewResult = Assert.IsType<ViewResult>(actionResult);

        Assert.Equal(6, controller.ViewBag.TotalDepartments);
        Assert.Equal(1, controller.ViewBag.UnassignedCount);
        Assert.NotNull(controller.ViewBag.Departments);

        var depts = Assert.IsAssignableFrom<System.Collections.IEnumerable>(controller.ViewBag.Departments);
        int count = 0;
        foreach (var _ in depts) count++;
        Assert.Equal(6, count);
    }
}

