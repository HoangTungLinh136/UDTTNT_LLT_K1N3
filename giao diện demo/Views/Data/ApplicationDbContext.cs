using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System;
using giao_dien_demo.Models.AI;

namespace giao_dien_demo.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor = null) : base(options) 
        { 
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<BackupRecord> BackupRecords { get; set; }
        public DbSet<BackupConfig> BackupConfigs { get; set; }
        public DbSet<SystemConfig> SystemConfigs { get; set; }
        
        // --- MODULE SYSTEM MONITORING ---
        public DbSet<SystemErrorLog> SystemErrorLogs { get; set; }
        public DbSet<MonitoringAlert> MonitoringAlerts { get; set; }

        public DbSet<User> Users { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<EmployeeReportingHistory> EmployeeReportingHistories { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Attendance> Attendances { get; set; } // Giữ lại 1 dòng này thôi
        public DbSet<AttendancePeriod> AttendancePeriods { get; set; }
        public DbSet<AttendanceRuleVersion> AttendanceRuleVersions { get; set; }
        public DbSet<Salary> Salaries { get; set; }
        public DbSet<Leave> Leaves { get; set; }
        public DbSet<WorkShift> WorkShifts { get; set; }
        public DbSet<WorkShiftAssignment> WorkShiftAssignments { get; set; }
        public DbSet<Holiday> Holidays { get; set; }

        // 🔥 ĐÃ XÓA dòng Attendances bị trùng ở đây

        public DbSet<Contract> Contracts { get; set; }

        public DbSet<JobPosition> JobPositions { get; set; }
        
        // public DbSet<Leader> Leaders { get; set; }
        public DbSet<SalaryGradeConfig> SalaryGradeConfigs { get; set; }
        
        // --- BẢNG CẤU HÌNH HIỆU SUẤT (MỚI) ---
        public DbSet<JobTitleConfig> JobTitleConfigs { get; set; }
        public DbSet<ExperienceConfig> ExperienceConfigs { get; set; }
        public DbSet<KPIConfig> KPIConfigs { get; set; }

        // --- MODULE PERFORMANCE MANAGEMENT (MỚI) ---
        public DbSet<KPIPeriod> KPIPeriods { get; set; }
        public DbSet<KPIEvaluation> KPIEvaluations { get; set; }
        public DbSet<KPITarget> KPITargets { get; set; }

        // --- MODULE THANH TOÁN LƯƠNG (MỚI - ĐỘC LẬP) ---
        public DbSet<PayrollPayment> PayrollPayments { get; set; }

        // Nếu bạn muốn lưu Báo cáo vào DB luôn để không bị reset, hãy bỏ comment dòng dưới:
        // public DbSet<Report> Reports { get; set; }

        // --- MODULE YÊU CẦU ĐIỀU CHỈNH CHẤM CÔNG (MỚI) ---
        public DbSet<AttendanceAdjustmentRequest> AttendanceAdjustmentRequests { get; set; }
        public DbSet<AttendanceAdjustmentAttachment> AttendanceAdjustmentAttachments { get; set; }

        // --- MODULE HỘI NHẬP NHÂN VIÊN ---
        public DbSet<OnboardingRecord> OnboardingRecords { get; set; }
        public DbSet<OnboardingChecklistTask> OnboardingChecklistTasks { get; set; }

        // --- MODULE BỔ NHIỆM ---
        public DbSet<AppointmentRecord> AppointmentRecords { get; set; }
        public DbSet<AppointmentHistory> AppointmentHistories { get; set; }

        // --- MODULE MIỄN NHIỆM ---
        public DbSet<DismissalRecord> DismissalRecords { get; set; }
        public DbSet<DismissalHistory> DismissalHistories { get; set; }

        // --- MODULE THUYÊN CHUYỂN ---
        public DbSet<TransferRecord> TransferRecords { get; set; }
        public DbSet<TransferHistory> TransferHistories { get; set; }

        // --- MODULE NGHỈ VIỆC ---
        public DbSet<OffboardingRecord> OffboardingRecords { get; set; }
        public DbSet<OffboardingHistory> OffboardingHistories { get; set; }
        public DbSet<OffboardingChecklistTask> OffboardingChecklistTasks { get; set; }

        // --- MODULE KHEN THƯỞNG ---
        public DbSet<RewardRecord> RewardRecords { get; set; }
        public DbSet<RewardHistory> RewardHistories { get; set; }

        // --- MODULE CAMERA IMOU ---
        public DbSet<CameraConfig> CameraConfigs { get; set; }
        public DbSet<CameraEvent> CameraEvents { get; set; }

        // --- MODULE SỰ CỐ ---
        public DbSet<IncidentRecord> IncidentRecords { get; set; }
        public DbSet<IncidentParticipant> IncidentParticipants { get; set; }
        public DbSet<IncidentWitness> IncidentWitnesses { get; set; }
        public DbSet<IncidentEvidence> IncidentEvidences { get; set; }
        public DbSet<IncidentHistory> IncidentHistories { get; set; }
        // --- MODULE QUY HOẠCH ---
        public DbSet<PlanningPeriod> PlanningPeriods { get; set; }
        public DbSet<SuccessionPlan> SuccessionPlans { get; set; }
        public DbSet<SuccessionCandidate> SuccessionCandidates { get; set; }
        public DbSet<CandidateDevelopmentPlan> CandidateDevelopmentPlans { get; set; }
        public DbSet<SuccessionHistory> SuccessionHistories { get; set; }

        // --- MODULE ĐÔN ĐỐC ---
        public DbSet<HrTask> HrTasks { get; set; }
        public DbSet<HrTaskHistory> HrTaskHistories { get; set; }

        // --- MODULE ĐỀ XUẤT ---
        public DbSet<Proposal> Proposals { get; set; }
        public DbSet<ProposalHistory> ProposalHistories { get; set; }

        // --- MODULE PHÚC LỢI (BENEFITS) ---
        public DbSet<BenefitProgram> BenefitPrograms { get; set; }
        public DbSet<BenefitEntitlement> BenefitEntitlements { get; set; }
        public DbSet<BenefitEnrollment> BenefitEnrollments { get; set; }

        public DbSet<TrainingCourse> TrainingCourses { get; set; }
        public DbSet<CourseEnrollment> CourseEnrollments { get; set; }
        public DbSet<TrainingCertificate> TrainingCertificates { get; set; }
        public DbSet<TrainingMaterial> TrainingMaterials { get; set; }

        public DbSet<NotificationRecipientGroup> NotificationRecipientGroups { get; set; }
        public DbSet<EmailTemplate> EmailTemplates { get; set; }
        public DbSet<NotificationRule> NotificationRules { get; set; }
        public DbSet<NotificationLog> NotificationLogs { get; set; }
        public DbSet<EventOutboxMessage> EventOutboxMessages { get; set; }

        // --- MODULE MANAGER SCOPE (P26) ---
        public DbSet<ManagerAssignment> ManagerAssignments { get; set; }

        // --- MODULE LEADER SCOPE (P29) ---
        public DbSet<LeaderAssignment> LeaderAssignments { get; set; }

        // --- MODULE AVA CONVERSATIONS ---
        public DbSet<AiConversation> AiConversations { get; set; }
        public DbSet<AiMessage> AiMessages { get; set; }

        // --- MODULE AVA PERSONAL MEMORY ---
        public DbSet<UserMemory> UserMemories { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.ConfigureWarnings(w => w
                .Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)
                .Ignore(Microsoft.EntityFrameworkCore.Diagnostics.SqlServerEventId.SavepointsDisabledBecauseOfMARS));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            Folder1SchemaCompatibility.Apply(modelBuilder);

            // Configure all decimal properties with explicit column type to eliminate EF Core truncation warnings
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }

            // Tự động chặn cascade delete cho tất cả các quan hệ tới Employee
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                if (relationship.PrincipalEntityType.ClrType == typeof(Employee))
                {
                    relationship.DeleteBehavior = DeleteBehavior.Restrict;
                }
            }

            // Đảm bảo EmployeeCode và Username là Unique
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.EmployeeCode)
                .IsUnique();

                        modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Filtered Unique Index on Users.EmployeeId (OD-15)
            modelBuilder.Entity<User>()
                .HasIndex(u => u.EmployeeId)
                .IsUnique()
                .HasFilter("[EmployeeId] IS NOT NULL");

            // Department Configuration (OD-01, OD-13, OD-19, CR-01)
            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.HasIndex(d => d.Code).IsUnique();
                entity.Property(d => d.Code).IsRequired().HasMaxLength(20);
                entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
                entity.Property(d => d.IsActive).HasDefaultValue(true);
                entity.HasMany(d => d.Employees)
                    .WithOne(e => e.DepartmentEntity)
                    .HasForeignKey(e => e.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Employee Manager & Department Indexes (OD-02, OD-07, OD-08, OD-18, OD-24)
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasIndex(e => e.DepartmentId);
                entity.HasIndex(e => e.ManagerId);
                entity.HasOne(e => e.Manager)
                    .WithMany(m => m.Subordinates)
                    .HasForeignKey(e => e.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.ToTable(t => t.HasCheckConstraint("CK_Employees_ManagerId_NotSelf", "[ManagerId] IS NULL OR [ManagerId] <> [Id]"));
            });

            // EmployeeReportingHistory Configuration (OD-09, OD-23)
            modelBuilder.Entity<EmployeeReportingHistory>(entity =>
            {
                entity.HasKey(h => h.Id);
                entity.Property(h => h.ChangeReason).IsRequired().HasMaxLength(500);
                entity.HasIndex(h => new { h.EmployeeId, h.EffectiveDate });
                entity.HasOne(h => h.Employee)
                    .WithMany()
                    .HasForeignKey(h => h.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(h => h.PreviousManager)
                    .WithMany()
                    .HasForeignKey(h => h.PreviousManagerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(h => h.NewManager)
                    .WithMany()
                    .HasForeignKey(h => h.NewManagerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(h => h.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(h => h.ChangedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AiConversation>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasMaxLength(128);
                entity.Property(x => x.Title).HasMaxLength(200);
                entity.HasIndex(x => new { x.UserId, x.UpdatedAt });
                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AiMessage>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ConversationId).HasMaxLength(128);
                entity.Property(x => x.Role).HasMaxLength(20);
                entity.Property(x => x.Route).HasMaxLength(40);
                entity.Property(x => x.Intent).HasMaxLength(120);
                entity.Property(x => x.WarningCode).HasMaxLength(120);
                entity.HasIndex(x => new { x.ConversationId, x.CreatedAt });
                entity.HasOne(x => x.Conversation)
                    .WithMany(x => x.Messages)
                    .HasForeignKey(x => x.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // -----------------------------------------------------
            // AVA PERSONAL MEMORY
            // -----------------------------------------------------
            modelBuilder.Entity<UserMemory>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.UserId)
                    .IsRequired()
                    .HasMaxLength(128);

                entity.Property(x => x.EmployeeId)
                    .HasMaxLength(128);

                entity.Property(x => x.MemoryType)
                    .IsRequired()
                    .HasMaxLength(64);

                entity.Property(x => x.MemoryKey)
                    .IsRequired()
                    .HasMaxLength(128);

                entity.Property(x => x.Content)
                    .IsRequired()
                    .HasMaxLength(4000);

                entity.Property(x => x.Source)
                    .IsRequired()
                    .HasMaxLength(64);

                entity.Property(x => x.SourceConversationId)
                    .HasMaxLength(128);

                entity.Property(x => x.Confidence)
                    .HasDefaultValue(1.0);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.CreatedAtUtc)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(x => x.UpdatedAtUtc)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                // Query chính của Phase 05:
                // lấy memory đang active theo user.
                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.IsActive
                })
                .HasDatabaseName(
                    "IX_UserMemories_UserId_IsActive"
                );

                // Hỗ trợ lọc theo loại/key memory.
                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.MemoryType,
                    x.MemoryKey,
                    x.IsActive
                })
                .HasDatabaseName(
                    "IX_UserMemories_User_Type_Key_Active"
                );

                entity.HasIndex(x => x.SourceConversationId)
                    .HasDatabaseName(
                        "IX_UserMemories_SourceConversationId"
                    );
            });


            // Seed dữ liệu mẫu cho Phúc lợi
            modelBuilder.Entity<BenefitProgram>().HasData(
                new BenefitProgram { Id = 1, Name = "Bảo hiểm Xã hội (BHXH)", Description = "Bảo hiểm theo quy định của nhà nước.", BenefitType = "Insurance", Status = "Open", IsAutoApplied = true, RequiresApproval = false },
                new BenefitProgram { Id = 2, Name = "Bảo hiểm Y tế (BHYT)", Description = "Khám chữa bệnh BHYT.", BenefitType = "Insurance", Status = "Open", IsAutoApplied = true, RequiresApproval = false },
                new BenefitProgram { Id = 3, Name = "Khám sức khỏe định kỳ 2026", Description = "Khám tổng quát tại bệnh viện Thu Cúc.", BenefitType = "Health", Status = "Open", RegistrationStart = new DateTime(2026, 9, 1), RegistrationEnd = new DateTime(2026, 9, 30), Capacity = 100 },
                new BenefitProgram { Id = 4, Name = "Phụ cấp ăn trưa", Description = "Phụ cấp 35k/ngày đi làm.", BenefitType = "Allowance", Status = "Open", IsAutoApplied = true, RequiresApproval = false }
            );

            // Bơm sẵn danh sách chức vụ
            modelBuilder.Entity<JobPosition>().HasData(
                new JobPosition { Id = 1, Group = "1. Ban Giám đốc", Name = "Giám đốc điều hành (CEO)", MinSalary = 60000000 },
                new JobPosition { Id = 2, Group = "1. Ban Giám đốc", Name = "Giám đốc Kỹ thuật (CTO)", MinSalary = 50000000 },
                new JobPosition { Id = 3, Group = "1. Ban Giám đốc", Name = "Giám đốc Nhân sự (CHRO)", MinSalary = 40000000 },
                new JobPosition { Id = 4, Group = "2. Cấp Quản lý", Name = "Quản lý Dự án (PM)", MinSalary = 30000000 },
                new JobPosition { Id = 5, Group = "2. Cấp Quản lý", Name = "Trưởng phòng Kỹ thuật", MinSalary = 30000000 },
                new JobPosition { Id = 6, Group = "2. Cấp Quản lý", Name = "Trưởng phòng HR / Sales", MinSalary = 20000000 },
                new JobPosition { Id = 7, Group = "3. Cấp Nhân viên", Name = "Lập trình viên (Senior)", MinSalary = 25000000 },
                new JobPosition { Id = 8, Group = "3. Cấp Nhân viên", Name = "Lập trình viên (Junior/Mid)", MinSalary = 12000000 },
                new JobPosition { Id = 9, Group = "3. Cấp Nhân viên", Name = "Phân tích nghiệp vụ (BA)", MinSalary = 15000000 },
                new JobPosition { Id = 10, Group = "3. Cấp Nhân viên", Name = "Kiểm thử phần mềm (Tester/QA)", MinSalary = 10000000 },
                new JobPosition { Id = 11, Group = "4. Khối Back-Office", Name = "Chuyên viên Nhân sự", MinSalary = 9000000 },
                new JobPosition { Id = 12, Group = "4. Khối Back-Office", Name = "Kế toán viên", MinSalary = 9000000 },
                new JobPosition { Id = 13, Group = "4. Khối Back-Office", Name = "Nhân viên Kinh doanh (Sales)", MinSalary = 7000000 },
                new JobPosition { Id = 14, Group = "5. Thực tập sinh", Name = "Thực tập sinh (Intern)", MinSalary = 3000000 }
            );

            // Seed metadata must be deterministic; DateTime.Now here causes a
            // permanent false-positive pending migration on every model build.
            var seededSystemConfigUpdatedAt = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Unspecified);

            // Seed SystemConfig
            modelBuilder.Entity<SystemConfig>().HasData(
                new SystemConfig { Key = "Company.Name", Value = "Công ty CP Công nghệ HRMS", Category = "Company", Description = "Tên công ty", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                new SystemConfig { Key = "Company.ShortName", Value = "HRMS Corp", Category = "Company", Description = "Tên viết tắt", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                new SystemConfig { Key = "Company.TaxCode", Value = "0101234567", Category = "Company", Description = "Mã số thuế", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                new SystemConfig { Key = "Company.Email", Value = "contact@hrms.vn", Category = "Company", Description = "Email liên hệ", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                new SystemConfig { Key = "Company.Phone", Value = "1900 1000", Category = "Company", Description = "Hotline", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                new SystemConfig { Key = "Company.Website", Value = "https://hrms.vn", Category = "Company", Description = "Website", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                new SystemConfig { Key = "Company.Address", Value = "Tầng 12, Tòa nhà Tech, Hà Nội", Category = "Company", Description = "Địa chỉ trụ sở", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                new SystemConfig { Key = "Company.Representative", Value = "Nguyễn Văn A", Category = "Company", Description = "Người đại diện", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                
                new SystemConfig { Key = "Loc.Language", Value = "vi-VN", Category = "Localization", Description = "Ngôn ngữ mặc định", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                new SystemConfig { Key = "Loc.Timezone", Value = "SE Asia Standard Time", Category = "Localization", Description = "Múi giờ", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                new SystemConfig { Key = "Loc.Currency", Value = "VND", Category = "Localization", Description = "Tiền tệ", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },

                new SystemConfig { Key = "Storage.MaxFileSizeMB", Value = "10", Category = "Storage", Description = "Dung lượng upload tối đa (MB)", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },
                new SystemConfig { Key = "Storage.AllowedTypes", Value = ".jpg,.jpeg,.png,.pdf,.doc,.docx,.xls,.xlsx", Category = "Storage", Description = "Định dạng cho phép", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" },

                new SystemConfig { Key = "Sys.MaintenanceMode", Value = "false", Category = "System", Description = "Chế độ bảo trì", UpdatedAt = seededSystemConfigUpdatedAt, UpdatedBy = "System" }
            );

            // --- P42: Fix ManagerAssignment cascade + unique index ---
            modelBuilder.Entity<ManagerAssignment>(entity =>
            {
                entity.Property(m => m.OrganizationalUnit)
                    .HasMaxLength(450);

                entity.HasOne(m => m.User)
                    .WithMany()
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(m => new { m.UserId, m.OrganizationalUnit })
                    .HasDatabaseName("IX_ManagerAssignments_UserId_OrgUnit");
            });

            // --- P42: Fix LeaderAssignment cascade + unique index ---
            modelBuilder.Entity<LeaderAssignment>(entity =>
            {
                entity.Property(l => l.OrganizationalUnit)
                    .HasMaxLength(450);

                entity.HasOne(l => l.User)
                    .WithMany()
                    .HasForeignKey(l => l.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => new { l.UserId, l.OrganizationalUnit })
                    .IsUnique()
                    .HasDatabaseName("IX_LeaderAssignments_UserId_OrgUnit_Unique");
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(n => n.RecipientUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(n => n.SenderUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(n => n.DeduplicationKey)
                    .IsUnique()
                    .HasFilter("[DeduplicationKey] IS NOT NULL");
            });

            // --- P42: HrTask has two Employee FKs — disambiguate ---
            modelBuilder.Entity<HrTask>(entity =>
            {
                entity.HasOne(t => t.AssigneeEmployee)
                    .WithMany()
                    .HasForeignKey(t => t.AssigneeEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.RelatedEmployee)
                    .WithMany()
                    .HasForeignKey(t => t.RelatedEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // --- P42: Proposal self-referencing FK ---
            modelBuilder.Entity<Proposal>(entity =>
            {
                entity.HasOne(p => p.SourceProposal)
                    .WithMany()
                    .HasForeignKey(p => p.SourceProposalId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.RelatedEmployee)
                    .WithMany()
                    .HasForeignKey(p => p.RelatedEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // --- P3-A2: KPI Performance Management Core ---
            modelBuilder.Entity<KPIEvaluation>(entity =>
            {
                entity.HasOne(e => e.Period)
                    .WithMany()
                    .HasForeignKey(e => e.PeriodId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Employee)
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ManagerUser)
                    .WithMany()
                    .HasForeignKey(e => e.ManagerUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Unique index (PeriodId, EmployeeId)
                entity.HasIndex(e => new { e.PeriodId, e.EmployeeId })
                    .IsUnique()
                    .HasDatabaseName("IX_KPIEvaluations_PeriodId_EmployeeId_Unique");
            });

            modelBuilder.Entity<KPITarget>(entity =>
            {
                entity.HasOne(t => t.Evaluation)
                    .WithMany(e => e.Targets)
                    .HasForeignKey(t => t.EvaluationId)
                    .OnDelete(DeleteBehavior.Cascade); // Application logic restricts deleting COMPLETED evaluations
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var auditEntries = OnBeforeSaveChanges();
            var result = await base.SaveChangesAsync(cancellationToken);
            await OnAfterSaveChanges(auditEntries);
            return result;
        }

        private List<AuditEntry> OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog ||
                    entry.Entity is NotificationLog ||
                    entry.Entity is UserMemory ||
                    entry.State == EntityState.Detached ||
                    entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry);
                auditEntry.TableName = entry.Metadata.GetTableName();
                
                var httpContext = _httpContextAccessor?.HttpContext;
                var session = httpContext?.Features
                    .Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>()?.Session;
                string user = session?.GetString("User") ?? "System";
                string role = session?.GetString("Role") ?? "";
                string ip = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "";
                
                auditEntry.UserId = user;
                auditEntry.Username = user;
                auditEntry.Role = role;
                auditEntry.IPAddress = ip;
                
                auditEntries.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            auditEntry.Action = "Create";
                            break;
                        case EntityState.Deleted:
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.Action = "Delete";
                            break;
                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                                auditEntry.ChangedColumns.Add(propertyName);
                            }
                            auditEntry.Action = "Update";
                            break;
                    }
                }
            }

            foreach (var auditEntry in auditEntries.Where(_ => !_.HasTemporaryProperties))
            {
                AuditLogs.Add(auditEntry.ToAuditLog());
            }

            return auditEntries.Where(_ => _.HasTemporaryProperties).ToList();
        }

        private Task OnAfterSaveChanges(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0)
                return Task.CompletedTask;

            foreach (var auditEntry in auditEntries)
            {
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }
                AuditLogs.Add(auditEntry.ToAuditLog());
            }

            return base.SaveChangesAsync();
        }
    }

    public class AuditEntry
    {
        public AuditEntry(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
        {
            Entry = entry;
        }

        public Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry { get; }
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
        public string IPAddress { get; set; }
        public string TableName { get; set; }
        public string Action { get; set; }
        public Dictionary<string, object> KeyValues { get; } = new Dictionary<string, object>();
        public Dictionary<string, object> OldValues { get; } = new Dictionary<string, object>();
        public Dictionary<string, object> NewValues { get; } = new Dictionary<string, object>();
        public List<Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry> TemporaryProperties { get; } = new List<Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry>();
        public List<string> ChangedColumns { get; } = new List<string>();
        public bool HasTemporaryProperties => TemporaryProperties.Any();

        public AuditLog ToAuditLog()
        {
            var audit = new AuditLog
            {
                Timestamp = DateTime.Now,
                UserId = UserId ?? string.Empty,
                Username = Username ?? string.Empty,
                Role = Role ?? string.Empty,
                Module = "Data",
                Action = Action ?? string.Empty,
                Resource = TableName ?? string.Empty,
                ResourceId = KeyValues.Count == 1 ? KeyValues.Values.First()?.ToString() ?? string.Empty : JsonSerializer.Serialize(KeyValues),
                EventType = "DataChange",
                Status = "Success",
                IPAddress = IPAddress ?? string.Empty,
                OldValues = OldValues.Count == 0 ? string.Empty : JsonSerializer.Serialize(OldValues),
                NewValues = NewValues.Count == 0 ? string.Empty : JsonSerializer.Serialize(NewValues),
                Changes = ChangedColumns.Count == 0 ? string.Empty : JsonSerializer.Serialize(ChangedColumns),
                Details =
                    $"Module=Data; EventType=DataChange; Status=Success; " +
                    $"Role={Role ?? string.Empty}; IP={IPAddress ?? string.Empty}; " +
                    $"OldValues={(OldValues.Count == 0 ? string.Empty : JsonSerializer.Serialize(OldValues))}; " +
                    $"NewValues={(NewValues.Count == 0 ? string.Empty : JsonSerializer.Serialize(NewValues))}; " +
                    $"Changes={(ChangedColumns.Count == 0 ? string.Empty : JsonSerializer.Serialize(ChangedColumns))}"
            };
            return audit;
        }
    }
}


