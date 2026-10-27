using System.Security.Claims;
using System.Text.Json;
using giao_dien_demo.Authorization;
using giao_dien_demo.Controllers;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRManagement.Tests;

public sealed class TrainingContractTests
{
    [Fact]
    public async Task TRAIN_001_HR_training_surface_is_HR_only()
    {
        var authorize = typeof(TrainingController).GetMethod(nameof(TrainingController.HrIndex))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        Assert.Equal(AppPolicies.HROnly, authorize.Policy);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task TRAIN_002_003_005_employee_courses_are_scoped_to_authenticated_employee_and_paginated()
    {
        await using var fixture = await Fixture.CreateAsync();
        var employee = new Employee { EmployeeCode = "NV001", Name = "Employee One", Department = "HR", Status = "Active" };
        var other = new Employee { EmployeeCode = "NV002", Name = "Employee Two", Department = "IT", Status = "Active" };
        fixture.Context.Employees.AddRange(employee, other);
        await fixture.Context.SaveChangesAsync();

        var user = new User { Username = "employee.one", FullName = employee.Name, Role = "Employee", CanLogin = true, EmployeeId = employee.Id, Email = "one@example.test" };
        var course = TestCourse("TRN-001", "Safety", "Safety course", 4);
        var otherCourse = TestCourse("TRN-002", "Other", "Other course", 2);
        fixture.Context.Users.Add(user);
        fixture.Context.TrainingCourses.AddRange(course, otherCourse);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.CourseEnrollments.AddRange(
            new CourseEnrollment { EmployeeId = employee.Id, CourseId = course.Id, EmployeeNameSnapshot = employee.Name, RegistrationDate = DateTime.UtcNow, ApprovalStatus = "APPROVED", ProgressStatus = "InProgress", ProgressPercent = 25, Result = "None", Notes = "" },
            new CourseEnrollment { EmployeeId = other.Id, CourseId = otherCourse.Id, EmployeeNameSnapshot = other.Name, RegistrationDate = DateTime.UtcNow, ApprovalStatus = "APPROVED", ProgressStatus = "Completed", ProgressPercent = 100, Result = "Pass", Notes = "" });
        await fixture.Context.SaveChangesAsync();

        var controller = fixture.CreateController("employee.one", "Employee");
        var result = await controller.GetMyCourses(null, "All", 1, 10);
        var payload = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);

        Assert.Equal(1, payload.GetProperty("totalCount").GetInt32());
        Assert.Equal(course.Id, payload.GetProperty("items")[0].GetProperty("CourseId").GetInt32());
    }

    [Fact]
    public async Task TRAIN_004_forged_enrollment_id_returns_not_found()
    {
        await using var fixture = await Fixture.CreateAsync();
        var employee = new Employee { EmployeeCode = "NV001", Name = "Employee One", Department = "HR", Status = "Active" };
        var other = new Employee { EmployeeCode = "NV002", Name = "Employee Two", Department = "IT", Status = "Active" };
        fixture.Context.Employees.AddRange(employee, other);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.Users.Add(new User { Username = "employee.one", FullName = employee.Name, Role = "Employee", CanLogin = true, EmployeeId = employee.Id, Email = "one@example.test" });
        var course = TestCourse("TRN-002", "Other", "Other course");
        fixture.Context.TrainingCourses.Add(course);
        await fixture.Context.SaveChangesAsync();
        var enrollment = new CourseEnrollment { EmployeeId = other.Id, CourseId = course.Id, EmployeeNameSnapshot = other.Name, RegistrationDate = DateTime.UtcNow, ApprovalStatus = "APPROVED", ProgressStatus = "Completed", ProgressPercent = 100, Result = "Pass", Notes = "" };
        fixture.Context.CourseEnrollments.Add(enrollment);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateController("employee.one", "Employee").GetCourseDetail(enrollment.Id);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task TRAIN_006_hr_search_can_use_canonical_employee_code()
    {
        await using var fixture = await Fixture.CreateAsync();
        var employee = new Employee { EmployeeCode = "NV009", Name = "Employee Nine", Department = "HR", Status = "Active" };
        fixture.Context.Employees.Add(employee);
        var course = TestCourse("TRN-009", "Security", "Security course");
        fixture.Context.TrainingCourses.Add(course);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.CourseEnrollments.Add(new CourseEnrollment { EmployeeId = employee.Id, CourseId = course.Id, EmployeeNameSnapshot = employee.Name, RegistrationDate = DateTime.UtcNow, ApprovalStatus = "PENDING_APPROVAL", ProgressStatus = "PendingApproval", ProgressPercent = 0, Result = "None", Notes = "" });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateController("hr.user", "HR").GetHrEnrollments("NV009", null);
        var payload = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);

        Assert.Single(payload.EnumerateArray());
        Assert.Equal("NV009", payload[0].GetProperty("employeeCode").GetString());
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

        public TrainingController CreateController(string username, string role)
        {
            var http = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, username),
                    new Claim(ClaimTypes.Role, role)
                }, "TestAuth"))
            };
            return new TrainingController(Context, null!, null!, null!, null!)
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

    private static TrainingCourse TestCourse(string code, string name, string description, int? hours = null) => new()
    {
        Code = code,
        Name = name,
        Description = description,
        Category = "General",
        Mode = "Online",
        Instructor = "HR",
        Status = "Open",
        DurationHours = hours
    };
}
