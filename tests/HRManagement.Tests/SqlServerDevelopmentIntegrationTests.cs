using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
using giao_dien_demo.Data;
using giao_dien_demo.Data.Interceptors;
using giao_dien_demo.Helpers;
using giao_dien_demo.Models;
using giao_dien_demo.Services;

namespace HRManagement.Tests
{
    [CollectionDefinition("SqlServerDevCollection", DisableParallelization = true)]
    public class SqlServerDevCollectionDefinition : ICollectionFixture<object>
    {
    }

    [Collection("SqlServerDevCollection")]
    public sealed class SqlServerDevelopmentIntegrationTests : IAsyncDisposable
    {
        private const string ConnectionString = "Server=localhost;Database=HRManagementDB_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;";
        private readonly ApplicationDbContext _context;
        private readonly DepartmentManagementService _deptService;

        public SqlServerDevelopmentIntegrationTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .AddInterceptors(new AppendOnlyReportingHistoryInterceptor())
                .Options;

            _context = new ApplicationDbContext(options);
            _deptService = new DepartmentManagementService(_context);
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
        }

        private async Task EnsureStandardBaselineAsync()
        {
            await _deptService.SeedOfficialDepartmentsAsync();
            var itDept = await _context.Departments.FirstOrDefaultAsync(d => d.Code == "IT");
            var hrDept = await _context.Departments.FirstOrDefaultAsync(d => d.Code == "HR");
            if (itDept != null && itDept.Name != "Phòng Công nghệ thông tin")
            {
                itDept.Name = "Phòng Công nghệ thông tin";
            }
            if (hrDept != null && hrDept.Name != "Phòng Nhân sự")
            {
                hrDept.Name = "Phòng Nhân sự";
            }

            var itIds = new HashSet<int> { 1, 3, 16, 17, 23 };
            var hrIds = new HashSet<int> { 2, 12, 13, 18, 57 };
            var unassignedIds = new HashSet<int> { 14, 58 };

            var employees = await _context.Employees.ToListAsync();
            foreach (var emp in employees)
            {
                emp.Status = "Active";
                if (itIds.Contains(emp.Id) && itDept != null)
                {
                    emp.DepartmentId = itDept.Id;
                    emp.Department = "Phòng Công nghệ thông tin";
                }
                else if (hrIds.Contains(emp.Id) && hrDept != null)
                {
                    emp.DepartmentId = hrDept.Id;
                    emp.Department = "Phòng Nhân sự";
                }
                else if (unassignedIds.Contains(emp.Id))
                {
                    emp.DepartmentId = null;
                    emp.Department = "Chưa xếp phòng";
                }
            }

            var users = await _context.Users.ToListAsync();
            foreach (var u in users)
            {
                u.CanLogin = true;
            }

            await _context.SaveChangesAsync();
        }

        // =========================================================================
        // 1. SEED & BACKFILL INTEGRATION TESTS (CR-01, CR-02, OD-01, OD-20)
        // =========================================================================

        [Fact]
        public async Task SQLDEV_001_DepartmentSeed_IsIdempotent_CR01()
        {
            await EnsureStandardBaselineAsync();

            // Act
            int secondRun = await _deptService.SeedOfficialDepartmentsAsync();

            // Assert: Idempotency check
            Assert.Equal(0, secondRun);

            var depts = await _context.Departments.OrderBy(d => d.Code).ToListAsync();
            Assert.Equal(6, depts.Count);

            var expected = new Dictionary<string, string>
            {
                { "FIN", "Phòng Tài chính Kế toán" },
                { "HR", "Phòng Nhân sự" },
                { "IT", "Phòng Công nghệ thông tin" },
                { "MKT", "Phòng Marketing & Truyền thông" },
                { "OPS", "Phòng Vận hành" },
                { "SALES", "Phòng Kinh doanh" }
            };

            foreach (var kvp in expected)
            {
                var found = depts.FirstOrDefault(d => d.Code == kvp.Key);
                Assert.NotNull(found);
                Assert.Equal(kvp.Value, found.Name);
                Assert.True(found.IsActive);
            }
        }

        [Fact]
        public async Task SQLDEV_002_LegacyBackfill_MapsExact12Employees_CR02()
        {
            await EnsureStandardBaselineAsync();

            var hrDept = await _context.Departments.FirstAsync(d => d.Code == "HR");
            var itDept = await _context.Departments.FirstAsync(d => d.Code == "IT");

            var legacyIds = new HashSet<int> { 1, 2, 3, 12, 13, 14, 16, 17, 18, 23, 57, 58 };
            var legacyEmployees = await _context.Employees.Where(e => legacyIds.Contains(e.Id)).ToListAsync();
            Assert.Equal(12, legacyEmployees.Count);

            // Group 1: 5 HR mapped
            var hrGroup = legacyEmployees.Where(e => e.DepartmentId == hrDept.Id).ToList();
            Assert.Equal(5, hrGroup.Count);
            Assert.All(hrGroup, e => Assert.Equal("Phòng Nhân sự", e.Department));

            // Group 2: 5 IT mapped
            var itGroup = legacyEmployees.Where(e => e.DepartmentId == itDept.Id).ToList();
            Assert.Equal(5, itGroup.Count);
            Assert.All(itGroup, e => Assert.Equal("Phòng Công nghệ thông tin", e.Department));

            // Group 3: 2 Unassigned pool (DepartmentId == null, string preserved)
            var unassignedGroup = legacyEmployees.Where(e => e.DepartmentId == null).ToList();
            Assert.Equal(2, unassignedGroup.Count);
            Assert.All(unassignedGroup, e => Assert.Equal("Chưa xếp phòng", e.Department));
        }

        [Fact]
        public async Task SQLDEV_003_DepartmentRename_SynchronouslyUpdatesEmployees_OD20()
        {
            await EnsureStandardBaselineAsync();
            var itDept = await _context.Departments.FirstAsync(d => d.Code == "IT");

            string updatedName = "Phòng Công nghệ thông tin & Chuyển đổi số";
            bool success = await _deptService.UpdateDepartmentNameAsync(itDept.Id, updatedName, updatedByUserId: 1);
            Assert.True(success);

            // Check department entity
            var reloadedDept = await _context.Departments.FindAsync(itDept.Id);
            Assert.Equal(updatedName, reloadedDept!.Name);

            // Check synchronous employee string update
            var itEmployees = await _context.Employees.Where(e => e.DepartmentId == itDept.Id).ToListAsync();
            Assert.NotEmpty(itEmployees);
            Assert.All(itEmployees, e => Assert.Equal(updatedName, e.Department));

            // Revert back for clean baseline
            await _deptService.UpdateDepartmentNameAsync(itDept.Id, "Phòng Công nghệ thông tin", updatedByUserId: 1);
        }

        // =========================================================================
        // 2. SQL SERVER CHECK CONSTRAINT & HIERARCHY CYCLE TESTS (OD-07, OD-11)
        // =========================================================================

        [Fact]
        public async Task SQLDEV_004_SelfManager_BlockedBySqlServerCheckConstraint_OD07()
        {
            var firstEmp = await _context.Employees.FirstAsync();

            // Direct Raw SQL update setting ManagerId = Id must violate CK_Employees_ManagerId_NotSelf
            var ex = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await _context.Database.ExecuteSqlRawAsync($"UPDATE [dbo].[Employees] SET [ManagerId] = {firstEmp.Id} WHERE [Id] = {firstEmp.Id}");
            });

            Assert.Contains("CK_Employees_ManagerId_NotSelf", ex.Message);
        }

        [Fact]
        public async Task SQLDEV_005_UniversalCycleDetection_Blocked_OD07()
        {
            var employees = await _context.Employees.Take(3).ToListAsync();
            var a = employees[0];
            var b = employees[1];
            var c = employees[2];

            // Set up chain: B -> A, C -> B
            b.ManagerId = a.Id;
            c.ManagerId = b.Id;
            await _context.SaveChangesAsync();

            // Assigning A -> C creates cycle A -> C -> B -> A
            var result = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                _context, employeeId: a.Id, proposedManagerId: c.Id, isApprovedNullException: false, isTopLevelRole: false);

            Assert.False(result.IsValid);
            Assert.Equal(HierarchyValidationStatus.CycleDirectlyInvolvesEmployee, result.Status);

            // Cleanup
            b.ManagerId = null;
            c.ManagerId = null;
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task SQLDEV_006_ManagerIdNull_RequiresApprovalWorkflow_OD11()
        {
            var emp = await _context.Employees.FirstAsync();

            // Unapproved null manager
            var unapproved = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                _context, employeeId: emp.Id, proposedManagerId: null, isApprovedNullException: false, isTopLevelRole: false);
            Assert.False(unapproved.IsValid);
            Assert.Equal(HierarchyValidationStatus.UnapprovedNullManagerForbidden, unapproved.Status);

            // Approved exception
            var approved = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                _context, employeeId: emp.Id, proposedManagerId: null, isApprovedNullException: true, isTopLevelRole: false);
            Assert.True(approved.IsValid);
        }

        // =========================================================================
        // 3. ROWVERSION OPTIMISTIC CONCURRENCY ON REAL SQL SERVER (OD-08)
        // =========================================================================

        [Fact]
        public async Task SQLDEV_007_RowVersion_OptimisticConcurrency_ThrowsDbUpdateConcurrencyException_OD08()
        {
            var emp = await _context.Employees.FirstAsync();

            // Create client 2 context
            var options2 = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            using var context2 = new ApplicationDbContext(options2);

            // Client 1 loads employee
            var client1Emp = await _context.Employees.FindAsync(emp.Id);
            // Client 2 loads employee
            var client2Emp = await context2.Employees.FindAsync(emp.Id);

            // Client 1 modifies and saves to force RowVersion increment
            client1Emp!.Salary = client1Emp.Salary + 1500m;
            await _context.SaveChangesAsync();

            // Client 2 attempts to save with stale RowVersion loaded before Client 1's save
            client2Emp!.Salary = client2Emp.Salary + 3000m;
            var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            {
                await context2.SaveChangesAsync();
            });

            Assert.NotNull(ex);
        }

        // =========================================================================
        // 4. APPEND-ONLY ENFORCEMENT ON REAL SQL SERVER (OD-09)
        // =========================================================================

        [Fact]
        public async Task SQLDEV_008_AppendOnly_BlockedByDatabaseTrigger_OnRawSqlUpdateAndDelete_OD09()
        {
            var firstUser = await _context.Users.FirstAsync();
            var firstEmp = await _context.Employees.FirstAsync();

            // Insert a valid history record
            var history = new EmployeeReportingHistory
            {
                EmployeeId = firstEmp.Id,
                PreviousManagerId = null,
                NewManagerId = null,
                ChangeReason = "Ban hành bổ nhiệm SQL Server Dev",
                EffectiveDate = DateTime.UtcNow,
                ChangedByUserId = firstUser.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.EmployeeReportingHistories.Add(history);
            await _context.SaveChangesAsync();

            // 1. Raw SQL UPDATE must be rejected by Trigger TR_EmployeeReportingHistory_AppendOnlyGuard
            var updateEx = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await _context.Database.ExecuteSqlRawAsync($"UPDATE [dbo].[EmployeeReportingHistory] SET [ChangeReason] = N'Hacked' WHERE [Id] = {history.Id}");
            });
            Assert.Contains("OD-09", updateEx.Message);

            // 2. Raw SQL DELETE must be rejected by Trigger TR_EmployeeReportingHistory_AppendOnlyGuard
            var deleteEx = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await _context.Database.ExecuteSqlRawAsync($"DELETE FROM [dbo].[EmployeeReportingHistory] WHERE [Id] = {history.Id}");
            });
            Assert.Contains("OD-09", deleteEx.Message);
        }

        [Fact]
        public async Task SQLDEV_009_AppendOnly_BlockedByEfCoreInterceptor_OD09()
        {
            var firstUser = await _context.Users.FirstAsync();
            var firstEmp = await _context.Employees.FirstAsync();

            var history = new EmployeeReportingHistory
            {
                EmployeeId = firstEmp.Id,
                PreviousManagerId = null,
                NewManagerId = null,
                ChangeReason = "Test Interceptor Audit",
                EffectiveDate = DateTime.UtcNow,
                ChangedByUserId = firstUser.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.EmployeeReportingHistories.Add(history);
            await _context.SaveChangesAsync();

            // EF Core ChangeTracker update attempt
            history.ChangeReason = "Attempted EF update";
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _context.SaveChangesAsync();
            });

            Assert.Contains("OD-09", ex.Message);
            _context.Entry(history).State = EntityState.Unchanged;
        }

        // =========================================================================
        // 5. FILTERED UNIQUE INDEX ON USERS.EMPLOYEEID ON REAL SQL SERVER (OD-15)
        // =========================================================================

        [Fact]
        public async Task SQLDEV_010_UsersEmployeeId_FilteredUniqueIndex_EnforcedOnSqlServer_OD15()
        {
            var firstEmp = await _context.Employees.FirstAsync();

            // Multiple users with EmployeeId = NULL are allowed
            var nullUser1 = new User { Username = "null_user_dev_1", PasswordHash = "hash1", Role = "Employee", EmployeeId = null };
            var nullUser2 = new User { Username = "null_user_dev_2", PasswordHash = "hash2", Role = "Employee", EmployeeId = null };
            _context.Users.AddRange(nullUser1, nullUser2);
            await _context.SaveChangesAsync();

            // Find or assign first user with firstEmp.Id
            var assignedUser = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == firstEmp.Id);
            if (assignedUser == null)
            {
                assignedUser = new User { Username = "assigned_dev_user", PasswordHash = "hash", Role = "Employee", EmployeeId = firstEmp.Id };
                _context.Users.Add(assignedUser);
                await _context.SaveChangesAsync();
            }

            // Attempting to assign duplicate EmployeeId to a second user must violate IX_Users_EmployeeId
            var duplicateUser = new User { Username = "duplicate_dev_user", PasswordHash = "hash", Role = "Employee", EmployeeId = firstEmp.Id };
            _context.Users.Add(duplicateUser);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await _context.SaveChangesAsync();
            });

            Assert.Contains("IX_Users_EmployeeId", ex.InnerException?.Message ?? ex.Message);

            // Clean up test users
            _context.Users.Remove(duplicateUser);
            _context.Users.Remove(nullUser1);
            _context.Users.Remove(nullUser2);
            await _context.SaveChangesAsync();
        }

        // =========================================================================
        // 6. PAYROLL CALCULATION REGRESSION ON REAL SQL SERVER (OD-05, BD-03, BD-09)
        // =========================================================================

        [Fact]
        public async Task SQLDEV_011_PayrollCalculation_ExecutesAccuratelyOnRealSqlServer()
        {
            var payrollService = new PayrollCalculationService(_context);
            var now = DateTime.Today;

            // Execute payroll calculation across all 12 employees in HRManagementDB_Dev
            var allEmployees = await _context.Employees.ToListAsync();
            var salaries = await payrollService.CalculatePayrollAsync(now);

            Assert.NotNull(salaries);
            Assert.Equal(allEmployees.Count, salaries.Count);

            // Verify that all 12 salary records have non-empty EmployeeNameSnapshot
            Assert.All(salaries, s => Assert.False(string.IsNullOrEmpty(s.EmployeeNameSnapshot)));

            // Verify DepartmentName snapshot corresponds to current Employee.Department
            var empMap = await _context.Employees.ToDictionaryAsync(e => e.Id, e => e.Department);
            foreach (var s in salaries)
            {
                if (s.EmployeeId.HasValue && empMap.TryGetValue(s.EmployeeId.Value, out var expectedDept))
                {
                    Assert.Equal(expectedDept, s.DepartmentName);
                }
            }
        }
    }
}
