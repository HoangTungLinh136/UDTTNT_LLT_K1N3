using System.Security.Claims;
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

public sealed class EmployeeDashboardContractTests
{
    [Fact]
    public void EMPDASH_001_dashboard_requires_employee_role()
    {
        var authorize = typeof(NhanVienController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(AppRoles.Employee, authorize.Roles);
    }

    [Fact]
    public async Task EMPDASH_002_to_011_dashboard_uses_authenticated_employee_scope_for_all_metrics()
    {
        await using var fixture = await Fixture.CreateAsync();
        var mine = new Employee { EmployeeCode = "NV001", Name = "Mine", Department = "HR", Position = "Specialist", Status = "Active" };
        var other = new Employee { EmployeeCode = "NV002", Name = "Other", Department = "IT", Position = "Developer", Status = "Active" };
        fixture.Context.Employees.AddRange(mine, other);
        await fixture.Context.SaveChangesAsync();

        var user = new User { Username = "mine", FullName = mine.Name!, Email = "mine@example.test", Role = AppRoles.Employee, CanLogin = true, EmployeeId = mine.Id };
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();

        var today = DateTime.Now;
        fixture.Context.Attendances.AddRange(
            new Attendance { EmployeeId = mine.Id, EmployeeNameSnapshot = mine.Name, CheckIn = today.AddHours(-2), CheckOut = today },
            new Attendance { EmployeeId = other.Id, EmployeeNameSnapshot = other.Name, CheckIn = today.AddHours(-3), CheckOut = today });
        fixture.Context.Leaves.AddRange(
            new Leave { EmployeeId = mine.Id, EmployeeNameSnapshot = mine.Name, Status = "Chờ duyệt", FromDate = today.Date, ToDate = today.Date, Reason = "Personal", LeaveType = "Nghỉ phép năm" },
            new Leave { EmployeeId = other.Id, EmployeeNameSnapshot = other.Name, Status = "Chờ duyệt", FromDate = today.Date, ToDate = today.Date, Reason = "Other", LeaveType = "Nghỉ phép năm" });
        fixture.Context.Contracts.AddRange(
            new Contract { EmployeeId = mine.Id, EmployeeNameSnapshot = mine.Name, Status = "Chờ nhân viên ký", StartDate = today.Date, EndDate = today.Date.AddYears(1), Content = "Mine" },
            new Contract { EmployeeId = other.Id, EmployeeNameSnapshot = other.Name, Status = "Chờ nhân viên ký", StartDate = today.Date, EndDate = today.Date.AddYears(1), Content = "Other" });
        fixture.Context.Notifications.AddRange(
            new Notification { RecipientUserId = user.Id, Message = "Mine", IsRead = false },
            new Notification { RecipientUserId = user.Id, Message = "Read", IsRead = true });
        var course = new TrainingCourse { Code = "TRN-001", Name = "Safety", Description = "Safety", Category = "General", Mode = "Online", Instructor = "HR", Status = "Open" };
        fixture.Context.TrainingCourses.Add(course);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.CourseEnrollments.AddRange(
            new CourseEnrollment { EmployeeId = mine.Id, CourseId = course.Id, EmployeeNameSnapshot = mine.Name, RegistrationDate = today, ApprovalStatus = "APPROVED", ProgressStatus = "InProgress", ProgressPercent = 40, Result = "None", Notes = "" },
            new CourseEnrollment { EmployeeId = other.Id, CourseId = course.Id, EmployeeNameSnapshot = other.Name, RegistrationDate = today, ApprovalStatus = "APPROVED", ProgressStatus = "Completed", ProgressPercent = 100, Result = "Pass", Notes = "" });
        fixture.Context.RewardRecords.AddRange(
            new RewardRecord { EmployeeId = mine.Id, Title = "Mine reward", Achievement = "Achievement", ApprovalStatus = "APPROVED" },
            new RewardRecord { EmployeeId = other.Id, Title = "Other reward", Achievement = "Achievement", ApprovalStatus = "APPROVED" });
        fixture.Context.BenefitEntitlements.AddRange(
            new BenefitEntitlement { EmployeeId = mine.Id, EmployeeNameSnapshot = mine.Name!, BenefitProgramId = 1, Status = "Active" },
            new BenefitEntitlement { EmployeeId = other.Id, EmployeeNameSnapshot = other.Name!, BenefitProgramId = 1, Status = "Active" });
        await fixture.Context.SaveChangesAsync();

        var controller = fixture.CreateController("mine");
        var result = await controller.Index();
        var model = Assert.IsType<EmployeeDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal("Mine", model.EmployeeName);
        Assert.Equal("NV001", model.EmployeeCode);
        Assert.Equal(1, model.AttendanceCountThisMonth);
        Assert.Equal(1, model.PendingLeaveCount);
        Assert.Equal(1, model.ContractCount);
        Assert.Equal(1, model.ContractsAwaitingSignature);
        Assert.Equal(1, model.UnreadNotificationCount);
        Assert.Equal(1, model.TrainingInProgressCount);
        Assert.Equal(1, model.RewardCount);
        Assert.Equal(1, model.ActiveBenefitCount);
        Assert.Single(model.RecentAttendances);
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
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new Fixture(connection, context);
        }

        public NhanVienController CreateController(string username)
        {
            var http = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, username),
                    new Claim(ClaimTypes.Role, AppRoles.Employee)
                }, "TestAuth"))
            };
            return new NhanVienController(Context)
            {
                ControllerContext = new ControllerContext { HttpContext = http }
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
