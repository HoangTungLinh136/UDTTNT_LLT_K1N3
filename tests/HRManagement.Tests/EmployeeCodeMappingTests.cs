using giao_dien_demo.Data;
using giao_dien_demo.Helpers;
using giao_dien_demo.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRManagement.Tests;

public sealed class EmployeeCodeMappingTests
{
    [Fact]
    public async Task SYS01_CODE_001_validation_uses_authoritative_employee_code()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Employees.Add(new Employee { EmployeeCode = "NV123", Name = "Canonical", Salary = 0, Status = "Active" });
        await context.SaveChangesAsync();

        var result = await PayrollValidationService.RunValidationAsync(context, new DateTime(2026, 9, 1));

        Assert.NotEmpty(result.Issues);
        Assert.All(result.Issues, issue => Assert.Equal("NV123", issue.EmployeeCode));
    }

    [Fact]
    public async Task SYS01_CODE_002_missing_employee_code_is_not_synthesized_from_database_id()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Employees.Add(new Employee { EmployeeCode = null, Name = "Unassigned", Salary = 0, Status = "Active" });
        await context.SaveChangesAsync();

        var result = await PayrollValidationService.RunValidationAsync(context, new DateTime(2026, 9, 1));

        Assert.NotEmpty(result.Issues);
        Assert.All(result.Issues, issue => Assert.Null(issue.EmployeeCode));
    }
}
