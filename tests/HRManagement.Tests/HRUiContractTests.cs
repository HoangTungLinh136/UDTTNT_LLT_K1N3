using giao_dien_demo.Authorization;
using giao_dien_demo.Controllers;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRManagement.Tests;

public sealed class HRUiContractTests
{
    [Fact]
    public async Task HRUI_001_002_003_004_005_006_employee_query_filters_before_pagination()
    {
        await using var fixture = await Fixture.CreateAsync();
        for (var i = 1; i <= 12; i++)
        {
            fixture.Context.Employees.Add(new Employee
            {
                EmployeeCode = $"NV{i:000}",
                Name = $"Employee {i:000}",
                Department = i <= 11 ? "HR" : "IT",
                Status = i == 11 ? "Probation" : "Active"
            });
        }
        await fixture.Context.SaveChangesAsync();

        var controller = fixture.CreateController();
        var result = await controller.Index("Employee", "HR", "All", page: 2);
        var employees = Assert.IsAssignableFrom<IEnumerable<Employee>>(Assert.IsType<ViewResult>(result).Model).ToList();

        Assert.Equal(11, (int)controller.ViewBag.TotalItems);
        Assert.Equal(2, (int)controller.ViewBag.PageNumber);
        Assert.Equal(10, (int)controller.ViewBag.PageSize);
        Assert.Single(employees);
        Assert.Equal("NV011", employees[0].EmployeeCode);
    }

    [Fact]
    public void HRUI_001_employee_list_is_hr_only_and_uses_canonical_employee_code()
    {
        var authorize = typeof(EmployeeController).GetMethod(nameof(EmployeeController.Index))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(AppPolicies.HROnly, authorize.Policy);
        Assert.NotNull(typeof(Employee).GetProperty(nameof(Employee.EmployeeCode)));
    }

    [Fact]
    public void HRUI_013_014_training_and_benefit_have_dedicated_hr_surfaces()
    {
        foreach (var controllerType in new[] { typeof(TrainingController), typeof(BenefitController) })
        {
            var action = controllerType.GetMethod("HrIndex");
            Assert.NotNull(action);
            var authorize = action!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Single();
            Assert.Equal(AppPolicies.HROnly, authorize.Policy);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }

        private Fixture(SqliteConnection connection, ApplicationDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new Fixture(connection, context);
        }

        public EmployeeController CreateController() => new(
            Context,
            hubContext: null!,
            hostEnvironment: null!,
            auditLogService: null!,
            managerAssignmentService: null!,
            employeeCodeAssignmentService: null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
