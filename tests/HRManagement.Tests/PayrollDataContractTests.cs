using giao_dien_demo.Controllers;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Helpers;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace HRManagement.Tests;

public sealed class PayrollDataContractTests
{
    [Fact]
    public void PAYDATA_010_013_payroll_surfaces_exclude_admin_and_require_business_roles()
    {
        var salaryRoles = typeof(SalaryController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles;
        var selfServiceRoles = typeof(MyPayrollController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles;

        Assert.DoesNotContain(AppRoles.Admin, salaryRoles!.Split(','));
        Assert.DoesNotContain(AppRoles.Admin, selfServiceRoles!.Split(','));
        Assert.Contains(AppRoles.HR, salaryRoles.Split(','));
        Assert.Contains(AppRoles.Employee, selfServiceRoles.Split(','));
    }

    [Fact]
    public void PAYDATA_005_cycle_period_has_canonical_boundaries()
    {
        var beforeCutoff = PayrollCycleHelper.GetCurrentCycle(new DateTime(2026, 9, 10, 12, 0, 0));
        var afterCutoff = PayrollCycleHelper.GetCurrentCycle(new DateTime(2026, 9, 11, 12, 0, 0));

        Assert.Equal(new DateTime(2026, 8, 11), beforeCutoff.StartDate);
        Assert.Equal(new DateTime(2026, 9, 10, 23, 59, 59), beforeCutoff.EndDate);
        Assert.Equal("T09/2026", beforeCutoff.PeriodName);
        Assert.Equal(new DateTime(2026, 9, 11), afterCutoff.StartDate);
        Assert.Equal(new DateTime(2026, 10, 10, 23, 59, 59), afterCutoff.EndDate);
        Assert.Equal("T10/2026", afterCutoff.PeriodName);
    }

    [Fact]
    public async Task PAYDATA_001_002_006_calculation_keeps_employee_and_report_lineage()
    {
        await using var fixture = await PayrollFixture.CreateAsync();
        var cycle = PayrollCycleHelper.GetCurrentCycle(DateTime.Now);
        var employee = new Employee
        {
            EmployeeCode = "PAY-A",
            Name = "Payroll Employee",
            CitizenId = "TAX-A",
            Department = "Finance",
            Status = "Active",
            Salary = 20_000_000m,
            PositionAllowance = 1_000_000m,
            HazardousAllowance = 500_000m,
            QualificationAllowance = 250_000m,
            RegionalAllowanceSystem = 0.1m
        };
        fixture.Context.Employees.Add(employee);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Attendances.Add(new Attendance
        {
            EmployeeId = employee.Id,
            EmployeeNameSnapshot = employee.Name,
            CheckIn = cycle.StartDate.AddDays(1).AddHours(8),
            CheckOut = cycle.StartDate.AddDays(1).AddHours(18),
            StandardHours = 8,
            OvertimeHours = 2
        });
        fixture.Context.RewardRecords.Add(new RewardRecord
        {
            EmployeeId = employee.Id,
            EmployeeCodeSnapshot = employee.EmployeeCode,
            EmployeeNameSnapshot = employee.Name,
            Title = "Approved reward",
            Achievement = "Achievement",
            HasMonetaryReward = true,
            RewardAmount = 1_500_000,
            PayrollPeriod = cycle.PeriodName,
            ApprovalStatus = "APPROVED"
        });
        await fixture.Context.SaveChangesAsync();

        var result = await new PayrollCalculationService(fixture.Context).CalculatePayrollAsync(DateTime.Now);
        var salary = Assert.Single(result);

        Assert.Equal(employee.Id, salary.EmployeeId);
        Assert.Equal(employee.Name, salary.EmployeeNameSnapshot);
        Assert.Equal(employee.Salary, (decimal)salary.BasicSalary);
        Assert.Equal(employee.CitizenId, salary.TaxCode);
        Assert.True(salary.PerformanceSalary > 0);
        Assert.True(salary.OvertimeCost > 0);
        Assert.Equal(3_750_000, salary.AllowanceCost);
        Assert.Equal(1_500_000, salary.Reward);
        Assert.True(salary.TotalSalary > 0);
        Assert.True(salary.NetSalary <= salary.TotalSalary);
    }

    [Fact]
    public async Task PAYDATA02_004_005_006_007_payment_and_salary_snapshots_are_not_live_master_data()
    {
        await using var fixture = await PayrollFixture.CreateAsync();
        var employee = new Employee
        {
            EmployeeCode = "PAY-H",
            Name = "Historical Employee",
            Status = "Active",
            Salary = 10_000_000m
        };
        fixture.Context.Employees.Add(employee);
        await fixture.Context.SaveChangesAsync();

        var cycle = PayrollCycleHelper.GetCurrentCycle(DateTime.Now);
        fixture.Context.Salaries.Add(new Salary
        {
            EmployeeId = employee.Id,
            EmployeeNameSnapshot = employee.Name,
            BasicSalary = 10_000_000,
            TotalSalary = 10_500_000,
            NetSalary = 9_000_000,
            SalaryMonth = cycle.StartDate
        });
        fixture.Context.PayrollPayments.Add(new PayrollPayment
        {
            EmployeeId = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            EmployeeName = employee.Name,
            NetSalary = 9_000_000,
            PayrollPeriod = cycle.PeriodName,
            CycleStartDate = cycle.StartDate,
            CycleEndDate = cycle.EndDate,
            PaymentDate = cycle.PaymentDate,
            Status = PaymentStatus.DaThanhToan,
            PaidAt = DateTime.Now
        });
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Employees.Entry(employee).Property(x => x.Salary).CurrentValue = 20_000_000m;
        fixture.Context.Attendances.Add(new Attendance
        {
            EmployeeId = employee.Id,
            CheckIn = cycle.StartDate.AddDays(2).AddHours(8),
            CheckOut = cycle.StartDate.AddDays(2).AddHours(17),
            StandardHours = 8
        });
        fixture.Context.RewardRecords.Add(new RewardRecord
        {
            EmployeeId = employee.Id,
            Title = "Later reward",
            Achievement = "Later achievement",
            HasMonetaryReward = true,
            RewardAmount = 5_000_000,
            PayrollPeriod = cycle.PeriodName,
            ApprovalStatus = "APPROVED"
        });
        await fixture.Context.SaveChangesAsync();

        var payment = await fixture.Context.PayrollPayments.SingleAsync();
        var snapshot = await fixture.Context.Salaries.SingleAsync();

        Assert.Equal(9_000_000, payment.NetSalary);
        Assert.Equal(10_000_000, snapshot.BasicSalary);
        Assert.Equal(9_000_000, snapshot.NetSalary);
    }

    private sealed class PayrollFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }

        private PayrollFixture(SqliteConnection connection, ApplicationDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public static async Task<PayrollFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new PayrollFixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
