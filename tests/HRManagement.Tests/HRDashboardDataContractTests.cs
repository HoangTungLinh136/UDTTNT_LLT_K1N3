using giao_dien_demo.Controllers;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRManagement.Tests;

public sealed class HRDashboardDataContractTests
{
    [Fact]
    public async Task HRDATA_002_003_present_count_uses_active_employee_lifecycle()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        var active = new Employee { EmployeeCode = "HRD-A", Name = "Active", Status = "Active", Department = "HR" };
        var probation = new Employee { EmployeeCode = "HRD-P", Name = "Probation", Status = "Probation", Department = "HR" };
        var inactive = new Employee { EmployeeCode = "HRD-I", Name = "Inactive", Status = "Offboarded", Department = "HR" };
        fixture.Context.Employees.AddRange(active, probation, inactive);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Attendances.AddRange(
            new Attendance { EmployeeId = active.Id, EmployeeNameSnapshot = active.Name, CheckIn = DateTime.Today.AddHours(8) },
            new Attendance { EmployeeId = inactive.Id, EmployeeNameSnapshot = inactive.Name, CheckIn = DateTime.Today.AddHours(8).AddMinutes(5) });
        await fixture.Context.SaveChangesAsync();

        var model = await fixture.ReadModelAsync();

        Assert.Equal(2, model.ActiveEmployeeCount);
        Assert.Equal(1, model.PresentTodayCount);
        Assert.Contains(model.Departments, x => x.Department == "HR" && x.Employees == 2);
    }

    [Fact]
    public async Task HRDATA_006_contract_queue_counts_only_hr_review_state()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        fixture.Context.Contracts.AddRange(
            new Contract { EmployeeNameSnapshot = "Awaiting HR", Status = "Đã ký (Chờ duyệt cuối)", CreatedAt = DateTime.Now },
            new Contract { EmployeeNameSnapshot = "Awaiting employee", Status = "Chờ nhân viên ký", CreatedAt = DateTime.Now },
            new Contract { EmployeeNameSnapshot = "Legacy pending", Status = "Chờ duyệt", CreatedAt = DateTime.Now });
        await fixture.Context.SaveChangesAsync();

        var model = await fixture.ReadModelAsync();

        Assert.Equal(1, model.PendingActionCount);
        Assert.Single(model.PendingWork);
        Assert.Equal("Hợp đồng", model.PendingWork[0].Type);
        Assert.Equal("Đã ký (Chờ duyệt cuối)", model.PendingWork[0].Status);
    }

    private sealed class DashboardFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }

        private DashboardFixture(SqliteConnection connection, ApplicationDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public static async Task<DashboardFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new DashboardFixture(connection, context);
        }

        public async Task<HRWorkspaceViewModel> ReadModelAsync()
        {
            var controller = new HRController(Context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var result = await controller.Index();
            return Assert.IsType<HRWorkspaceViewModel>(Assert.IsType<ViewResult>(result).Model);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
