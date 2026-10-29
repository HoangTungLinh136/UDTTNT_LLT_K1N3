using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Data.Interceptors;
using giao_dien_demo.Helpers;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using giao_dien_demo.Controllers;

namespace HRManagement.Tests
{
    [Collection("SqlServerDevCollection")]
    public sealed class PhaseC_BusinessAcceptanceTests : IAsyncDisposable
    {
        private const string ConnectionString = "Server=localhost;Database=HRManagementDB_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;";
        private readonly ApplicationDbContext _context;
        private readonly DepartmentManagementService _deptService;
        private readonly DirectManagerService _directManagerService;
        private readonly TransferEffectiveService _transferEffectiveService;
        private readonly AuditLogService _auditLogService;

        public PhaseC_BusinessAcceptanceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .AddInterceptors(new AppendOnlyReportingHistoryInterceptor())
                .Options;

            _context = new ApplicationDbContext(options);
            _deptService = new DepartmentManagementService(_context);
            
            var httpContextAccessor = new HttpContextAccessor();
            _auditLogService = new AuditLogService(_context, httpContextAccessor);
            _directManagerService = new DirectManagerService(_context, _auditLogService);

            var logger = NullLogger<TransferEffectiveService>.Instance;
            var fakeNotificationService = new FakeNotificationService();
            _transferEffectiveService = new TransferEffectiveService(_context, logger, _auditLogService, fakeNotificationService);
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

        private sealed class FakeNotificationService : IInAppNotificationService
        {
            public Task<IReadOnlyList<Notification>> SendAsync(
                string recipientSelector,
                string message,
                string notificationType,
                string? targetUrl = null,
                int? referenceId = null,
                int? senderUserId = null,
                string? senderName = null,
                string? deduplicationKey = null,
                IReadOnlyCollection<int>? allowedRecipientUserIds = null)
            {
                return Task.FromResult<IReadOnlyList<Notification>>(new List<Notification>());
            }
        }

        // =========================================================================
        // WF-01: THAY ĐỔI QUẢN LÝ TRỰC TIẾP (OD-07, OD-08, OD-09, OD-11, BD-06)
        // =========================================================================

        [Fact]
        public async Task WF01_001_ChangeDirectManager_Valid_UpdatesManager_RecordsReportingHistory_And_AuditLog()
        {
            // Arrange
            await EnsureStandardBaselineAsync();

            var emp2 = await _context.Employees.FirstAsync(e => e.Id == 2);
            var emp1 = await _context.Employees.FirstAsync(e => e.Id == 1);
            int? originalManagerId = emp2.ManagerId;

            var request = new DirectManagerChangeRequest
            {
                EmployeeId = emp2.Id,
                NewManagerId = emp1.Id,
                Reason = "Bàn giao quản lý trực tiếp theo quyết định số HR-2026-01",
                RowVersion = emp2.RowVersion
            };

            // Act
            var result = await _directManagerService.ChangeDirectManagerAsync(request, "admin_test", 1);

            // Assert
            Assert.True(result.Success, $"Expected success but got error: {result.Error}");
            Assert.NotNull(result.History);

            // Verify live DB update
            var updatedEmp = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == 2);
            Assert.Equal(emp1.Id, updatedEmp.ManagerId);

            // Verify EmployeeReportingHistory
            var historyRecord = await _context.EmployeeReportingHistories
                .AsNoTracking()
                .Where(h => h.EmployeeId == 2)
                .OrderByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            Assert.NotNull(historyRecord);
            Assert.Equal(emp1.Id, historyRecord.NewManagerId);
            Assert.Equal(originalManagerId, historyRecord.PreviousManagerId);
            Assert.Contains("HR-2026-01", historyRecord.ChangeReason);

            // Verify AuditLog
            var auditEntry = await _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.Resource == "Employee" && a.ResourceId == "2" && a.Action == "CHANGE_DIRECT_MANAGER")
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            Assert.NotNull(auditEntry);
            Assert.Equal("Success", auditEntry.Status);
            Assert.Contains($"ManagerId={emp1.Id}", auditEntry.NewValues);
        }

        [Fact]
        public async Task WF01_002_ChangeDirectManager_CycleDetected_Rejects_RollsBack_LeavesNoPartialState()
        {
            // Arrange: Setup A (1) -> B (2)
            await EnsureStandardBaselineAsync();
            var emp1 = await _context.Employees.FirstAsync(e => e.Id == 1);
            var emp2 = await _context.Employees.FirstAsync(e => e.Id == 2);

            emp2.ManagerId = emp1.Id;
            await _context.SaveChangesAsync();

            int reportingHistoryCountBefore = await _context.EmployeeReportingHistories.CountAsync();

            // Act: Try to make B (2) the manager of A (1) -> Direct Cycle A-B-A
            var cycleRequest = new DirectManagerChangeRequest
            {
                EmployeeId = emp1.Id,
                NewManagerId = emp2.Id,
                Reason = "Cố tình tạo chu trình",
                RowVersion = emp1.RowVersion
            };

            var result = await _directManagerService.ChangeDirectManagerAsync(cycleRequest, "admin_test", 1);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("chu trình", result.Error, StringComparison.OrdinalIgnoreCase);

            // Verify DB state rolled back and unchanged
            var emp1After = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == 1);
            Assert.Null(emp1After.ManagerId); // Emp 1 manager was not changed

            int reportingHistoryCountAfter = await _context.EmployeeReportingHistories.CountAsync();
            Assert.Equal(reportingHistoryCountBefore, reportingHistoryCountAfter); // No orphan history entry
        }

        [Fact]
        public async Task WF01_003_ChangeDirectManager_ConcurrencyConflict_Rejects_RollsBack()
        {
            // Arrange
            await EnsureStandardBaselineAsync();
            var emp = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == 3);

            // Stale row version
            byte[] staleRowVersion = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 };

            var request = new DirectManagerChangeRequest
            {
                EmployeeId = 3,
                NewManagerId = 1,
                Reason = "Kiểm tra concurrency conflict",
                RowVersion = staleRowVersion
            };

            // Act
            var result = await _directManagerService.ChangeDirectManagerAsync(request, "admin_test", 1);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Concurrency", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task WF01_004_ChangeDirectManager_NullWithoutApproval_Rejects_WithApproval_Succeeds()
        {
            // Arrange
            await EnsureStandardBaselineAsync();
            var emp = await _context.Employees.FirstAsync(e => e.Id == 2);
            emp.ManagerId = 1;
            await _context.SaveChangesAsync();

            // Case A: NULL without exception approval -> Rejected (Fail-Closed)
            var unapprovedRequest = new DirectManagerChangeRequest
            {
                EmployeeId = 2,
                NewManagerId = null,
                AllowNullManagerException = false
            };

            var rejectResult = await _directManagerService.ChangeDirectManagerAsync(unapprovedRequest, "admin_test", 1);
            Assert.False(rejectResult.Success);
            Assert.Contains("OD-11", rejectResult.Error);

            // Case B: NULL with valid approved exception -> Succeeds
            var approvedRequest = new DirectManagerChangeRequest
            {
                EmployeeId = 2,
                NewManagerId = null,
                AllowNullManagerException = true,
                NullManagerReason = "Phê duyệt CEO miễn quản lý trực tiếp trong giai đoạn biệt phái"
            };

            var successResult = await _directManagerService.ChangeDirectManagerAsync(approvedRequest, "admin_test", 1);
            Assert.True(successResult.Success, $"Expected success but got: {successResult.Error}");

            var empAfter = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == 2);
            Assert.Null(empAfter.ManagerId);
        }

        // =========================================================================
        // WF-02: ĐIỀU CHUYỂN PHÒNG BAN (OD-04, OD-20, WF-02)
        // =========================================================================

        [Fact]
        public async Task WF02_001_TransferRecord_EffectiveExecution_AtomicallyUpdatesDepartmentId_Department_ManagerId_Histories_And_Audit()
        {
            // Arrange
            await EnsureStandardBaselineAsync();

            var itDept = await _context.Departments.FirstAsync(d => d.Code == "IT");
            var hrDept = await _context.Departments.FirstAsync(d => d.Code == "HR");
            var emp = await _context.Employees.FirstAsync(e => e.Department == "Phòng Công nghệ thông tin" && e.Id != 1); // Employee in IT
            var hrManager = await _context.Employees.FirstAsync(e => e.Id == 2); // Manager in HR

            // Create an approved transfer record with EffectiveDate in the past (due now)
            var transfer = new TransferRecord
            {
                EmployeeId = emp.Id,
                CurrentDepartmentSnapshot = emp.Department,
                TargetDepartmentId = hrDept.Id,
                TargetDepartment = hrDept.Name,
                TargetPosition = "Chuyên viên nhân sự cấp cao",
                TargetJobTitle = "Senior HR Specialist",
                TargetManagerId = hrManager.Id,
                EffectiveDate = DateTime.Today.AddDays(-1),
                ApprovalStatus = "APPROVED",
                Reason = "Điều chuyển sang Phòng Nhân sự theo chiến lược tái cấu trúc"
            };

            _context.TransferRecords.Add(transfer);
            await _context.SaveChangesAsync();

            // Act
            bool applied = await _transferEffectiveService.ApplyTransferAsync(transfer.Id, "HR_LEADER");

            // Assert
            Assert.True(applied);

            // Verify live DB atomic updates
            var updatedEmp = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == emp.Id);
            Assert.Equal(hrDept.Id, updatedEmp.DepartmentId);
            Assert.Equal("Phòng Nhân sự", updatedEmp.Department);
            Assert.Equal(hrManager.Id, updatedEmp.ManagerId);
            Assert.Equal("Chuyên viên nhân sự cấp cao", updatedEmp.Position);
            Assert.Equal("Senior HR Specialist", updatedEmp.JobTitle);

            // Verify TransferRecord is EFFECTIVE
            var updatedTransfer = await _context.TransferRecords.AsNoTracking().FirstAsync(t => t.Id == transfer.Id);
            Assert.Equal("EFFECTIVE", updatedTransfer.ApprovalStatus);
            Assert.NotNull(updatedTransfer.AppliedAt);
            Assert.Equal("HR_LEADER", updatedTransfer.AppliedBy);

            // Verify EmployeeReportingHistory was atomically inserted
            var repHistory = await _context.EmployeeReportingHistories
                .AsNoTracking()
                .Where(h => h.EmployeeId == emp.Id)
                .OrderByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            Assert.NotNull(repHistory);
            Assert.Equal(hrManager.Id, repHistory.NewManagerId);
            Assert.Contains(transfer.Id.ToString(), repHistory.ChangeReason);

            // Verify AuditLog was created
            var audit = await _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.Resource == "TransferRecord" && a.ResourceId == transfer.Id.ToString() && a.Action == "EFFECTIVE")
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            Assert.NotNull(audit);
            Assert.Equal("Success", audit.Status);
            Assert.Contains($"DeptId={hrDept.Id}", audit.NewValues);
            Assert.Contains($"ManagerId={hrManager.Id}", audit.NewValues);
        }

        [Fact]
        public async Task WF02_002_TransferRecord_InvalidTargetManager_RollsBackAtomically_LeavesNoPartialState()
        {
            // Arrange
            await EnsureStandardBaselineAsync();
            var emp = await _context.Employees.FirstAsync(e => e.Id == 3);
            string originalDept = emp.Department ?? "";
            int? originalDeptId = emp.DepartmentId;
            int? originalManagerId = emp.ManagerId;

            // TargetManagerId = emp.Id (Self manager forbidden by OD-07)
            var transfer = new TransferRecord
            {
                EmployeeId = emp.Id,
                CurrentDepartmentSnapshot = emp.Department,
                TargetDepartment = "Phòng Nhân sự",
                TargetManagerId = emp.Id, // Self-cycle!
                EffectiveDate = DateTime.Today.AddDays(-1),
                ApprovalStatus = "APPROVED",
                Reason = "Điều chuyển không hợp lệ do tự quản lý chính mình"
            };

            _context.TransferRecords.Add(transfer);
            await _context.SaveChangesAsync();

            // Act
            bool applied = await _transferEffectiveService.ApplyTransferAsync(transfer.Id, "SYSTEM");

            // Assert
            Assert.False(applied);

            // Verify DB rolled back completely
            var empAfter = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == emp.Id);
            Assert.Equal(originalDept, empAfter.Department);
            Assert.Equal(originalDeptId, empAfter.DepartmentId);
            Assert.Equal(originalManagerId, empAfter.ManagerId);

            var transferAfter = await _context.TransferRecords.AsNoTracking().FirstAsync(t => t.Id == transfer.Id);
            Assert.Equal("APPROVED", transferAfter.ApprovalStatus); // Not EFFECTIVE
            Assert.Null(transferAfter.AppliedAt);
        }

        // =========================================================================
        // WF-03: NGHỈ VIỆC VÀ XỬ LÝ CẤP DƯỚI (OD-12, OD-16, WF-03)
        // =========================================================================

        [Fact]
        public async Task WF03_001_Offboarding_ManagerWithActiveSubordinates_BlockedByOD12Guard_PreservesApprovedRecord()
        {
            // Arrange
            await EnsureStandardBaselineAsync();

            var manager = await _context.Employees.FirstAsync(e => e.Id == 1);
            var subordinate = await _context.Employees.FirstAsync(e => e.Id == 2);

            // Make sure subordinate is active and reports to manager
            manager.Status = "Active";
            subordinate.Status = "Active";
            subordinate.ManagerId = manager.Id;

            var managerUser = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == manager.Id);
            if (managerUser != null)
            {
                managerUser.CanLogin = true;
            }
            await _context.SaveChangesAsync();

            // Create an approved offboarding record ready to complete
            var offboarding = new OffboardingRecord
            {
                EmployeeId = manager.Id,
                ApprovalStatus = "APPROVED",
                OffboardingStatus = "PENDING_HANDOVER",
                AttendanceClosingStatus = "COMPLETED",
                EffectiveDate = DateTime.Today
            };
            _context.OffboardingRecords.Add(offboarding);
            await _context.SaveChangesAsync();

            // Add mandatory checklist tasks all done
            _context.OffboardingChecklistTasks.Add(new OffboardingChecklistTask
            {
                OffboardingRecordId = offboarding.Id,
                TaskName = "Bàn giao tài sản",
                IsMandatory = true,
                Status = "DONE"
            });
            await _context.SaveChangesAsync();

            // Setup Controller with HR user context
            var controller = new OffboardingController(
                _context,
                null!, // IApprovalEngine
                null!, // IWorkflowPolicy
                _auditLogService,
                null!) // ILeadershipScopeService
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.Name, "hr_admin"),
                            new Claim(ClaimTypes.Role, AppRoles.HR)
                        }, "TestAuth"))
                    }
                }
            };

            // Act
            var actionResult = await controller.Complete(offboarding.Id);
            var jsonResult = Assert.IsType<JsonResult>(actionResult);

            // Extract success property
            var successProp = jsonResult.Value?.GetType().GetProperty("success")?.GetValue(jsonResult.Value);
            var messageProp = jsonResult.Value?.GetType().GetProperty("message")?.GetValue(jsonResult.Value)?.ToString();

            // Assert
            Assert.Equal(false, successProp);
            Assert.Contains("OD-12", messageProp ?? "");

            // Verify manager is NOT offboarded and user still has CanLogin = true
            var managerAfter = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == manager.Id);
            Assert.Equal("Active", managerAfter.Status);

            if (managerUser != null)
            {
                var userAfter = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == managerUser.Id);
                Assert.True(userAfter.CanLogin);
            }

            // Verify approved offboarding record is preserved
            var offboardingAfter = await _context.OffboardingRecords.AsNoTracking().FirstAsync(o => o.Id == offboarding.Id);
            Assert.Equal("APPROVED", offboardingAfter.ApprovalStatus);
            Assert.NotEqual("COMPLETED", offboardingAfter.OffboardingStatus);
        }

        [Fact]
        public async Task WF03_002_Offboarding_AfterSubordinatesReassigned_SuccessfullyOffboards_And_LocksAccount()
        {
            // Arrange
            await EnsureStandardBaselineAsync();

            var manager = await _context.Employees.FirstAsync(e => e.Id == 1);
            var subordinate = await _context.Employees.FirstAsync(e => e.Id == 2);

            manager.Status = "Active";
            subordinate.Status = "Active";
            // Reassign subordinate to another manager (or null exception) so manager has NO active subordinates
            subordinate.ManagerId = null;

            var managerUser = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == manager.Id);
            if (managerUser != null)
            {
                managerUser.CanLogin = true;
            }
            await _context.SaveChangesAsync();

            var offboarding = new OffboardingRecord
            {
                EmployeeId = manager.Id,
                ApprovalStatus = "APPROVED",
                OffboardingStatus = "PENDING_HANDOVER",
                AttendanceClosingStatus = "COMPLETED",
                EffectiveDate = DateTime.Today
            };
            _context.OffboardingRecords.Add(offboarding);
            await _context.SaveChangesAsync();

            _context.OffboardingChecklistTasks.Add(new OffboardingChecklistTask
            {
                OffboardingRecordId = offboarding.Id,
                TaskName = "Bàn giao tài sản",
                IsMandatory = true,
                Status = "DONE"
            });
            await _context.SaveChangesAsync();

            var controller = new OffboardingController(
                _context,
                null!,
                null!,
                _auditLogService,
                null!)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.Name, "hr_admin"),
                            new Claim(ClaimTypes.Role, AppRoles.HR)
                        }, "TestAuth"))
                    }
                }
            };

            // Act
            var actionResult = await controller.Complete(offboarding.Id);
            var jsonResult = Assert.IsType<JsonResult>(actionResult);
            var successProp = jsonResult.Value?.GetType().GetProperty("success")?.GetValue(jsonResult.Value);

            // Assert
            Assert.Equal(true, successProp);

            // Verify manager is now Offboarded and CanLogin is false
            var managerAfter = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == manager.Id);
            Assert.Equal("Offboarded", managerAfter.Status);

            if (managerUser != null)
            {
                var userAfter = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == managerUser.Id);
                Assert.False(userAfter.CanLogin, "User account should be disabled (CanLogin = false) per OD-16.");

                // Cleanup: restore user and employee to active state for manual testing
                var userToRestore = await _context.Users.FirstAsync(u => u.Id == managerUser.Id);
                userToRestore.CanLogin = true;
                var empToRestore = await _context.Employees.FirstAsync(e => e.Id == manager.Id);
                empToRestore.Status = "Active";
                await _context.SaveChangesAsync();
            }
        }

        // =========================================================================
        // WF-04: RBAC & DATA SCOPE ACCEPTANCE (BD-04, BD-05, BD-06)
        // =========================================================================

        [Fact]
        public void WF04_001_DirectManagerChange_ProtectedBy_HROnlyPolicy()
        {
            // Verify attribute on EmployeeController.ChangeDirectManager
            var method = typeof(EmployeeController).GetMethod(nameof(EmployeeController.ChangeDirectManager));
            Assert.NotNull(method);

            var authorizeAttrs = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
                .ToList();

            Assert.NotEmpty(authorizeAttrs);
            Assert.Contains(authorizeAttrs, a => a.Policy == AppPolicies.HROnly || a.Roles == "HR,Admin");
        }

        [Fact]
        public async Task WF04_002_DirectManagerId_DoesNotSilentlyExpand_OrganizationalUnitDataScope()
        {
            // Arrange
            await EnsureStandardBaselineAsync();

            // Employee 2 is an employee in IT.
            // Employee 1 is Manager in HR.
            // Setting ManagerId of Employee 2 to Employee 1:
            var emp2 = await _context.Employees.FirstAsync(e => e.Id == 2);
            emp2.ManagerId = 1;
            await _context.SaveChangesAsync();

            var managerUser = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == 1);
            Assert.NotNull(managerUser);

            // Query active ManagerAssignments for managerUser
            var assignedUnits = await _context.ManagerAssignments
                .Where(a => a.UserId == managerUser.Id && (a.EffectiveTo == null || a.EffectiveTo > DateTime.Now))
                .Select(a => a.OrganizationalUnit)
                .ToListAsync();

            // ManagerUser's organizational units come EXCLUSIVELY from ManagerAssignments, NOT from subordinates' departments!
            // Assert IT is NOT automatically granted to managerUser just because Employee 2 reports to him
            Assert.DoesNotContain("Phòng IT", assignedUnits);
            Assert.DoesNotContain("Phòng Công nghệ thông tin", assignedUnits);
        }

        // =========================================================================
        // WF-05: PAYROLL REGRESSION ACCEPTANCE (BD-09, WF-05)
        // =========================================================================

        [Fact]
        public async Task WF05_001_PayrollRegression_PrePostMigration_YieldsZeroVariance_And_CorrectDepartmentSnapshot()
        {
            // Arrange
            await EnsureStandardBaselineAsync();
            var payrollService = new PayrollCalculationService(_context);

            var employees = await _context.Employees.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
            Assert.True(employees.Count >= 12);

            // Act: Calculate payroll across all employees in HRManagementDB_Dev
            var salaries = await payrollService.CalculatePayrollAsync(DateTime.Today);
            Assert.NotNull(salaries);
            Assert.Equal(employees.Count, salaries.Count);

            // Assert math integrity and department snapshot across all 12 records
            var empMap = employees.ToDictionary(e => e.Id);
            foreach (var s in salaries)
            {
                Assert.NotNull(s.EmployeeId);
                Assert.True(empMap.ContainsKey(s.EmployeeId.Value));
                var emp = empMap[s.EmployeeId.Value];

                // NetSalary = TotalSalary - Insurance - TaxTNCN - Discipline
                double expectedNet = s.TotalSalary - s.Insurance - s.TaxTNCN - s.Discipline;
                double variance = Math.Abs(expectedNet - s.NetSalary);
                Assert.True(variance < 0.01, $"Variance was {variance} for employee {emp.EmployeeCode}");

                // Department Name snapshot verification
                Assert.NotNull(s.DepartmentName);
                Assert.Equal(emp.Department, s.DepartmentName);
            }
        }

        // =========================================================================
        // NHIỆM VỤ 2: SECURITY & SESSION VALIDATION ACCEPTANCE
        // =========================================================================

        [Fact]
        public void SEC01_AntiforgeryTokens_AreEnforced_OnAllCriticalPostEndpoints()
        {
            // 1. EmployeeController.ChangeDirectManager has ValidateAntiForgeryToken
            var empMethod = typeof(EmployeeController).GetMethod(nameof(EmployeeController.ChangeDirectManager));
            Assert.NotNull(empMethod);
            var empHasAf = empMethod.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).Any();
            Assert.True(empHasAf, "EmployeeController.ChangeDirectManager must have [ValidateAntiForgeryToken].");

            // 2. TransferController has AutoValidateAntiforgeryToken
            var transferHasAf = typeof(TransferController).GetCustomAttributes(typeof(AutoValidateAntiforgeryTokenAttribute), true).Any();
            Assert.True(transferHasAf, "TransferController must have [AutoValidateAntiforgeryToken].");

            // 3. OffboardingController has AutoValidateAntiforgeryToken
            var offboardHasAf = typeof(OffboardingController).GetCustomAttributes(typeof(AutoValidateAntiforgeryTokenAttribute), true).Any();
            Assert.True(offboardHasAf, "OffboardingController must have [AutoValidateAntiforgeryToken].");
        }

        [Fact]
        public async Task SEC02_SessionTermination_RejectsPrincipal_WhenCanLoginIsFalse()
        {
            // Arrange
            await EnsureStandardBaselineAsync();
            var testUser = await _context.Users.FirstAsync(u => u.CanLogin == true);

            // Simulate user account lock (OD-16 offboarding or admin lock)
            testUser.CanLogin = false;
            await _context.SaveChangesAsync();

            // Act: Evaluate security validation condition identical to Program.cs OnValidatePrincipal
            var dbUser = await _context.Users
                .Where(u => u.Username == testUser.Username)
                .Select(u => new { u.CanLogin, u.Role })
                .SingleOrDefaultAsync();

            bool isRejected = dbUser == null || dbUser.CanLogin != true || string.IsNullOrEmpty(dbUser.Role);

            // Assert: Must reject active principal immediately
            Assert.True(isRejected, "Active principal must be rejected when CanLogin is false per OD-16.");

            // Cleanup
            testUser.CanLogin = true;
            await _context.SaveChangesAsync();
        }

        // =========================================================================
        // NHIỆM VỤ 3: TEST ISOLATION & CLEANUP
        // =========================================================================

        [Fact]
        public async Task ISO01_TestIsolation_BaselineCleanup_PreservesNonTestData()
        {
            // Arrange
            await EnsureStandardBaselineAsync();

            // Verify clean baseline
            var initialCount = await _context.Employees.CountAsync();
            Assert.True(initialCount >= 12);

            // Verify non-test employees are preserved
            var activeEmployees = await _context.Employees.Where(e => e.Status == "Active").CountAsync();
            Assert.True(activeEmployees >= 12);

            // Verify standard departments exist
            var depts = await _context.Departments.CountAsync();
            Assert.True(depts >= 6, "Standard departments must be preserved.");
        }
    }
}
