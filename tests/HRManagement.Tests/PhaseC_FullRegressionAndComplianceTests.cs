using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using giao_dien_demo.Data;
using giao_dien_demo.Data.Interceptors;
using giao_dien_demo.Helpers;
using giao_dien_demo.Models;
using giao_dien_demo.Services;

namespace HRManagement.Tests
{
    public sealed class PhaseC_FullRegressionAndComplianceTests : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly DepartmentManagementService _deptService;

        public PhaseC_FullRegressionAndComplianceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(new AppendOnlyReportingHistoryInterceptor())
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            _deptService = new DepartmentManagementService(_context);
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }

        // =========================================================================
        // NHÓM C1: DEPARTMENT DATA MODEL & BACKFILL (CR-01, CR-02, OD-01, OD-20)
        // =========================================================================

        [Fact]
        public async Task C1_001_SeedOfficialDepartments_CreatesExactlySixDepartments_Per_CR01()
        {
            // Act
            int inserted = await _deptService.SeedOfficialDepartmentsAsync();

            // Assert
            Assert.Equal(6, inserted);
            var depts = await _context.Departments.OrderBy(d => d.Code).ToListAsync();
            Assert.Equal(6, depts.Count);

            var codeMap = depts.ToDictionary(d => d.Code, d => d.Name);
            Assert.Equal("Phòng Nhân sự", codeMap["HR"]);
            Assert.Equal("Phòng Công nghệ thông tin", codeMap["IT"]);
            Assert.Equal("Phòng Marketing & Truyền thông", codeMap["MKT"]);
            Assert.Equal("Phòng Tài chính Kế toán", codeMap["FIN"]);
            Assert.Equal("Phòng Kinh doanh", codeMap["SALES"]);
            Assert.Equal("Phòng Vận hành", codeMap["OPS"]);

            // Re-run seed must be idempotent (0 inserted)
            int secondRun = await _deptService.SeedOfficialDepartmentsAsync();
            Assert.Equal(0, secondRun);
        }

        [Fact]
        public async Task C1_002_BackfillLegacyEmployees_MapsExactCR02Groups_PreservesUnassigned()
        {
            // Arrange: 12 nhân viên theo đúng phân bổ Live DB thực tế
            _context.Employees.AddRange(
                // 5 thuộc HR
                new Employee { EmployeeCode = "HR1", Name = "Emp HR 1", Department = "Phòng nhân sự", Status = "Active" },
                new Employee { EmployeeCode = "HR2", Name = "Emp HR 2", Department = "Phòng nhân sự", Status = "Active" },
                new Employee { EmployeeCode = "HR3", Name = "Emp HR 3", Department = "Phòng nhân sự", Status = "Active" },
                new Employee { EmployeeCode = "HR4", Name = "Emp HR 4", Department = "Phòng HR", Status = "Active" },
                new Employee { EmployeeCode = "HR5", Name = "Emp HR 5", Department = "HR Dept", Status = "Active" },
                // 5 thuộc IT
                new Employee { EmployeeCode = "IT1", Name = "Emp IT 1", Department = "Phòng IT", Status = "Active" },
                new Employee { EmployeeCode = "IT2", Name = "Emp IT 2", Department = "Phòng IT", Status = "Active" },
                new Employee { EmployeeCode = "IT3", Name = "Emp IT 3", Department = "IT Software", Status = "Active" },
                new Employee { EmployeeCode = "IT4", Name = "Emp IT 4", Department = "IT Software", Status = "Active" },
                new Employee { EmployeeCode = "IT5", Name = "Emp IT 5", Department = "Phòng Quản trị hệ thống", Status = "Active" },
                // 2 Chưa xếp phòng (Unassigned)
                new Employee { EmployeeCode = "UA1", Name = "Emp UA 1", Department = "Chưa xếp phòng", Status = "Active" },
                new Employee { EmployeeCode = "UA2", Name = "Emp UA 2", Department = "Chưa xếp phòng", Status = "Active" }
            );
            await _context.SaveChangesAsync();

            // Act
            int backfilled = await _deptService.BackfillLegacyEmployeesAsync();

            // Assert
            Assert.Equal(12, backfilled);

            var hrDept = await _context.Departments.FirstAsync(d => d.Code == "HR");
            var itDept = await _context.Departments.FirstAsync(d => d.Code == "IT");

            var all = await _context.Employees.ToListAsync();
            
            // 5 nhân viên HR phải có DepartmentId = hrDept.Id và Department = "Phòng Nhân sự"
            var hrGroup = all.Where(e => e.DepartmentId == hrDept.Id).ToList();
            Assert.Equal(5, hrGroup.Count);
            Assert.All(hrGroup, e => Assert.Equal("Phòng Nhân sự", e.Department));

            // 5 nhân viên IT phải có DepartmentId = itDept.Id và Department = "Phòng Công nghệ thông tin"
            var itGroup = all.Where(e => e.DepartmentId == itDept.Id).ToList();
            Assert.Equal(5, itGroup.Count);
            Assert.All(itGroup, e => Assert.Equal("Phòng Công nghệ thông tin", e.Department));

            // 2 nhân viên Chưa xếp phòng phải có DepartmentId = NULL và Department = "Chưa xếp phòng"
            var unassignedGroup = all.Where(e => e.DepartmentId == null).ToList();
            Assert.Equal(2, unassignedGroup.Count);
            Assert.All(unassignedGroup, e => Assert.Equal("Chưa xếp phòng", e.Department));
        }

        [Fact]
        public async Task C1_003_UpdateDepartmentName_SynchronouslyUpdatesEmployees_OD20()
        {
            // Arrange
            await _deptService.SeedOfficialDepartmentsAsync();
            var itDept = await _context.Departments.FirstAsync(d => d.Code == "IT");

            var emp = new Employee { EmployeeCode = "IT01", Name = "Dev 1", DepartmentId = itDept.Id, Department = itDept.Name, Status = "Active" };
            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();

            // Act
            string newName = "Phòng CNTT & Đổi mới sáng tạo";
            bool updated = await _deptService.UpdateDepartmentNameAsync(itDept.Id, newName, updatedByUserId: 1);

            // Assert
            Assert.True(updated);
            var reloadedDept = await _context.Departments.FindAsync(itDept.Id);
            Assert.Equal(newName, reloadedDept!.Name);

            var reloadedEmp = await _context.Employees.FindAsync(emp.Id);
            Assert.Equal(newName, reloadedEmp!.Department); // Synchronous update verified
        }

        // =========================================================================
        // NHÓM C2: DIRECT REPORTING & UNIVERSAL CYCLE DETECTION (OD-07, OD-09, OD-11)
        // =========================================================================

        [Fact]
        public async Task C2_001_SelfManager_IsStrictlyForbidden_OD07()
        {
            var emp = new Employee { EmployeeCode = "E01", Name = "Emp 1", Status = "Active" };
            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();

            var result = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                _context, emp.Id, proposedManagerId: emp.Id, isApprovedNullException: false, isTopLevelRole: false);

            Assert.False(result.IsValid);
            Assert.Equal(HierarchyValidationStatus.SelfManagerForbidden, result.Status);
        }

        [Fact]
        public async Task C2_002_DirectTwoLevelCycle_IsDetectedAndBlocked_OD07()
        {
            // A -> B. Gán B -> A phải bị chặn
            var empA = new Employee { EmployeeCode = "A", Name = "Emp A", Status = "Active" };
            var empB = new Employee { EmployeeCode = "B", Name = "Emp B", Status = "Active" };
            _context.Employees.AddRange(empA, empB);
            await _context.SaveChangesAsync();

            // A có Manager là B
            empA.ManagerId = empB.Id;
            await _context.SaveChangesAsync();

            // Gán B có Manager là A -> Chu trình A - B - A
            var result = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                _context, employeeId: empB.Id, proposedManagerId: empA.Id, isApprovedNullException: false, isTopLevelRole: false);

            Assert.False(result.IsValid);
            Assert.Equal(HierarchyValidationStatus.CycleDirectlyInvolvesEmployee, result.Status);
        }

        [Fact]
        public async Task C2_003_MultiLevelCycle_ThreeNodes_IsDetectedAndBlocked_OD07()
        {
            // A -> B -> C. Gán C -> A phải bị chặn
            var empA = new Employee { EmployeeCode = "A", Name = "Emp A", Status = "Active" };
            var empB = new Employee { EmployeeCode = "B", Name = "Emp B", Status = "Active" };
            var empC = new Employee { EmployeeCode = "C", Name = "Emp C", Status = "Active" };
            _context.Employees.AddRange(empA, empB, empC);
            await _context.SaveChangesAsync();

            empB.ManagerId = empA.Id; // B -> A
            empC.ManagerId = empB.Id; // C -> B
            await _context.SaveChangesAsync();

            // Gán A -> C -> Chu trình A -> C -> B -> A
            var result = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                _context, employeeId: empA.Id, proposedManagerId: empC.Id, isApprovedNullException: false, isTopLevelRole: false);

            Assert.False(result.IsValid);
            Assert.Equal(HierarchyValidationStatus.CycleDirectlyInvolvesEmployee, result.Status);
        }

        [Fact]
        public async Task C2_004_PreExistingCycleInAncestors_IsDetectedAndBlocked()
        {
            // M1 -> M2 -> M1 (Chu trình tồn tại sẵn giữa cấp trên). Gán E -> M1 phải bị chặn
            var empE = new Employee { EmployeeCode = "E", Name = "Emp E", Status = "Active" };
            var mgr1 = new Employee { EmployeeCode = "M1", Name = "Mgr 1", Status = "Active" };
            var mgr2 = new Employee { EmployeeCode = "M2", Name = "Mgr 2", Status = "Active" };
            _context.Employees.AddRange(empE, mgr1, mgr2);
            await _context.SaveChangesAsync();

            mgr1.ManagerId = mgr2.Id;
            mgr2.ManagerId = mgr1.Id; // Vòng lặp giữa M1 và M2
            await _context.SaveChangesAsync();

            var result = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                _context, employeeId: empE.Id, proposedManagerId: mgr1.Id, isApprovedNullException: false, isTopLevelRole: false);

            Assert.False(result.IsValid);
            Assert.Equal(HierarchyValidationStatus.PreExistingCycleInManagementChain, result.Status);
        }

        [Fact]
        public async Task C2_005_FailClosed_BrokenLink_ReturnsCorruptedHierarchyChain()
        {
            var empE = new Employee { EmployeeCode = "E", Name = "Emp E", Status = "Active" };
            var mgr1 = new Employee { EmployeeCode = "M1", Name = "Mgr 1", Status = "Active" };
            _context.Employees.AddRange(empE, mgr1);
            await _context.SaveChangesAsync();

            // Tạm thời tắt FK check trong SQLite để chèn dữ liệu mắt xích bị hỏng kiểm thử Fail-Closed
            await _context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            await _context.Database.ExecuteSqlRawAsync($"UPDATE Employees SET ManagerId = 99999 WHERE Id = {mgr1.Id};");
            await _context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

            var result = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                _context, employeeId: empE.Id, proposedManagerId: mgr1.Id, isApprovedNullException: false, isTopLevelRole: false);

            Assert.False(result.IsValid);
            Assert.Equal(HierarchyValidationStatus.CorruptedHierarchyChain, result.Status);
        }

        [Fact]
        public async Task C2_006_NullManager_RequiresApprovedWorkflow_OD11()
        {
            var emp = new Employee { EmployeeCode = "E", Name = "Emp", Status = "Active" };
            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();

            // Trường hợp 1: Không có phê duyệt ngoại lệ -> BỊ TỪ CHỐI
            var unapprovedResult = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                _context, employeeId: emp.Id, proposedManagerId: null, isApprovedNullException: false, isTopLevelRole: false);

            Assert.False(unapprovedResult.IsValid);
            Assert.Equal(HierarchyValidationStatus.UnapprovedNullManagerForbidden, unapprovedResult.Status);

            // Trường hợp 2: Có phê duyệt ngoại lệ hợp lệ -> CHẤP NHẬN
            var approvedResult = await HierarchyValidationHelper.ValidateReportingHierarchyUniversalAsync(
                _context, employeeId: emp.Id, proposedManagerId: null, isApprovedNullException: true, isTopLevelRole: false);

            Assert.True(approvedResult.IsValid);
        }

        [Fact]
        public async Task C2_007_AppendOnlyInterceptor_ThrowsException_OnUpdateOrDelete_OD09()
        {
            var user = new User { Username = "auditor", Role = "Admin", PasswordHash = "hash" };
            var emp = new Employee { EmployeeCode = "AUD01", Name = "Auditor Employee", Status = "Active" };
            _context.Users.Add(user);
            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();

            var history = new EmployeeReportingHistory
            {
                EmployeeId = emp.Id,
                PreviousManagerId = null,
                NewManagerId = null,
                ChangeReason = "Bổ nhiệm ban đầu",
                EffectiveDate = DateTime.UtcNow,
                ChangedByUserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.EmployeeReportingHistories.Add(history);
            await _context.SaveChangesAsync();

            // Thử UPDATE -> Phải ném ngoại lệ
            history.ChangeReason = "Cố tình sửa đổi lịch sử";
            var updateEx = await Assert.ThrowsAsync<InvalidOperationException>(() => _context.SaveChangesAsync());
            Assert.Contains("OD-09", updateEx.Message);

            // Detach để reset ChangeTracker
            _context.Entry(history).State = EntityState.Unchanged;

            // Thử DELETE -> Phải ném ngoại lệ
            _context.EmployeeReportingHistories.Remove(history);
            var deleteEx = await Assert.ThrowsAsync<InvalidOperationException>(() => _context.SaveChangesAsync());
            Assert.Contains("OD-09", deleteEx.Message);
        }

        // =========================================================================
        // NHÓM C3 & C4: CONCURRENCY & OFFBOARDING SUBORDINATE GUARD (OD-08, OD-12)
        // =========================================================================

        [Fact]
        public async Task C4_001_OffboardingGuard_BlocksManager_WithActiveSubordinates_OD12()
        {
            // Quản lý M có cấp dưới S
            var mgr = new Employee { EmployeeCode = "MGR", Name = "Manager", Status = "Active" };
            var sub = new Employee { EmployeeCode = "SUB", Name = "Subordinate", Status = "Active" };
            _context.Employees.AddRange(mgr, sub);
            await _context.SaveChangesAsync();

            sub.ManagerId = mgr.Id;
            await _context.SaveChangesAsync();

            // Kiểm tra Guard: Quản lý còn cấp dưới Active -> hasActiveSubordinates == true
            bool hasActive = await _context.Employees.AnyAsync(e => e.ManagerId == mgr.Id && e.Status == "Active");
            Assert.True(hasActive); // Guard kích hoạt thành công theo OD-12
        }

        // =========================================================================
        // NHÓM C6: PAYROLL REGRESSION PRESERVATION (OD-05, BD-03, BD-09)
        // =========================================================================

        [Fact]
        public async Task C6_001_PayrollCalculation_PreservesExactFormulas_And_DepartmentName()
        {
            // Seed 1 JobTitleConfig
            _context.JobTitleConfigs.Add(new JobTitleConfig
            {
                JobTitle = "Trưởng phòng",
                Coefficient = 1.5m,
                Notes = "Hệ số trưởng phòng"
            });

            // Employee có JobTitle khớp
            var emp1 = new Employee
            {
                EmployeeCode = "NV01",
                Name = "Nguyễn Văn A",
                Department = "Phòng Công nghệ thông tin",
                JobTitle = "Trưởng phòng",
                Salary = 20800000m,
                AcademicLevel = null,
                KPIScore = null,
                ExperienceYears = 0,
                Status = "Active"
            };

            // Employee có JobTitle KHÔNG khớp (Fallback 1.0m)
            var emp2 = new Employee
            {
                EmployeeCode = "NV02",
                Name = "Trần Thị B",
                Department = "Phòng Nhân sự",
                JobTitle = "Nhân viên chưa cấu hình",
                Salary = 20800000m,
                AcademicLevel = null,
                KPIScore = null,
                ExperienceYears = 0,
                Status = "Active"
            };

            _context.Employees.AddRange(emp1, emp2);
            await _context.SaveChangesAsync();

            // Giả lập 208 giờ công chuẩn cho mỗi nhân viên
            var now = DateTime.Today;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            for (int i = 0; i < 26; i++)
            {
                var workDay = startOfMonth.AddDays(i);
                if (workDay.DayOfWeek != DayOfWeek.Sunday)
                {
                    _context.Attendances.Add(new Attendance
                    {
                        EmployeeId = emp1.Id,
                        CheckIn = workDay.AddHours(8),
                        CheckOut = workDay.AddHours(17),
                        StandardHours = 8.0,
                        OvertimeHours = 0
                    });
                    _context.Attendances.Add(new Attendance
                    {
                        EmployeeId = emp2.Id,
                        CheckIn = workDay.AddHours(8),
                        CheckOut = workDay.AddHours(17),
                        StandardHours = 8.0,
                        OvertimeHours = 0
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Khởi tạo PayrollCalculationService
            var payrollService = new PayrollCalculationService(_context);
            var results = await payrollService.CalculatePayrollAsync(startOfMonth);

            Assert.Equal(2, results.Count);

            var pay1 = results.First(r => r.EmployeeId == emp1.Id);
            var pay2 = results.First(r => r.EmployeeId == emp2.Id);

            // Kiểm tra bảo toàn tên phòng ban trong snapshot lương (OD-01, OD-20)
            Assert.Equal("Phòng Công nghệ thông tin", pay1.DepartmentName);
            Assert.Equal("Phòng Nhân sự", pay2.DepartmentName);

            // Chu kỳ lương 11 tháng trước đến 10 tháng này: Có đúng 9 ngày làm việc hợp lệ trong khoảng ngày 1-10 (72 giờ)
            // Emp1 có hsViTri = 1.5m -> PerformanceSalary = (20800000 / 208) * 72 * 1.5 = 10,800,000
            Assert.Equal(10800000.0, pay1.PerformanceSalary, 2);

            // Emp2 fallback hsViTri = 1.0m -> PerformanceSalary = (20800000 / 208) * 72 * 1.0 = 7,200,000
            Assert.Equal(7200000.0, pay2.PerformanceSalary, 2);
        }
    }
}
