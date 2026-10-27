IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Attendances] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeName] nvarchar(max) NULL,
    [CheckIn] datetime2 NOT NULL,
    [CheckOut] datetime2 NULL,
    CONSTRAINT [PK_Attendances] PRIMARY KEY ([Id])
);

CREATE TABLE [Leaves] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeName] nvarchar(max) NULL,
    [FromDate] datetime2 NOT NULL,
    [ToDate] datetime2 NOT NULL,
    [Reason] nvarchar(max) NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_Leaves] PRIMARY KEY ([Id])
);

CREATE TABLE [Notifications] (
    [Id] int NOT NULL IDENTITY,
    [Message] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsRead] bit NOT NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
);

CREATE TABLE [Salaries] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [EmployeeName] nvarchar(max) NULL,
    [BasicSalary] float NOT NULL,
    [HoursWorked] float NOT NULL,
    [NumberOfDependents] int NOT NULL,
    [HolidayBonus] float NOT NULL,
    [DiligenceBonus] float NOT NULL,
    [Reward] float NOT NULL,
    [Discipline] float NOT NULL,
    [TotalSalary] float NOT NULL,
    [Insurance] float NOT NULL,
    [TaxTNCN] float NOT NULL,
    [NetSalary] float NOT NULL,
    [SalaryMonth] datetime2 NOT NULL,
    CONSTRAINT [PK_Salaries] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [Username] nvarchar(max) NOT NULL,
    [Password] nvarchar(max) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [IsOnline] bit NOT NULL,
    [LastActive] datetime2 NULL,
    [CanLogin] bit NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260411100804_InitialCreate', N'9.0.0');

CREATE TABLE [Employees] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NULL,
    [Department] nvarchar(max) NULL,
    [Position] nvarchar(max) NULL,
    [Salary] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260411102712_AddEmployeesTableFinal', N'9.0.0');

ALTER TABLE [Employees] ADD [Email] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260411103201_AddEmailColumnToEmployee', N'9.0.0');

ALTER TABLE [Notifications] ADD [NguoiNhan] nvarchar(max) NULL;

CREATE TABLE [Contracts] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeName] nvarchar(max) NULL,
    [Duration] nvarchar(max) NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NULL,
    [Signature] nvarchar(max) NULL,
    [HrSignature] nvarchar(max) NULL,
    [ExtensionDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260411111312_UpdateDbFinal', N'9.0.0');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260411172052_AddContractTable', N'9.0.0');

ALTER TABLE [Employees] ADD [HazardousAllowance] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Employees] ADD [PositionAllowance] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Employees] ADD [RegionalAllowanceSystem] decimal(18,2) NOT NULL DEFAULT 0.0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260413085303_AddPhuCapNhanVien', N'9.0.0');

ALTER TABLE [Salaries] ADD [BonusAndAllowance] float NOT NULL DEFAULT 0.0E0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260414031422_AddThongTinChamCongChuyenSau', N'9.0.0');

ALTER TABLE [Attendances] ADD [LateMinutes] int NOT NULL DEFAULT 0;

ALTER TABLE [Attendances] ADD [OvertimeHours] float NOT NULL DEFAULT 0.0E0;

ALTER TABLE [Attendances] ADD [StandardHours] float NOT NULL DEFAULT 0.0E0;

ALTER TABLE [Attendances] ADD [StatusNote] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260414031644_CapNhatCotChamCong', N'9.0.0');

ALTER TABLE [Employees] ADD [EmployeeCode] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260414042620_ThemMaNhanVienTuDong', N'9.0.0');

CREATE TABLE [JobPositions] (
    [Id] int NOT NULL IDENTITY,
    [Group] nvarchar(max) NULL,
    [Name] nvarchar(max) NULL,
    [MinSalary] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_JobPositions] PRIMARY KEY ([Id])
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Group', N'MinSalary', N'Name') AND [object_id] = OBJECT_ID(N'[JobPositions]'))
    SET IDENTITY_INSERT [JobPositions] ON;
INSERT INTO [JobPositions] ([Id], [Group], [MinSalary], [Name])
VALUES (1, N'1. Ban Giám đốc', 60000000.0, N'Giám đốc điều hành (CEO)'),
(2, N'1. Ban Giám đốc', 50000000.0, N'Giám đốc Kỹ thuật (CTO)'),
(3, N'1. Ban Giám đốc', 40000000.0, N'Giám đốc Nhân sự (CHRO)'),
(4, N'2. Cấp Quản lý', 30000000.0, N'Quản lý Dự án (PM)'),
(5, N'2. Cấp Quản lý', 30000000.0, N'Trưởng phòng Kỹ thuật'),
(6, N'2. Cấp Quản lý', 20000000.0, N'Trưởng phòng HR / Sales'),
(7, N'3. Cấp Nhân viên', 25000000.0, N'Lập trình viên (Senior)'),
(8, N'3. Cấp Nhân viên', 12000000.0, N'Lập trình viên (Junior/Mid)'),
(9, N'3. Cấp Nhân viên', 15000000.0, N'Phân tích nghiệp vụ (BA)'),
(10, N'3. Cấp Nhân viên', 10000000.0, N'Kiểm thử phần mềm (Tester/QA)'),
(11, N'4. Khối Back-Office', 9000000.0, N'Chuyên viên Nhân sự'),
(12, N'4. Khối Back-Office', 9000000.0, N'Kế toán viên'),
(13, N'4. Khối Back-Office', 7000000.0, N'Nhân viên Kinh doanh (Sales)'),
(14, N'5. Thực tập sinh', 3000000.0, N'Thực tập sinh (Intern)');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Group', N'MinSalary', N'Name') AND [object_id] = OBJECT_ID(N'[JobPositions]'))
    SET IDENTITY_INSERT [JobPositions] OFF;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260415031256_AddJobPositionsTable', N'9.0.0');

ALTER TABLE [Employees] ADD [JobTitle] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260415032318_AddJobTitleToEmployee', N'9.0.0');

ALTER TABLE [Employees] ADD [Address] nvarchar(max) NULL;

ALTER TABLE [Employees] ADD [AvatarPath] nvarchar(max) NULL;

ALTER TABLE [Employees] ADD [BankAccountNumber] nvarchar(max) NULL;

ALTER TABLE [Employees] ADD [BankName] nvarchar(max) NULL;

ALTER TABLE [Employees] ADD [BirthDate] datetime2 NULL;

ALTER TABLE [Employees] ADD [CitizenId] nvarchar(max) NULL;

ALTER TABLE [Employees] ADD [Gender] nvarchar(max) NULL;

ALTER TABLE [Employees] ADD [PhoneNumber] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260415083442_UpdateEmployeeProfileFields', N'9.0.0');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260415084826_BoSungHoSoCaNhan', N'9.0.0');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260415091012_UpdateProfileFields', N'9.0.0');

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Leaves]') AND [c].[name] = N'Status');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Leaves] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Leaves] ALTER COLUMN [Status] nvarchar(max) NULL;

ALTER TABLE [Leaves] ADD [EmployeeSignature] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260421163533_UpdateDbStructureAndSeedData', N'9.0.0');

ALTER TABLE [Employees] ADD [AcademicLevel] nvarchar(max) NULL;

ALTER TABLE [Employees] ADD [QualificationAllowance] decimal(18,2) NOT NULL DEFAULT 0.0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260426115537_AddAcademicLevelToEmployee', N'9.0.0');

CREATE TABLE [SalaryGradeConfigs] (
    [Id] int NOT NULL IDENTITY,
    [AcademicLevel] nvarchar(max) NOT NULL,
    [SalaryRank] int NOT NULL,
    [Coefficient] decimal(18,2) NOT NULL,
    [SuggestedAllowance] decimal(18,2) NOT NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_SalaryGradeConfigs] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260808092845_AddSalaryGradeConfig', N'9.0.0');

ALTER TABLE [Employees] ADD [ExperienceYears] int NOT NULL DEFAULT 0;

ALTER TABLE [Employees] ADD [KPIScore] nvarchar(max) NULL;

CREATE TABLE [ExperienceConfigs] (
    [Id] int NOT NULL IDENTITY,
    [MinYears] int NOT NULL,
    [MaxYears] int NOT NULL,
    [Coefficient] decimal(18,2) NOT NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_ExperienceConfigs] PRIMARY KEY ([Id])
);

CREATE TABLE [JobTitleConfigs] (
    [Id] int NOT NULL IDENTITY,
    [JobTitle] nvarchar(max) NOT NULL,
    [Coefficient] decimal(18,2) NOT NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_JobTitleConfigs] PRIMARY KEY ([Id])
);

CREATE TABLE [KPIConfigs] (
    [Id] int NOT NULL IDENTITY,
    [KPIRating] nvarchar(max) NOT NULL,
    [Coefficient] decimal(18,2) NOT NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_KPIConfigs] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260808155916_AddPerformanceSalaryFields', N'9.0.0');

CREATE TABLE [PayrollPayments] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [EmployeeCode] nvarchar(max) NULL,
    [EmployeeName] nvarchar(max) NULL,
    [Department] nvarchar(max) NULL,
    [BankName] nvarchar(max) NULL,
    [BankAccountNumber] nvarchar(max) NULL,
    [NetSalary] float NOT NULL,
    [PayrollPeriod] nvarchar(max) NULL,
    [Status] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [PaidAt] datetime2 NULL,
    [PaymentNote] nvarchar(max) NULL,
    CONSTRAINT [PK_PayrollPayments] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260809153017_AddPayrollPaymentTable', N'9.0.0');

ALTER TABLE [PayrollPayments] ADD [ConfirmedAt] datetime2 NULL;

ALTER TABLE [PayrollPayments] ADD [ConfirmedBy] nvarchar(max) NULL;

ALTER TABLE [PayrollPayments] ADD [CycleEndDate] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';

ALTER TABLE [PayrollPayments] ADD [CycleStartDate] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';

ALTER TABLE [PayrollPayments] ADD [PaymentDate] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';

ALTER TABLE [PayrollPayments] ADD [WorkDays] float NOT NULL DEFAULT 0.0E0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260810150408_AddPayrollCycleFields', N'9.0.0');

ALTER TABLE [Attendances] ADD [AdjustmentReason] nvarchar(max) NULL;

ALTER TABLE [Attendances] ADD [AdjustmentStatus] nvarchar(max) NULL;

ALTER TABLE [Attendances] ADD [RequestedCheckOutTime] time NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260811162843_AddAttendanceAdjustmentFields', N'9.0.0');

ALTER TABLE [Notifications] ADD [NotificationType] nvarchar(max) NULL;

ALTER TABLE [Notifications] ADD [ReferenceId] int NULL;

ALTER TABLE [Notifications] ADD [TargetUrl] nvarchar(max) NULL;

CREATE TABLE [AttendanceAdjustmentAttachments] (
    [Id] int NOT NULL IDENTITY,
    [RequestId] int NOT NULL,
    [FileName] nvarchar(max) NOT NULL,
    [FilePath] nvarchar(max) NOT NULL,
    [ContentType] nvarchar(max) NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AttendanceAdjustmentAttachments] PRIMARY KEY ([Id])
);

CREATE TABLE [AttendanceAdjustmentRequests] (
    [Id] int NOT NULL IDENTITY,
    [AttendanceId] int NOT NULL,
    [EmployeeCode] nvarchar(max) NOT NULL,
    [RequestedCheckIn] time NULL,
    [RequestedCheckOut] time NULL,
    [Reason] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [ReviewedBy] nvarchar(max) NULL,
    [ReviewedAt] datetime2 NULL,
    [RejectionReason] nvarchar(max) NULL,
    CONSTRAINT [PK_AttendanceAdjustmentRequests] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260811165139_AddAttendanceRequestsAndUploads', N'9.0.0');

CREATE TABLE [CameraConfigs] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [IPAddress] nvarchar(100) NOT NULL,
    [RtspUrl] nvarchar(500) NOT NULL,
    [Username] nvarchar(100) NOT NULL,
    [Password] nvarchar(255) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_CameraConfigs] PRIMARY KEY ([Id])
);

CREATE TABLE [CameraEvents] (
    [Id] int NOT NULL IDENTITY,
    [CameraConfigId] int NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [ZoneName] nvarchar(100) NOT NULL,
    [SnapshotPath] nvarchar(500) NULL,
    [IsResolved] bit NOT NULL,
    CONSTRAINT [PK_CameraEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CameraEvents_CameraConfigs_CameraConfigId] FOREIGN KEY ([CameraConfigId]) REFERENCES [CameraConfigs] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_CameraEvents_CameraConfigId] ON [CameraEvents] ([CameraConfigId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260830033421_AddCameraIntegration', N'9.0.0');

CREATE TABLE [AppointmentRecords] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [EmployeeCodeSnapshot] nvarchar(max) NULL,
    [EmployeeNameSnapshot] nvarchar(max) NULL,
    [PreviousDepartment] nvarchar(max) NULL,
    [PreviousPosition] nvarchar(max) NULL,
    [PreviousJobTitle] nvarchar(max) NULL,
    [NewDepartment] nvarchar(max) NULL,
    [NewPosition] nvarchar(max) NULL,
    [NewJobTitle] nvarchar(max) NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [Reason] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [DecisionNumber] nvarchar(max) NULL,
    [DecisionDate] datetime2 NULL,
    [DecisionFilePath] nvarchar(max) NULL,
    [ApprovalStatus] nvarchar(max) NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [SubmittedBy] nvarchar(max) NULL,
    [SubmittedAt] datetime2 NULL,
    [ApprovedBy] nvarchar(max) NULL,
    [ApprovedAt] datetime2 NULL,
    [RejectedBy] nvarchar(max) NULL,
    [RejectedAt] datetime2 NULL,
    [RejectionReason] nvarchar(max) NULL,
    [CancelledBy] nvarchar(max) NULL,
    [CancelledAt] datetime2 NULL,
    [CancellationReason] nvarchar(max) NULL,
    [AppliedBy] nvarchar(max) NULL,
    [AppliedAt] datetime2 NULL,
    CONSTRAINT [PK_AppointmentRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AppointmentRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AppointmentHistories] (
    [Id] int NOT NULL IDENTITY,
    [AppointmentRecordId] int NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [Actor] nvarchar(max) NULL,
    [Action] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL,
    CONSTRAINT [PK_AppointmentHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AppointmentHistories_AppointmentRecords_AppointmentRecordId] FOREIGN KEY ([AppointmentRecordId]) REFERENCES [AppointmentRecords] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AppointmentHistories_AppointmentRecordId] ON [AppointmentHistories] ([AppointmentRecordId]);

CREATE INDEX [IX_AppointmentRecords_EmployeeId] ON [AppointmentRecords] ([EmployeeId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260905193043_AddAppointmentModule', N'9.0.0');

CREATE TABLE [DismissalRecords] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [EmployeeCodeSnapshot] nvarchar(max) NULL,
    [EmployeeNameSnapshot] nvarchar(max) NULL,
    [CurrentDepartmentSnapshot] nvarchar(max) NULL,
    [CurrentPositionSnapshot] nvarchar(max) NULL,
    [CurrentJobTitleSnapshot] nvarchar(max) NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [Reason] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [PostReliefOption] nvarchar(max) NULL,
    [TargetDepartment] nvarchar(max) NULL,
    [TargetPosition] nvarchar(max) NULL,
    [DecisionNumber] nvarchar(max) NULL,
    [DecisionDate] datetime2 NULL,
    [DecisionFilePath] nvarchar(max) NULL,
    [Signer] nvarchar(max) NULL,
    [ApprovalStatus] nvarchar(max) NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [SubmittedBy] nvarchar(max) NULL,
    [SubmittedAt] datetime2 NULL,
    [ApprovedBy] nvarchar(max) NULL,
    [ApprovedAt] datetime2 NULL,
    [RejectedBy] nvarchar(max) NULL,
    [RejectedAt] datetime2 NULL,
    [RejectionReason] nvarchar(max) NULL,
    [CancelledBy] nvarchar(max) NULL,
    [CancelledAt] datetime2 NULL,
    [CancellationReason] nvarchar(max) NULL,
    [AppliedBy] nvarchar(max) NULL,
    [AppliedAt] datetime2 NULL,
    CONSTRAINT [PK_DismissalRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DismissalRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [DismissalHistories] (
    [Id] int NOT NULL IDENTITY,
    [DismissalRecordId] int NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [Actor] nvarchar(max) NULL,
    [Action] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL,
    CONSTRAINT [PK_DismissalHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DismissalHistories_DismissalRecords_DismissalRecordId] FOREIGN KEY ([DismissalRecordId]) REFERENCES [DismissalRecords] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_DismissalHistories_DismissalRecordId] ON [DismissalHistories] ([DismissalRecordId]);

CREATE INDEX [IX_DismissalRecords_EmployeeId] ON [DismissalRecords] ([EmployeeId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260905194610_AddDismissalModule', N'9.0.0');

CREATE TABLE [TransferRecords] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [EmployeeCodeSnapshot] nvarchar(max) NULL,
    [EmployeeNameSnapshot] nvarchar(max) NULL,
    [CurrentDepartmentSnapshot] nvarchar(max) NULL,
    [CurrentPositionSnapshot] nvarchar(max) NULL,
    [CurrentJobTitleSnapshot] nvarchar(max) NULL,
    [TargetDepartment] nvarchar(max) NULL,
    [TargetPosition] nvarchar(max) NULL,
    [TargetJobTitle] nvarchar(max) NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [Reason] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [DecisionNumber] nvarchar(max) NULL,
    [DecisionDate] datetime2 NULL,
    [DecisionFilePath] nvarchar(max) NULL,
    [ApprovalStatus] nvarchar(max) NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [SubmittedBy] nvarchar(max) NULL,
    [SubmittedAt] datetime2 NULL,
    [ApprovedBy] nvarchar(max) NULL,
    [ApprovedAt] datetime2 NULL,
    [RejectedBy] nvarchar(max) NULL,
    [RejectedAt] datetime2 NULL,
    [RejectionReason] nvarchar(max) NULL,
    [CancelledBy] nvarchar(max) NULL,
    [CancelledAt] datetime2 NULL,
    [CancellationReason] nvarchar(max) NULL,
    [AppliedBy] nvarchar(max) NULL,
    [AppliedAt] datetime2 NULL,
    CONSTRAINT [PK_TransferRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TransferRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [TransferHistories] (
    [Id] int NOT NULL IDENTITY,
    [TransferRecordId] int NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [Actor] nvarchar(max) NULL,
    [Action] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL,
    CONSTRAINT [PK_TransferHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TransferHistories_TransferRecords_TransferRecordId] FOREIGN KEY ([TransferRecordId]) REFERENCES [TransferRecords] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_TransferHistories_TransferRecordId] ON [TransferHistories] ([TransferRecordId]);

CREATE INDEX [IX_TransferRecords_EmployeeId] ON [TransferRecords] ([EmployeeId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260906042925_AddTransferModule', N'9.0.0');

CREATE TABLE [OffboardingRecords] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [EmployeeCodeSnapshot] nvarchar(max) NULL,
    [EmployeeNameSnapshot] nvarchar(max) NULL,
    [DepartmentSnapshot] nvarchar(max) NULL,
    [PositionSnapshot] nvarchar(max) NULL,
    [ExitType] nvarchar(max) NULL,
    [NoticeDate] datetime2 NULL,
    [LastWorkingDate] datetime2 NOT NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [Reason] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [ResponsiblePersonId] int NULL,
    [ResponsiblePersonSnapshot] nvarchar(max) NULL,
    [ApprovalStatus] nvarchar(max) NOT NULL,
    [OffboardingStatus] nvarchar(max) NOT NULL,
    [HandoverStatus] nvarchar(max) NOT NULL,
    [AttendanceClosingStatus] nvarchar(max) NOT NULL,
    [LeaveClosingStatus] nvarchar(max) NOT NULL,
    [SettlementStatus] nvarchar(max) NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [SubmittedBy] nvarchar(max) NULL,
    [SubmittedAt] datetime2 NULL,
    [ApprovedBy] nvarchar(max) NULL,
    [ApprovedAt] datetime2 NULL,
    [RejectedBy] nvarchar(max) NULL,
    [RejectedAt] datetime2 NULL,
    [RejectionReason] nvarchar(max) NULL,
    [CancelledBy] nvarchar(max) NULL,
    [CancelledAt] datetime2 NULL,
    [CancellationReason] nvarchar(max) NULL,
    [CompletedBy] nvarchar(max) NULL,
    [CompletedAt] datetime2 NULL,
    CONSTRAINT [PK_OffboardingRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OffboardingRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [OffboardingChecklistTasks] (
    [Id] int NOT NULL IDENTITY,
    [OffboardingRecordId] int NOT NULL,
    [TaskName] nvarchar(max) NOT NULL,
    [IsMandatory] bit NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [CompletedAt] datetime2 NULL,
    [CompletedBy] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [Assignee] nvarchar(max) NULL,
    [Category] nvarchar(max) NULL,
    [Deadline] datetime2 NULL,
    CONSTRAINT [PK_OffboardingChecklistTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OffboardingChecklistTasks_OffboardingRecords_OffboardingRecordId] FOREIGN KEY ([OffboardingRecordId]) REFERENCES [OffboardingRecords] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [OffboardingHistories] (
    [Id] int NOT NULL IDENTITY,
    [OffboardingRecordId] int NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [Actor] nvarchar(max) NULL,
    [Action] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL,
    CONSTRAINT [PK_OffboardingHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OffboardingHistories_OffboardingRecords_OffboardingRecordId] FOREIGN KEY ([OffboardingRecordId]) REFERENCES [OffboardingRecords] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_OffboardingChecklistTasks_OffboardingRecordId] ON [OffboardingChecklistTasks] ([OffboardingRecordId]);

CREATE INDEX [IX_OffboardingHistories_OffboardingRecordId] ON [OffboardingHistories] ([OffboardingRecordId]);

CREATE INDEX [IX_OffboardingRecords_EmployeeId] ON [OffboardingRecords] ([EmployeeId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260906052820_AddOffboardingModule', N'9.0.0');

CREATE TABLE [RewardRecords] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [EmployeeCodeSnapshot] nvarchar(max) NULL,
    [EmployeeNameSnapshot] nvarchar(max) NULL,
    [DepartmentSnapshot] nvarchar(max) NULL,
    [PositionSnapshot] nvarchar(max) NULL,
    [RewardType] nvarchar(max) NULL,
    [Title] nvarchar(max) NOT NULL,
    [Achievement] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [ProposedDate] datetime2 NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [HasMonetaryReward] bit NOT NULL,
    [RewardAmount] float NOT NULL,
    [PayrollPeriod] nvarchar(max) NULL,
    [IsPayrollSynced] bit NOT NULL,
    [PayrollSyncedAt] datetime2 NULL,
    [ApprovalStatus] nvarchar(max) NOT NULL,
    [ProposedBy] nvarchar(max) NULL,
    [SubmittedBy] nvarchar(max) NULL,
    [SubmittedAt] datetime2 NULL,
    [ApprovedBy] nvarchar(max) NULL,
    [ApprovedAt] datetime2 NULL,
    [RejectedBy] nvarchar(max) NULL,
    [RejectedAt] datetime2 NULL,
    [RejectionReason] nvarchar(max) NULL,
    [CancelledBy] nvarchar(max) NULL,
    [CancelledAt] datetime2 NULL,
    [CancellationReason] nvarchar(max) NULL,
    [CreatedBy] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_RewardRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RewardRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RewardHistories] (
    [Id] int NOT NULL IDENTITY,
    [RewardRecordId] int NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [Actor] nvarchar(max) NULL,
    [Action] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL,
    CONSTRAINT [PK_RewardHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RewardHistories_RewardRecords_RewardRecordId] FOREIGN KEY ([RewardRecordId]) REFERENCES [RewardRecords] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_RewardHistories_RewardRecordId] ON [RewardHistories] ([RewardRecordId]);

CREATE INDEX [IX_RewardRecords_EmployeeId] ON [RewardRecords] ([EmployeeId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260906060320_AddRewardModule', N'9.0.0');

CREATE TABLE [IncidentRecords] (
    [Id] int NOT NULL IDENTITY,
    [IncidentCode] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [IncidentType] nvarchar(max) NOT NULL,
    [Severity] nvarchar(max) NOT NULL,
    [OccurredAt] datetime2 NOT NULL,
    [Location] nvarchar(max) NULL,
    [Description] nvarchar(max) NOT NULL,
    [ReportedBy] nvarchar(max) NULL,
    [ResponsiblePersonId] int NULL,
    [ResponsiblePersonNameSnapshot] nvarchar(max) NULL,
    [Status] nvarchar(max) NOT NULL,
    [InvestigationSummary] nvarchar(max) NULL,
    [ProcessingNotes] nvarchar(max) NULL,
    [Conclusion] nvarchar(max) NULL,
    [Resolution] nvarchar(max) NULL,
    [InvestigationStartedAt] datetime2 NULL,
    [InvestigationStartedBy] nvarchar(max) NULL,
    [ProcessingStartedAt] datetime2 NULL,
    [ProcessingStartedBy] nvarchar(max) NULL,
    [ResolvedAt] datetime2 NULL,
    [ResolvedBy] nvarchar(max) NULL,
    [ClosedAt] datetime2 NULL,
    [ClosedBy] nvarchar(max) NULL,
    [CancelledAt] datetime2 NULL,
    [CancelledBy] nvarchar(max) NULL,
    [CancellationReason] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_IncidentRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IncidentRecords_Employees_ResponsiblePersonId] FOREIGN KEY ([ResponsiblePersonId]) REFERENCES [Employees] ([Id])
);

CREATE TABLE [IncidentEvidences] (
    [Id] int NOT NULL IDENTITY,
    [IncidentRecordId] int NOT NULL,
    [FileName] nvarchar(max) NOT NULL,
    [FilePath] nvarchar(max) NOT NULL,
    [ContentType] nvarchar(max) NULL,
    [Description] nvarchar(max) NULL,
    [UploadedBy] nvarchar(max) NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_IncidentEvidences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IncidentEvidences_IncidentRecords_IncidentRecordId] FOREIGN KEY ([IncidentRecordId]) REFERENCES [IncidentRecords] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [IncidentHistories] (
    [Id] int NOT NULL IDENTITY,
    [IncidentRecordId] int NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [Actor] nvarchar(max) NULL,
    [Action] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL,
    CONSTRAINT [PK_IncidentHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IncidentHistories_IncidentRecords_IncidentRecordId] FOREIGN KEY ([IncidentRecordId]) REFERENCES [IncidentRecords] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [IncidentParticipants] (
    [Id] int NOT NULL IDENTITY,
    [IncidentRecordId] int NOT NULL,
    [EmployeeId] int NOT NULL,
    [EmployeeCodeSnapshot] nvarchar(max) NULL,
    [EmployeeNameSnapshot] nvarchar(max) NULL,
    [DepartmentSnapshot] nvarchar(max) NULL,
    [PositionSnapshot] nvarchar(max) NULL,
    [ParticipantRole] nvarchar(max) NULL,
    CONSTRAINT [PK_IncidentParticipants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IncidentParticipants_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_IncidentParticipants_IncidentRecords_IncidentRecordId] FOREIGN KEY ([IncidentRecordId]) REFERENCES [IncidentRecords] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [IncidentWitnesses] (
    [Id] int NOT NULL IDENTITY,
    [IncidentRecordId] int NOT NULL,
    [WitnessName] nvarchar(max) NOT NULL,
    [EmployeeCode] nvarchar(max) NULL,
    [Department] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_IncidentWitnesses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IncidentWitnesses_IncidentRecords_IncidentRecordId] FOREIGN KEY ([IncidentRecordId]) REFERENCES [IncidentRecords] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_IncidentEvidences_IncidentRecordId] ON [IncidentEvidences] ([IncidentRecordId]);

CREATE INDEX [IX_IncidentHistories_IncidentRecordId] ON [IncidentHistories] ([IncidentRecordId]);

CREATE INDEX [IX_IncidentParticipants_EmployeeId] ON [IncidentParticipants] ([EmployeeId]);

CREATE INDEX [IX_IncidentParticipants_IncidentRecordId] ON [IncidentParticipants] ([IncidentRecordId]);

CREATE INDEX [IX_IncidentRecords_ResponsiblePersonId] ON [IncidentRecords] ([ResponsiblePersonId]);

CREATE INDEX [IX_IncidentWitnesses_IncidentRecordId] ON [IncidentWitnesses] ([IncidentRecordId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260906061904_AddIncidentModule', N'9.0.0');

CREATE TABLE [CandidateDevelopmentPlans] (
    [Id] int NOT NULL IDENTITY,
    [SuccessionCandidateId] int NOT NULL,
    [Objective] nvarchar(max) NULL,
    [DevelopmentGap] nvarchar(max) NULL,
    [Activities] nvarchar(max) NULL,
    [ResponsiblePerson] nvarchar(max) NULL,
    [StartDate] datetime2 NULL,
    [TargetDate] datetime2 NULL,
    [Status] nvarchar(max) NULL,
    [Progress] int NOT NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_CandidateDevelopmentPlans] PRIMARY KEY ([Id])
);

CREATE TABLE [PlanningPeriods] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [Status] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PlanningPeriods] PRIMARY KEY ([Id])
);

CREATE TABLE [SuccessionCandidates] (
    [Id] int NOT NULL IDENTITY,
    [SuccessionPlanId] int NOT NULL,
    [EmployeeId] int NOT NULL,
    [EmployeeCodeSnapshot] nvarchar(max) NULL,
    [EmployeeNameSnapshot] nvarchar(max) NULL,
    [DepartmentSnapshot] nvarchar(max) NULL,
    [PositionSnapshot] nvarchar(max) NULL,
    [CompetencyScore] int NULL,
    [ExperienceScore] int NULL,
    [PerformanceScore] int NULL,
    [PotentialScore] int NULL,
    [ReadinessLevel] nvarchar(max) NULL,
    [Rank] int NULL,
    [AssessmentNotes] nvarchar(max) NULL,
    [AddedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SuccessionCandidates] PRIMARY KEY ([Id])
);

CREATE TABLE [SuccessionHistories] (
    [Id] int NOT NULL IDENTITY,
    [SuccessionPlanId] int NOT NULL,
    [Action] nvarchar(max) NULL,
    [Actor] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_SuccessionHistories] PRIMARY KEY ([Id])
);

CREATE TABLE [SuccessionPlans] (
    [Id] int NOT NULL IDENTITY,
    [PlanningPeriodId] int NOT NULL,
    [TargetPositionId] int NOT NULL,
    [Department] nvarchar(max) NULL,
    [Priority] nvarchar(max) NULL,
    [Reason] nvarchar(max) NULL,
    [OwnerName] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [Status] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SuccessionPlans] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260906063933_AddSuccessionPlanning', N'9.0.0');

CREATE TABLE [HrTaskHistories] (
    [Id] int NOT NULL IDENTITY,
    [HrTaskId] int NOT NULL,
    [Action] nvarchar(max) NULL,
    [Actor] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_HrTaskHistories] PRIMARY KEY ([Id])
);

CREATE TABLE [HrTasks] (
    [Id] int NOT NULL IDENTITY,
    [TaskCode] nvarchar(max) NULL,
    [Title] nvarchar(max) NULL,
    [Description] nvarchar(max) NULL,
    [RelatedModule] nvarchar(max) NULL,
    [RelatedRecordId] int NULL,
    [RelatedRecordCode] nvarchar(max) NULL,
    [RelatedEmployeeId] int NULL,
    [AssigneeEmployeeId] int NOT NULL,
    [AssigneeNameSnapshot] nvarchar(max) NULL,
    [CreatedBy] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [StartDate] datetime2 NULL,
    [Deadline] datetime2 NOT NULL,
    [Priority] nvarchar(max) NULL,
    [Status] nvarchar(max) NULL,
    [ProgressPercent] int NOT NULL,
    [ReminderEnabled] bit NOT NULL,
    [ReminderBeforeValue] int NULL,
    [ReminderBeforeUnit] nvarchar(max) NULL,
    [CompletedAt] datetime2 NULL,
    [CompletedBy] nvarchar(max) NULL,
    [CancelledAt] datetime2 NULL,
    [CancelledBy] nvarchar(max) NULL,
    [CancellationReason] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_HrTasks] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260906064956_AddTaskTracking', N'9.0.0');

CREATE TABLE [ProposalHistories] (
    [Id] int NOT NULL IDENTITY,
    [ProposalId] int NOT NULL,
    [Action] nvarchar(max) NULL,
    [Actor] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_ProposalHistories] PRIMARY KEY ([Id])
);

CREATE TABLE [Proposals] (
    [Id] int NOT NULL IDENTITY,
    [ProposalCode] nvarchar(max) NULL,
    [Title] nvarchar(max) NULL,
    [ProposalType] nvarchar(max) NULL,
    [CurrentSituation] nvarchar(max) NULL,
    [ProposalContent] nvarchar(max) NULL,
    [Reason] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [RelatedEmployeeId] int NULL,
    [RelatedEmployeeNameSnapshot] nvarchar(max) NULL,
    [RelatedDepartment] nvarchar(max) NULL,
    [TargetPositionId] int NULL,
    [TargetPositionSnapshot] nvarchar(max) NULL,
    [EstimatedCost] decimal(18,2) NULL,
    [ProposedEffectiveDate] datetime2 NULL,
    [Status] nvarchar(max) NULL,
    [CurrentApprovalLevel] int NOT NULL,
    [TotalApprovalLevels] int NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [SubmittedBy] nvarchar(max) NULL,
    [SubmittedAt] datetime2 NULL,
    [ApprovedBy] nvarchar(max) NULL,
    [ApprovedAt] datetime2 NULL,
    [RejectedBy] nvarchar(max) NULL,
    [RejectedAt] datetime2 NULL,
    [RejectionReason] nvarchar(max) NULL,
    [RevisionRequestedBy] nvarchar(max) NULL,
    [RevisionRequestedAt] datetime2 NULL,
    [RevisionRequestReason] nvarchar(max) NULL,
    [CancelledBy] nvarchar(max) NULL,
    [CancelledAt] datetime2 NULL,
    [CancellationReason] nvarchar(max) NULL,
    [SourceProposalId] int NULL,
    [GeneratedBusinessType] nvarchar(max) NULL,
    [GeneratedBusinessRecordId] int NULL,
    CONSTRAINT [PK_Proposals] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260906070739_AddProposalModule', N'9.0.0');

CREATE TABLE [AttendancePeriods] (
    [Id] int NOT NULL IDENTITY,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [LockedAt] datetime2 NULL,
    [LockedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_AttendancePeriods] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260906075647_AddAttendancePeriod', N'9.0.0');

CREATE TABLE [Holidays] (
    [Id] int NOT NULL IDENTITY,
    [HolidayName] nvarchar(200) NOT NULL,
    [FromDate] datetime2 NOT NULL,
    [ToDate] datetime2 NOT NULL,
    [ScopeType] nvarchar(50) NOT NULL,
    [ScopeValue] nvarchar(100) NOT NULL,
    [IsPaidHoliday] bit NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedBy] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Holidays] PRIMARY KEY ([Id])
);

CREATE TABLE [WorkShifts] (
    [Id] int NOT NULL IDENTITY,
    [ShiftCode] nvarchar(50) NOT NULL,
    [ShiftName] nvarchar(200) NOT NULL,
    [ShiftType] nvarchar(100) NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [BreakDurationMinutes] int NOT NULL,
    [LateGraceMinutes] int NOT NULL,
    [EarlyLeaveGraceMinutes] int NOT NULL,
    [IsOvernight] bit NOT NULL,
    [IsOvertimeEnabled] bit NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [Notes] nvarchar(max) NULL,
    [CreatedBy] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_WorkShifts] PRIMARY KEY ([Id])
);

CREATE TABLE [WorkShiftAssignments] (
    [Id] int NOT NULL IDENTITY,
    [WorkShiftId] int NOT NULL,
    [TargetType] nvarchar(50) NOT NULL,
    [TargetValue] nvarchar(100) NOT NULL,
    [EffectiveFrom] datetime2 NULL,
    [EffectiveTo] datetime2 NULL,
    CONSTRAINT [PK_WorkShiftAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkShiftAssignments_WorkShifts_WorkShiftId] FOREIGN KEY ([WorkShiftId]) REFERENCES [WorkShifts] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_WorkShiftAssignments_WorkShiftId] ON [WorkShiftAssignments] ([WorkShiftId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260906135834_AddAttendanceConfigTables', N'9.0.0');

CREATE TABLE [AttendanceRuleVersions] (
    [Id] int NOT NULL IDENTITY,
    [VersionCode] nvarchar(50) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [EffectiveFrom] datetime2 NOT NULL,
    [EffectiveTo] datetime2 NULL,
    [ScopeType] nvarchar(50) NOT NULL,
    [ScopeId] int NULL,
    [LateGraceMinutes] int NOT NULL,
    [LateFullCreditThresholdMinutes] int NOT NULL,
    [LateHandlingMode] nvarchar(50) NOT NULL,
    [LateMaxDeductionMinutes] int NOT NULL,
    [EarlyLeaveGraceMinutes] int NOT NULL,
    [EarlyLeaveFullCreditThresholdMinutes] int NOT NULL,
    [EarlyLeaveHandlingMode] nvarchar(50) NOT NULL,
    [EarlyLeaveMaxDeductionMinutes] int NOT NULL,
    [CheckInRoundingMode] nvarchar(50) NOT NULL,
    [CheckInRoundingStepMinutes] int NOT NULL,
    [CheckOutRoundingMode] nvarchar(50) NOT NULL,
    [CheckOutRoundingStepMinutes] int NOT NULL,
    [MissingCheckInMode] nvarchar(50) NOT NULL,
    [MissingCheckInAssumedTime] time NULL,
    [MissingCheckOutMode] nvarchar(50) NOT NULL,
    [MissingCheckOutAssumedTime] time NULL,
    [DailyOtThreshold] float NOT NULL,
    [WeeklyOtThreshold] float NOT NULL,
    [AutoCalculateOt] bit NOT NULL,
    [AllowAfterShiftOt] bit NOT NULL,
    [CreatedBy] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedBy] nvarchar(100) NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_AttendanceRuleVersions] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260906152329_AddAttendanceRules', N'9.0.0');

ALTER TABLE [Contracts] ADD [PdfFilePath] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260907073012_AddPdfFilePathToContract', N'9.0.0');

CREATE TABLE [BenefitEnrollments] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeName] nvarchar(max) NOT NULL,
    [BenefitProgramId] int NOT NULL,
    [RegistrationDate] datetime2 NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Notes] nvarchar(max) NULL,
    [RejectionReason] nvarchar(max) NULL,
    CONSTRAINT [PK_BenefitEnrollments] PRIMARY KEY ([Id])
);

CREATE TABLE [BenefitEntitlements] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeName] nvarchar(max) NOT NULL,
    [BenefitProgramId] int NOT NULL,
    [EffectiveFrom] datetime2 NULL,
    [EffectiveTo] datetime2 NULL,
    [Status] nvarchar(max) NOT NULL,
    [Amount] decimal(18,2) NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_BenefitEntitlements] PRIMARY KEY ([Id])
);

CREATE TABLE [BenefitPrograms] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [BenefitType] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [RegistrationStart] datetime2 NULL,
    [RegistrationEnd] datetime2 NULL,
    [RequiresApproval] bit NOT NULL,
    [IsAutoApplied] bit NOT NULL,
    [Capacity] int NULL,
    [RequiredContractType] nvarchar(max) NULL,
    CONSTRAINT [PK_BenefitPrograms] PRIMARY KEY ([Id])
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BenefitType', N'Capacity', N'Description', N'IsAutoApplied', N'Name', N'RegistrationEnd', N'RegistrationStart', N'RequiredContractType', N'RequiresApproval', N'Status') AND [object_id] = OBJECT_ID(N'[BenefitPrograms]'))
    SET IDENTITY_INSERT [BenefitPrograms] ON;
INSERT INTO [BenefitPrograms] ([Id], [BenefitType], [Capacity], [Description], [IsAutoApplied], [Name], [RegistrationEnd], [RegistrationStart], [RequiredContractType], [RequiresApproval], [Status])
VALUES (1, N'Insurance', NULL, N'Bảo hiểm theo quy định của nhà nước.', CAST(1 AS bit), N'Bảo hiểm Xã hội (BHXH)', NULL, NULL, NULL, CAST(0 AS bit), N'Open'),
(2, N'Insurance', NULL, N'Khám chữa bệnh BHYT.', CAST(1 AS bit), N'Bảo hiểm Y tế (BHYT)', NULL, NULL, NULL, CAST(0 AS bit), N'Open'),
(3, N'Health', 100, N'Khám tổng quát tại bệnh viện Thu Cúc.', CAST(0 AS bit), N'Khám sức khỏe định kỳ 2026', '2026-09-30T00:00:00.0000000', '2026-09-01T00:00:00.0000000', NULL, CAST(1 AS bit), N'Open'),
(4, N'Allowance', NULL, N'Phụ cấp 35k/ngày đi làm.', CAST(1 AS bit), N'Phụ cấp ăn trưa', NULL, NULL, NULL, CAST(0 AS bit), N'Open');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BenefitType', N'Capacity', N'Description', N'IsAutoApplied', N'Name', N'RegistrationEnd', N'RegistrationStart', N'RequiredContractType', N'RequiresApproval', N'Status') AND [object_id] = OBJECT_ID(N'[BenefitPrograms]'))
    SET IDENTITY_INSERT [BenefitPrograms] OFF;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260907074604_AddBenefitModuleClean', N'9.0.0');

CREATE TABLE [TrainingCourses] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(255) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Category] nvarchar(100) NOT NULL,
    [Mode] nvarchar(50) NOT NULL,
    [Instructor] nvarchar(255) NOT NULL,
    [RegistrationStart] datetime2 NULL,
    [RegistrationEnd] datetime2 NULL,
    [StartDate] datetime2 NULL,
    [EndDate] datetime2 NULL,
    [DurationHours] int NULL,
    [Capacity] int NULL,
    [IsMandatory] bit NOT NULL,
    [RequiresApproval] bit NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_TrainingCourses] PRIMARY KEY ([Id])
);

CREATE TABLE [CourseEnrollments] (
    [Id] int NOT NULL IDENTITY,
    [CourseId] int NOT NULL,
    [EmployeeName] nvarchar(100) NOT NULL,
    [RegistrationDate] datetime2 NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [ProgressPercent] int NOT NULL,
    [Result] nvarchar(50) NOT NULL,
    [CompletionDate] datetime2 NULL,
    [Notes] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CourseEnrollments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CourseEnrollments_TrainingCourses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [TrainingCourses] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [TrainingMaterials] (
    [Id] int NOT NULL IDENTITY,
    [CourseId] int NOT NULL,
    [Title] nvarchar(255) NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [FileUrl] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_TrainingMaterials] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TrainingMaterials_TrainingCourses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [TrainingCourses] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [TrainingCertificates] (
    [Id] int NOT NULL IDENTITY,
    [EnrollmentId] int NOT NULL,
    [EmployeeName] nvarchar(100) NOT NULL,
    [IssuedDate] datetime2 NOT NULL,
    [CertificateNumber] nvarchar(100) NOT NULL,
    [CertificateUrl] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_TrainingCertificates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TrainingCertificates_CourseEnrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [CourseEnrollments] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_BenefitEntitlements_BenefitProgramId] ON [BenefitEntitlements] ([BenefitProgramId]);

CREATE INDEX [IX_BenefitEnrollments_BenefitProgramId] ON [BenefitEnrollments] ([BenefitProgramId]);

CREATE INDEX [IX_CourseEnrollments_CourseId] ON [CourseEnrollments] ([CourseId]);

CREATE UNIQUE INDEX [IX_TrainingCertificates_EnrollmentId] ON [TrainingCertificates] ([EnrollmentId]);

CREATE INDEX [IX_TrainingMaterials_CourseId] ON [TrainingMaterials] ([CourseId]);

ALTER TABLE [BenefitEnrollments] ADD CONSTRAINT [FK_BenefitEnrollments_BenefitPrograms_BenefitProgramId] FOREIGN KEY ([BenefitProgramId]) REFERENCES [BenefitPrograms] ([Id]) ON DELETE CASCADE;

ALTER TABLE [BenefitEntitlements] ADD CONSTRAINT [FK_BenefitEntitlements_BenefitPrograms_BenefitProgramId] FOREIGN KEY ([BenefitProgramId]) REFERENCES [BenefitPrograms] ([Id]) ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260907103044_AddTrainingModule', N'9.0.0');

ALTER TABLE [RewardRecords] ADD [DecisionDocumentUrl] nvarchar(max) NULL;

ALTER TABLE [RewardRecords] ADD [DecisionNumber] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260907110041_AddRewardDecisionFields', N'9.0.0');

CREATE TABLE [EmailTemplates] (
    [Id] int NOT NULL IDENTITY,
    [TemplateName] nvarchar(100) NOT NULL,
    [Subject] nvarchar(255) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [EventCode] nvarchar(50) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_EmailTemplates] PRIMARY KEY ([Id])
);

CREATE TABLE [NotificationLogs] (
    [Id] int NOT NULL IDENTITY,
    [RuleId] int NOT NULL,
    [EventCode] nvarchar(50) NOT NULL,
    [RecipientEmail] nvarchar(100) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [FailureReason] nvarchar(max) NOT NULL,
    [SentAt] datetime2 NOT NULL,
    CONSTRAINT [PK_NotificationLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [NotificationRecipientGroups] (
    [Id] int NOT NULL IDENTITY,
    [GroupName] nvarchar(100) NOT NULL,
    [Description] nvarchar(255) NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [TargetValues] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_NotificationRecipientGroups] PRIMARY KEY ([Id])
);

CREATE TABLE [NotificationRules] (
    [Id] int NOT NULL IDENTITY,
    [RuleName] nvarchar(100) NOT NULL,
    [Description] nvarchar(255) NOT NULL,
    [EventCode] nvarchar(50) NOT NULL,
    [Module] nvarchar(50) NOT NULL,
    [RecipientGroupId] int NULL,
    [EmailTemplateId] int NULL,
    [Channel] nvarchar(50) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_NotificationRules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_NotificationRules_EmailTemplates_EmailTemplateId] FOREIGN KEY ([EmailTemplateId]) REFERENCES [EmailTemplates] ([Id]),
    CONSTRAINT [FK_NotificationRules_NotificationRecipientGroups_RecipientGroupId] FOREIGN KEY ([RecipientGroupId]) REFERENCES [NotificationRecipientGroups] ([Id])
);

CREATE INDEX [IX_NotificationRules_EmailTemplateId] ON [NotificationRules] ([EmailTemplateId]);

CREATE INDEX [IX_NotificationRules_RecipientGroupId] ON [NotificationRules] ([RecipientGroupId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260908045526_AddNotificationEngine', N'9.0.0');

CREATE TABLE [AuditLogs] (
    [Id] bigint NOT NULL IDENTITY,
    [Timestamp] datetime2 NOT NULL,
    [UserId] nvarchar(100) NOT NULL,
    [Username] nvarchar(255) NOT NULL,
    [Role] nvarchar(100) NOT NULL,
    [Module] nvarchar(100) NOT NULL,
    [Action] nvarchar(100) NOT NULL,
    [Resource] nvarchar(255) NOT NULL,
    [ResourceId] nvarchar(100) NOT NULL,
    [EventType] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [IPAddress] nvarchar(100) NOT NULL,
    [UserAgent] nvarchar(max) NOT NULL,
    [OldValues] nvarchar(max) NOT NULL,
    [NewValues] nvarchar(max) NOT NULL,
    [Changes] nvarchar(max) NOT NULL,
    [FailureReason] nvarchar(max) NOT NULL,
    [RequestId] nvarchar(100) NOT NULL,
    [CorrelationId] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260908080932_AddAuditLog', N'9.0.0');

CREATE TABLE [BackupConfigs] (
    [Id] int NOT NULL IDENTITY,
    [IsEnabled] bit NOT NULL,
    [Frequency] nvarchar(50) NOT NULL,
    [ExecuteTime] time NOT NULL,
    [RetentionDays] int NOT NULL,
    [BackupType] nvarchar(50) NOT NULL,
    [LastRunTime] datetime2 NULL,
    CONSTRAINT [PK_BackupConfigs] PRIMARY KEY ([Id])
);

CREATE TABLE [BackupRecords] (
    [Id] bigint NOT NULL IDENTITY,
    [FileName] nvarchar(255) NOT NULL,
    [FilePath] nvarchar(1000) NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(100) NOT NULL,
    [CompletedAt] datetime2 NULL,
    [FailureReason] nvarchar(max) NOT NULL,
    [Checksum] nvarchar(64) NOT NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_BackupRecords] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260908083302_AddBackupModels', N'9.0.0');

CREATE TABLE [SystemConfigs] (
    [Key] nvarchar(100) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_SystemConfigs] PRIMARY KEY ([Key])
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'Category', N'Description', N'UpdatedAt', N'UpdatedBy', N'Value') AND [object_id] = OBJECT_ID(N'[SystemConfigs]'))
    SET IDENTITY_INSERT [SystemConfigs] ON;
INSERT INTO [SystemConfigs] ([Key], [Category], [Description], [UpdatedAt], [UpdatedBy], [Value])
VALUES (N'Company.Address', N'Company', N'Địa chỉ trụ sở', '2026-09-08T15:52:10.6591505+07:00', N'System', N'Tầng 12, Tòa nhà Tech, Hà Nội'),
(N'Company.Email', N'Company', N'Email liên hệ', '2026-09-08T15:52:10.6591501+07:00', N'System', N'contact@hrms.vn'),
(N'Company.Name', N'Company', N'Tên công ty', '2026-09-08T15:52:10.6586412+07:00', N'System', N'Công ty CP Công nghệ HRMS'),
(N'Company.Phone', N'Company', N'Hotline', '2026-09-08T15:52:10.6591503+07:00', N'System', N'1900 1000'),
(N'Company.Representative', N'Company', N'Người đại diện', '2026-09-08T15:52:10.6591506+07:00', N'System', N'Nguyễn Văn A'),
(N'Company.ShortName', N'Company', N'Tên viết tắt', '2026-09-08T15:52:10.6591489+07:00', N'System', N'HRMS Corp'),
(N'Company.TaxCode', N'Company', N'Mã số thuế', '2026-09-08T15:52:10.6591500+07:00', N'System', N'0101234567'),
(N'Company.Website', N'Company', N'Website', '2026-09-08T15:52:10.6591504+07:00', N'System', N'https://hrms.vn'),
(N'Loc.Currency', N'Localization', N'Tiền tệ', '2026-09-08T15:52:10.6591510+07:00', N'System', N'VND'),
(N'Loc.Language', N'Localization', N'Ngôn ngữ mặc định', '2026-09-08T15:52:10.6591507+07:00', N'System', N'vi-VN'),
(N'Loc.Timezone', N'Localization', N'Múi giờ', '2026-09-08T15:52:10.6591508+07:00', N'System', N'SE Asia Standard Time'),
(N'Storage.AllowedTypes', N'Storage', N'Định dạng cho phép', '2026-09-08T15:52:10.6591512+07:00', N'System', N'.jpg,.jpeg,.png,.pdf,.doc,.docx,.xls,.xlsx'),
(N'Storage.MaxFileSizeMB', N'Storage', N'Dung lượng upload tối đa (MB)', '2026-09-08T15:52:10.6591511+07:00', N'System', N'10'),
(N'Sys.MaintenanceMode', N'System', N'Chế độ bảo trì', '2026-09-08T15:52:10.6591514+07:00', N'System', N'false');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'Category', N'Description', N'UpdatedAt', N'UpdatedBy', N'Value') AND [object_id] = OBJECT_ID(N'[SystemConfigs]'))
    SET IDENTITY_INSERT [SystemConfigs] OFF;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260908085211_AddSystemConfigModels', N'9.0.0');

UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183914+07:00'
WHERE [Key] = N'Company.Address';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183911+07:00'
WHERE [Key] = N'Company.Email';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1179132+07:00'
WHERE [Key] = N'Company.Name';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183912+07:00'
WHERE [Key] = N'Company.Phone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183916+07:00'
WHERE [Key] = N'Company.Representative';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183902+07:00'
WHERE [Key] = N'Company.ShortName';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183909+07:00'
WHERE [Key] = N'Company.TaxCode';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183913+07:00'
WHERE [Key] = N'Company.Website';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183919+07:00'
WHERE [Key] = N'Loc.Currency';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183917+07:00'
WHERE [Key] = N'Loc.Language';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183918+07:00'
WHERE [Key] = N'Loc.Timezone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183921+07:00'
WHERE [Key] = N'Storage.AllowedTypes';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183920+07:00'
WHERE [Key] = N'Storage.MaxFileSizeMB';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T15:53:08.1183923+07:00'
WHERE [Key] = N'Sys.MaintenanceMode';
SELECT @@ROWCOUNT;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260908085308_AddSystemConfigModels_Fix', N'9.0.0');

CREATE TABLE [MonitoringAlerts] (
    [Id] int NOT NULL IDENTITY,
    [Severity] nvarchar(50) NOT NULL,
    [AlertName] nvarchar(200) NOT NULL,
    [MetricName] nvarchar(100) NOT NULL,
    [CurrentValue] nvarchar(50) NOT NULL,
    [Threshold] nvarchar(50) NOT NULL,
    [StartedAt] datetime2 NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [AcknowledgedAt] datetime2 NULL,
    [AcknowledgedBy] nvarchar(100) NOT NULL,
    [ResolvedAt] datetime2 NULL,
    CONSTRAINT [PK_MonitoringAlerts] PRIMARY KEY ([Id])
);

CREATE TABLE [SystemErrorLogs] (
    [Id] int NOT NULL IDENTITY,
    [Timestamp] datetime2 NOT NULL,
    [Level] nvarchar(20) NOT NULL,
    [Service] nvarchar(100) NOT NULL,
    [ExceptionType] nvarchar(200) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [StackTrace] nvarchar(max) NOT NULL,
    [RequestId] nvarchar(100) NOT NULL,
    [CorrelationId] nvarchar(100) NOT NULL,
    [User] nvarchar(100) NOT NULL,
    [IP] nvarchar(50) NOT NULL,
    [UserAgent] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_SystemErrorLogs] PRIMARY KEY ([Id])
);

UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482737+07:00'
WHERE [Key] = N'Company.Address';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482733+07:00'
WHERE [Key] = N'Company.Email';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6477128+07:00'
WHERE [Key] = N'Company.Name';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482734+07:00'
WHERE [Key] = N'Company.Phone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482738+07:00'
WHERE [Key] = N'Company.Representative';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482721+07:00'
WHERE [Key] = N'Company.ShortName';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482731+07:00'
WHERE [Key] = N'Company.TaxCode';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482736+07:00'
WHERE [Key] = N'Company.Website';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482742+07:00'
WHERE [Key] = N'Loc.Currency';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482739+07:00'
WHERE [Key] = N'Loc.Language';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482741+07:00'
WHERE [Key] = N'Loc.Timezone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482744+07:00'
WHERE [Key] = N'Storage.AllowedTypes';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482743+07:00'
WHERE [Key] = N'Storage.MaxFileSizeMB';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-08T16:04:11.6482746+07:00'
WHERE [Key] = N'Sys.MaintenanceMode';
SELECT @@ROWCOUNT;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260908090412_AddMonitoringModels', N'9.0.0');

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'Password');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Users] ALTER COLUMN [Password] nvarchar(max) NULL;

ALTER TABLE [Users] ADD [PasswordHash] nvarchar(max) NULL;

UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230299+07:00'
WHERE [Key] = N'Company.Address';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230294+07:00'
WHERE [Key] = N'Company.Email';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5223742+07:00'
WHERE [Key] = N'Company.Name';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230295+07:00'
WHERE [Key] = N'Company.Phone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230300+07:00'
WHERE [Key] = N'Company.Representative';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230284+07:00'
WHERE [Key] = N'Company.ShortName';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230292+07:00'
WHERE [Key] = N'Company.TaxCode';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230297+07:00'
WHERE [Key] = N'Company.Website';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230303+07:00'
WHERE [Key] = N'Loc.Currency';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230301+07:00'
WHERE [Key] = N'Loc.Language';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230302+07:00'
WHERE [Key] = N'Loc.Timezone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230308+07:00'
WHERE [Key] = N'Storage.AllowedTypes';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230307+07:00'
WHERE [Key] = N'Storage.MaxFileSizeMB';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T11:04:03.5230309+07:00'
WHERE [Key] = N'Sys.MaintenanceMode';
SELECT @@ROWCOUNT;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260909040406_AddPasswordHashToUser', N'9.0.0');

ALTER TABLE [NotificationLogs] ADD [IdempotencyKey] nvarchar(100) NOT NULL DEFAULT N'';

ALTER TABLE [NotificationLogs] ADD [TargetUserId] nvarchar(100) NOT NULL DEFAULT N'';

UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245219+07:00'
WHERE [Key] = N'Company.Address';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245212+07:00'
WHERE [Key] = N'Company.Email';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4220227+07:00'
WHERE [Key] = N'Company.Name';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245214+07:00'
WHERE [Key] = N'Company.Phone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245222+07:00'
WHERE [Key] = N'Company.Representative';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245180+07:00'
WHERE [Key] = N'Company.ShortName';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245209+07:00'
WHERE [Key] = N'Company.TaxCode';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245217+07:00'
WHERE [Key] = N'Company.Website';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245228+07:00'
WHERE [Key] = N'Loc.Currency';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245224+07:00'
WHERE [Key] = N'Loc.Language';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245226+07:00'
WHERE [Key] = N'Loc.Timezone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245232+07:00'
WHERE [Key] = N'Storage.AllowedTypes';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245230+07:00'
WHERE [Key] = N'Storage.MaxFileSizeMB';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T16:35:12.4245235+07:00'
WHERE [Key] = N'Sys.MaintenanceMode';
SELECT @@ROWCOUNT;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260909093515_UpdateNotificationLogP07', N'9.0.0');

ALTER TABLE [AppointmentRecords] DROP CONSTRAINT [FK_AppointmentRecords_Employees_EmployeeId];

ALTER TABLE [DismissalRecords] DROP CONSTRAINT [FK_DismissalRecords_Employees_EmployeeId];

ALTER TABLE [IncidentParticipants] DROP CONSTRAINT [FK_IncidentParticipants_Employees_EmployeeId];

ALTER TABLE [IncidentRecords] DROP CONSTRAINT [FK_IncidentRecords_Employees_ResponsiblePersonId];

ALTER TABLE [OffboardingRecords] DROP CONSTRAINT [FK_OffboardingRecords_Employees_EmployeeId];


                IF EXISTS (SELECT 1 FROM sys.foreign_keys
                           WHERE name = N'FK_OnboardingRecords_Employees_EmployeeId'
                             AND parent_object_id = OBJECT_ID(N'[OnboardingRecords]'))
                    ALTER TABLE [OnboardingRecords]
                    DROP CONSTRAINT [FK_OnboardingRecords_Employees_EmployeeId];

ALTER TABLE [RewardRecords] DROP CONSTRAINT [FK_RewardRecords_Employees_EmployeeId];

ALTER TABLE [TransferRecords] DROP CONSTRAINT [FK_TransferRecords_Employees_EmployeeId];

EXEC sp_rename N'[Salaries].[EmployeeName]', N'EmployeeNameSnapshot', 'COLUMN';

EXEC sp_rename N'[Leaves].[EmployeeName]', N'EmployeeNameSnapshot', 'COLUMN';

EXEC sp_rename N'[Contracts].[EmployeeName]', N'EmployeeNameSnapshot', 'COLUMN';

EXEC sp_rename N'[Attendances].[EmployeeName]', N'EmployeeNameSnapshot', 'COLUMN';

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'Username');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [Users] ALTER COLUMN [Username] nvarchar(450) NOT NULL;

ALTER TABLE [Users] ADD [EmployeeId] int NULL;

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Salaries]') AND [c].[name] = N'EmployeeId');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Salaries] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [Salaries] ALTER COLUMN [EmployeeId] int NULL;

ALTER TABLE [Leaves] ADD [EmployeeId] int NULL;

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Employees]') AND [c].[name] = N'EmployeeCode');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Employees] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [Employees] ALTER COLUMN [EmployeeCode] nvarchar(450) NULL;

ALTER TABLE [Employees] ADD [Status] nvarchar(max) NULL;

ALTER TABLE [Contracts] ADD [EmployeeId] int NULL;

ALTER TABLE [Attendances] ADD [EmployeeId] int NULL;

UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291372+07:00'
WHERE [Key] = N'Company.Address';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291366+07:00'
WHERE [Key] = N'Company.Email';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5276501+07:00'
WHERE [Key] = N'Company.Name';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291368+07:00'
WHERE [Key] = N'Company.Phone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291374+07:00'
WHERE [Key] = N'Company.Representative';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291350+07:00'
WHERE [Key] = N'Company.ShortName';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291364+07:00'
WHERE [Key] = N'Company.TaxCode';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291370+07:00'
WHERE [Key] = N'Company.Website';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291380+07:00'
WHERE [Key] = N'Loc.Currency';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291376+07:00'
WHERE [Key] = N'Loc.Language';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291378+07:00'
WHERE [Key] = N'Loc.Timezone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291383+07:00'
WHERE [Key] = N'Storage.AllowedTypes';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291381+07:00'
WHERE [Key] = N'Storage.MaxFileSizeMB';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T17:12:58.5291385+07:00'
WHERE [Key] = N'Sys.MaintenanceMode';
SELECT @@ROWCOUNT;


CREATE INDEX [IX_Users_EmployeeId] ON [Users] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);

CREATE INDEX [IX_Salaries_EmployeeId] ON [Salaries] ([EmployeeId]);

CREATE INDEX [IX_Leaves_EmployeeId] ON [Leaves] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_Employees_EmployeeCode] ON [Employees] ([EmployeeCode]) WHERE [EmployeeCode] IS NOT NULL;

CREATE INDEX [IX_Contracts_EmployeeId] ON [Contracts] ([EmployeeId]);

CREATE INDEX [IX_Attendances_EmployeeId] ON [Attendances] ([EmployeeId]);

ALTER TABLE [AppointmentRecords] ADD CONSTRAINT [FK_AppointmentRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Attendances] ADD CONSTRAINT [FK_Attendances_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Contracts] ADD CONSTRAINT [FK_Contracts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [DismissalRecords] ADD CONSTRAINT [FK_DismissalRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [IncidentParticipants] ADD CONSTRAINT [FK_IncidentParticipants_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [IncidentRecords] ADD CONSTRAINT [FK_IncidentRecords_Employees_ResponsiblePersonId] FOREIGN KEY ([ResponsiblePersonId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Leaves] ADD CONSTRAINT [FK_Leaves_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [OffboardingRecords] ADD CONSTRAINT [FK_OffboardingRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [OnboardingRecords] ADD CONSTRAINT [FK_OnboardingRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [RewardRecords] ADD CONSTRAINT [FK_RewardRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Salaries] ADD CONSTRAINT [FK_Salaries_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [TransferRecords] ADD CONSTRAINT [FK_TransferRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260909101301_Combine_P01_P09_Foundation', N'9.0.0');

ALTER TABLE [TrainingCertificates] ADD [EmployeeNameSnapshot] nvarchar(100) NOT NULL DEFAULT N'';

ALTER TABLE [CourseEnrollments] ADD [ProgressStatus] nvarchar(50) NOT NULL DEFAULT N'';

ALTER TABLE [CourseEnrollments] ADD [EmployeeNameSnapshot] nvarchar(100) NOT NULL DEFAULT N'';

ALTER TABLE [BenefitEntitlements] ADD [EmployeeNameSnapshot] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [BenefitEnrollments] ADD [EmployeeNameSnapshot] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [BenefitEnrollments] ADD [ApprovalStatus] nvarchar(max) NOT NULL DEFAULT N'DRAFT';


                UPDATE [TrainingCertificates] SET [EmployeeNameSnapshot] = LEFT([EmployeeName], 100);
                UPDATE [CourseEnrollments] SET [EmployeeNameSnapshot] = LEFT([EmployeeName], 100), [ProgressStatus] = LEFT([Status], 50);
                UPDATE [BenefitEntitlements] SET [EmployeeNameSnapshot] = [EmployeeName];
                UPDATE [BenefitEnrollments] SET [EmployeeNameSnapshot] = [EmployeeName], [ApprovalStatus] = [Status];

ALTER TABLE [TrainingCertificates] ADD [CanonicalFileId] nvarchar(100) NOT NULL DEFAULT N'';

ALTER TABLE [TrainingCertificates] ADD [EmployeeId] int NOT NULL DEFAULT 0;

ALTER TABLE [Leaves] ADD [LeaveType] nvarchar(max) NULL;

ALTER TABLE [Leaves] ADD [RejectionReason] nvarchar(max) NULL;

ALTER TABLE [Leaves] ADD [ReviewedAt] datetime2 NULL;

ALTER TABLE [Leaves] ADD [ReviewedBy] nvarchar(max) NULL;

ALTER TABLE [CourseEnrollments] ADD [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT N'';

ALTER TABLE [CourseEnrollments] ADD [EmployeeId] int NOT NULL DEFAULT 0;

ALTER TABLE [Contracts] ADD [RejectionReason] nvarchar(max) NULL;

ALTER TABLE [Contracts] ADD [ReviewedAt] datetime2 NULL;

ALTER TABLE [Contracts] ADD [ReviewedBy] nvarchar(max) NULL;

ALTER TABLE [BenefitEntitlements] ADD [EmployeeId] int NOT NULL DEFAULT 0;

ALTER TABLE [BenefitEnrollments] ADD [EmployeeId] int NOT NULL DEFAULT 0;


                UPDATE u SET u.EmployeeId = e.Id
                FROM [Users] u INNER JOIN [Employees] e ON e.[Name] = u.[FullName]
                WHERE u.EmployeeId IS NULL;

                UPDATE a SET a.EmployeeId = e.Id
                FROM [Attendances] a INNER JOIN [Employees] e ON e.[Name] = a.[EmployeeNameSnapshot]
                WHERE a.EmployeeId IS NULL;

                UPDATE c SET c.EmployeeId = e.Id
                FROM [Contracts] c INNER JOIN [Employees] e ON e.[Name] = c.[EmployeeNameSnapshot]
                WHERE c.EmployeeId IS NULL;

                UPDATE l SET l.EmployeeId = e.Id
                FROM [Leaves] l INNER JOIN [Employees] e ON e.[Name] = l.[EmployeeNameSnapshot]
                WHERE l.EmployeeId IS NULL;

                UPDATE s SET s.EmployeeId = e.Id
                FROM [Salaries] s INNER JOIN [Employees] e ON e.[Name] = s.[EmployeeNameSnapshot]
                WHERE s.EmployeeId IS NULL;

                UPDATE b SET b.EmployeeId = e.Id
                FROM [BenefitEnrollments] b INNER JOIN [Employees] e ON e.[Name] = b.[EmployeeNameSnapshot]
                WHERE b.EmployeeId = 0;

                UPDATE b SET b.EmployeeId = e.Id
                FROM [BenefitEntitlements] b INNER JOIN [Employees] e ON e.[Name] = b.[EmployeeNameSnapshot]
                WHERE b.EmployeeId = 0;

                UPDATE c SET c.EmployeeId = e.Id
                FROM [CourseEnrollments] c INNER JOIN [Employees] e ON e.[Name] = c.[EmployeeNameSnapshot]
                WHERE c.EmployeeId = 0;

                UPDATE t SET t.EmployeeId = e.Id
                FROM [TrainingCertificates] t INNER JOIN [Employees] e ON e.[Name] = t.[EmployeeNameSnapshot]
                WHERE t.EmployeeId = 0;

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AttendanceAdjustmentRequests]') AND [c].[name] = N'Status');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [AttendanceAdjustmentRequests] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [AttendanceAdjustmentRequests] ALTER COLUMN [Status] nvarchar(max) NULL;

CREATE TABLE [ManagerAssignments] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [OrganizationalUnit] nvarchar(max) NOT NULL,
    [EffectiveFrom] datetime2 NOT NULL,
    [EffectiveTo] datetime2 NULL,
    [AssignedBy] nvarchar(max) NULL,
    [AssignedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ManagerAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ManagerAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244784+07:00'
WHERE [Key] = N'Company.Address';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244778+07:00'
WHERE [Key] = N'Company.Email';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5233260+07:00'
WHERE [Key] = N'Company.Name';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244780+07:00'
WHERE [Key] = N'Company.Phone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244786+07:00'
WHERE [Key] = N'Company.Representative';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244761+07:00'
WHERE [Key] = N'Company.ShortName';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244775+07:00'
WHERE [Key] = N'Company.TaxCode';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244782+07:00'
WHERE [Key] = N'Company.Website';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244865+07:00'
WHERE [Key] = N'Loc.Currency';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244788+07:00'
WHERE [Key] = N'Loc.Language';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244863+07:00'
WHERE [Key] = N'Loc.Timezone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244869+07:00'
WHERE [Key] = N'Storage.AllowedTypes';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244867+07:00'
WHERE [Key] = N'Storage.MaxFileSizeMB';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T20:48:49.5244871+07:00'
WHERE [Key] = N'Sys.MaintenanceMode';
SELECT @@ROWCOUNT;


CREATE INDEX [IX_TrainingCertificates_EmployeeId] ON [TrainingCertificates] ([EmployeeId]);

CREATE INDEX [IX_SuccessionPlans_PlanningPeriodId] ON [SuccessionPlans] ([PlanningPeriodId]);

CREATE INDEX [IX_SuccessionPlans_TargetPositionId] ON [SuccessionPlans] ([TargetPositionId]);

CREATE INDEX [IX_SuccessionHistories_SuccessionPlanId] ON [SuccessionHistories] ([SuccessionPlanId]);

CREATE INDEX [IX_SuccessionCandidates_EmployeeId] ON [SuccessionCandidates] ([EmployeeId]);

CREATE INDEX [IX_SuccessionCandidates_SuccessionPlanId] ON [SuccessionCandidates] ([SuccessionPlanId]);

CREATE INDEX [IX_CourseEnrollments_EmployeeId] ON [CourseEnrollments] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_CandidateDevelopmentPlans_SuccessionCandidateId] ON [CandidateDevelopmentPlans] ([SuccessionCandidateId]);

CREATE INDEX [IX_BenefitEntitlements_EmployeeId] ON [BenefitEntitlements] ([EmployeeId]);

CREATE INDEX [IX_BenefitEnrollments_EmployeeId] ON [BenefitEnrollments] ([EmployeeId]);

CREATE INDEX [IX_ManagerAssignments_UserId] ON [ManagerAssignments] ([UserId]);

ALTER TABLE [BenefitEnrollments] ADD CONSTRAINT [FK_BenefitEnrollments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [BenefitEntitlements] ADD CONSTRAINT [FK_BenefitEntitlements_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [CandidateDevelopmentPlans] ADD CONSTRAINT [FK_CandidateDevelopmentPlans_SuccessionCandidates_SuccessionCandidateId] FOREIGN KEY ([SuccessionCandidateId]) REFERENCES [SuccessionCandidates] ([Id]) ON DELETE CASCADE;

ALTER TABLE [CourseEnrollments] ADD CONSTRAINT [FK_CourseEnrollments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [SuccessionCandidates] ADD CONSTRAINT [FK_SuccessionCandidates_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [SuccessionCandidates] ADD CONSTRAINT [FK_SuccessionCandidates_SuccessionPlans_SuccessionPlanId] FOREIGN KEY ([SuccessionPlanId]) REFERENCES [SuccessionPlans] ([Id]) ON DELETE CASCADE;

ALTER TABLE [SuccessionHistories] ADD CONSTRAINT [FK_SuccessionHistories_SuccessionPlans_SuccessionPlanId] FOREIGN KEY ([SuccessionPlanId]) REFERENCES [SuccessionPlans] ([Id]) ON DELETE CASCADE;

ALTER TABLE [SuccessionPlans] ADD CONSTRAINT [FK_SuccessionPlans_JobPositions_TargetPositionId] FOREIGN KEY ([TargetPositionId]) REFERENCES [JobPositions] ([Id]) ON DELETE CASCADE;

ALTER TABLE [SuccessionPlans] ADD CONSTRAINT [FK_SuccessionPlans_PlanningPeriods_PlanningPeriodId] FOREIGN KEY ([PlanningPeriodId]) REFERENCES [PlanningPeriods] ([Id]) ON DELETE CASCADE;

ALTER TABLE [TrainingCertificates] ADD CONSTRAINT [FK_TrainingCertificates_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260909134852_AddManagerScopeAssignmentsP26', N'9.0.0');

UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Company.Address';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Company.Email';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Company.Name';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Company.Phone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Company.Representative';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Company.ShortName';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Company.TaxCode';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Company.Website';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Loc.Currency';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Loc.Language';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Loc.Timezone';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Storage.AllowedTypes';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Storage.MaxFileSizeMB';
SELECT @@ROWCOUNT;


UPDATE [SystemConfigs] SET [UpdatedAt] = '2026-09-09T00:00:00.0000000'
WHERE [Key] = N'Sys.MaintenanceMode';
SELECT @@ROWCOUNT;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260909135548_StabilizeSystemConfigSeedMetadata', N'9.0.0');

ALTER TABLE [ManagerAssignments] DROP CONSTRAINT [FK_ManagerAssignments_Users_UserId];

DROP INDEX [IX_ManagerAssignments_UserId] ON [ManagerAssignments];

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ManagerAssignments]') AND [c].[name] = N'OrganizationalUnit');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [ManagerAssignments] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [ManagerAssignments] ALTER COLUMN [OrganizationalUnit] nvarchar(450) NOT NULL;

CREATE TABLE [LeaderAssignments] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [OrganizationalUnit] nvarchar(450) NOT NULL,
    [EffectiveFrom] datetime2 NOT NULL,
    [EffectiveTo] datetime2 NULL,
    [AssignedBy] nvarchar(max) NULL,
    [AssignedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_LeaderAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LeaderAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_LeaderAssignments_UserId_OrgUnit_Unique] ON [LeaderAssignments] ([UserId], [OrganizationalUnit]);

CREATE UNIQUE INDEX [IX_ManagerAssignments_UserId_OrgUnit_Unique] ON [ManagerAssignments] ([UserId], [OrganizationalUnit]);

ALTER TABLE [ManagerAssignments] ADD CONSTRAINT [FK_ManagerAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260909170458_P42_AlignModelToSchema', N'9.0.0');

CREATE INDEX [IX_Proposals_RelatedEmployeeId] ON [Proposals] ([RelatedEmployeeId]);

CREATE INDEX [IX_Proposals_SourceProposalId] ON [Proposals] ([SourceProposalId]);

CREATE INDEX [IX_ProposalHistories_ProposalId] ON [ProposalHistories] ([ProposalId]);

CREATE INDEX [IX_PayrollPayments_EmployeeId] ON [PayrollPayments] ([EmployeeId]);

CREATE INDEX [IX_NotificationLogs_RuleId] ON [NotificationLogs] ([RuleId]);

CREATE INDEX [IX_HrTasks_AssigneeEmployeeId] ON [HrTasks] ([AssigneeEmployeeId]);

CREATE INDEX [IX_HrTasks_RelatedEmployeeId] ON [HrTasks] ([RelatedEmployeeId]);

CREATE INDEX [IX_HrTaskHistories_HrTaskId] ON [HrTaskHistories] ([HrTaskId]);

CREATE INDEX [IX_AttendanceAdjustmentRequests_AttendanceId] ON [AttendanceAdjustmentRequests] ([AttendanceId]);

CREATE INDEX [IX_AttendanceAdjustmentAttachments_RequestId] ON [AttendanceAdjustmentAttachments] ([RequestId]);

ALTER TABLE [AttendanceAdjustmentAttachments] ADD CONSTRAINT [FK_AttendanceAdjustmentAttachments_AttendanceAdjustmentRequests_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [AttendanceAdjustmentRequests] ([Id]) ON DELETE CASCADE;

ALTER TABLE [AttendanceAdjustmentRequests] ADD CONSTRAINT [FK_AttendanceAdjustmentRequests_Attendances_AttendanceId] FOREIGN KEY ([AttendanceId]) REFERENCES [Attendances] ([Id]) ON DELETE CASCADE;

ALTER TABLE [HrTaskHistories] ADD CONSTRAINT [FK_HrTaskHistories_HrTasks_HrTaskId] FOREIGN KEY ([HrTaskId]) REFERENCES [HrTasks] ([Id]) ON DELETE CASCADE;

ALTER TABLE [HrTasks] ADD CONSTRAINT [FK_HrTasks_Employees_AssigneeEmployeeId] FOREIGN KEY ([AssigneeEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [HrTasks] ADD CONSTRAINT [FK_HrTasks_Employees_RelatedEmployeeId] FOREIGN KEY ([RelatedEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [NotificationLogs] ADD CONSTRAINT [FK_NotificationLogs_NotificationRules_RuleId] FOREIGN KEY ([RuleId]) REFERENCES [NotificationRules] ([Id]) ON DELETE CASCADE;

ALTER TABLE [PayrollPayments] ADD CONSTRAINT [FK_PayrollPayments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [ProposalHistories] ADD CONSTRAINT [FK_ProposalHistories_Proposals_ProposalId] FOREIGN KEY ([ProposalId]) REFERENCES [Proposals] ([Id]) ON DELETE CASCADE;

ALTER TABLE [Proposals] ADD CONSTRAINT [FK_Proposals_Employees_RelatedEmployeeId] FOREIGN KEY ([RelatedEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Proposals] ADD CONSTRAINT [FK_Proposals_Proposals_SourceProposalId] FOREIGN KEY ([SourceProposalId]) REFERENCES [Proposals] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260909184115_P43_CompleteRelationshipConstraints', N'9.0.0');

ALTER TABLE [Proposals] ADD [RowVersion] rowversion NULL;

ALTER TABLE [Notifications] ADD [DeduplicationKey] nvarchar(450) NULL;

ALTER TABLE [Notifications] ADD [RecipientUserId] int NULL;

ALTER TABLE [Notifications] ADD [SenderName] nvarchar(max) NULL;

ALTER TABLE [Notifications] ADD [SenderUserId] int NULL;

UPDATE notification
                  SET RecipientUserId = recipient.Id
                  FROM Notifications AS notification
                  CROSS APPLY
                  (
                      SELECT TOP (1) appUser.Id
                      FROM Users AS appUser
                      LEFT JOIN Employees AS employee ON employee.Id = appUser.EmployeeId
                      WHERE appUser.Username = notification.NguoiNhan
                         OR appUser.FullName = notification.NguoiNhan
                         OR employee.Name = notification.NguoiNhan
                      ORDER BY CASE
                          WHEN appUser.Username = notification.NguoiNhan THEN 0
                          WHEN appUser.FullName = notification.NguoiNhan THEN 1
                          ELSE 2
                      END, appUser.Id
                  ) AS recipient;

                  IF EXISTS (SELECT 1 FROM Notifications WHERE RecipientUserId IS NULL)
                      THROW 51000, 'P43.1 cannot map one or more legacy notification recipients to Users.', 1;

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Notifications]') AND [c].[name] = N'RecipientUserId');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Notifications] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [Notifications] ALTER COLUMN [RecipientUserId] int NOT NULL;

CREATE UNIQUE INDEX [IX_Notifications_DeduplicationKey] ON [Notifications] ([DeduplicationKey]) WHERE [DeduplicationKey] IS NOT NULL;

CREATE INDEX [IX_Notifications_RecipientUserId] ON [Notifications] ([RecipientUserId]);

CREATE INDEX [IX_Notifications_SenderUserId] ON [Notifications] ([SenderUserId]);

ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_Users_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_Users_SenderUserId] FOREIGN KEY ([SenderUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260909192530_P43_1_TargetedBlockerRemediation', N'9.0.0');

CREATE TABLE [EventOutboxMessages] (
    [Id] int NOT NULL IDENTITY,
    [EventCode] nvarchar(100) NOT NULL,
    [PayloadJson] nvarchar(max) NOT NULL,
    [RelatedUsername] nvarchar(100) NULL,
    [RelatedManagerUsername] nvarchar(100) NULL,
    [IdempotencyKey] nvarchar(100) NULL,
    [Status] nvarchar(20) NOT NULL,
    [RetryCount] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [LastAttemptAt] datetime2 NULL,
    [LastError] nvarchar(max) NULL,
    CONSTRAINT [PK_EventOutboxMessages] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260914093928_AddEventOutbox', N'9.0.0');

CREATE TABLE [KPIPeriods] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_KPIPeriods] PRIMARY KEY ([Id])
);

CREATE TABLE [KPIEvaluations] (
    [Id] int NOT NULL IDENTITY,
    [PeriodId] int NOT NULL,
    [EmployeeId] int NOT NULL,
    [ManagerUserId] int NULL,
    [ManagerUsernameSnapshot] nvarchar(100) NULL,
    [Status] nvarchar(50) NOT NULL,
    [FinalScore] decimal(5,2) NULL,
    [FinalRating] nvarchar(20) NULL,
    [EmployeeDepartmentSnapshot] nvarchar(100) NULL,
    [EmployeePositionSnapshot] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(100) NULL,
    [CompletedAt] datetime2 NULL,
    [CompletedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_KPIEvaluations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_KPIEvaluations_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_KPIEvaluations_KPIPeriods_PeriodId] FOREIGN KEY ([PeriodId]) REFERENCES [KPIPeriods] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_KPIEvaluations_Users_ManagerUserId] FOREIGN KEY ([ManagerUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [KPITargets] (
    [Id] int NOT NULL IDENTITY,
    [EvaluationId] int NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Weight] decimal(5,2) NOT NULL,
    [Progress] decimal(5,2) NOT NULL,
    [ManagerScore] decimal(5,2) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_KPITargets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_KPITargets_KPIEvaluations_EvaluationId] FOREIGN KEY ([EvaluationId]) REFERENCES [KPIEvaluations] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_KPIEvaluations_EmployeeId] ON [KPIEvaluations] ([EmployeeId]);

CREATE INDEX [IX_KPIEvaluations_ManagerUserId] ON [KPIEvaluations] ([ManagerUserId]);

CREATE UNIQUE INDEX [IX_KPIEvaluations_PeriodId_EmployeeId_Unique] ON [KPIEvaluations] ([PeriodId], [EmployeeId]);

CREATE INDEX [IX_KPITargets_EvaluationId] ON [KPITargets] ([EvaluationId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915022204_AddP3PerformanceManagementCore', N'9.0.0');

CREATE TABLE [AiConversations] (
    [Id] nvarchar(128) NOT NULL,
    [UserId] int NOT NULL,
    [Title] nvarchar(200) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AiConversations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiConversations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [AiMessages] (
    [Id] int NOT NULL IDENTITY,
    [ConversationId] nvarchar(128) NOT NULL,
    [Role] nvarchar(20) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [Route] nvarchar(40) NULL,
    [Intent] nvarchar(120) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [SourceMetadataJson] nvarchar(max) NULL,
    [WarningCode] nvarchar(120) NULL,
    CONSTRAINT [PK_AiMessages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiMessages_AiConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [AiConversations] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AiConversations_UserId_UpdatedAt] ON [AiConversations] ([UserId], [UpdatedAt]);

CREATE INDEX [IX_AiMessages_ConversationId_CreatedAt] ON [AiMessages] ([ConversationId], [CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260916183034_AddAiConversations', N'9.0.0');

DROP INDEX [IX_ManagerAssignments_UserId_OrgUnit_Unique] ON [ManagerAssignments];

CREATE INDEX [IX_ManagerAssignments_UserId_OrgUnit] ON [ManagerAssignments] ([UserId], [OrganizationalUnit]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260917100000_Mgr02AllowHistoricalManagerAssignments', N'9.0.0');

CREATE TABLE [UserMemories] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(128) NOT NULL,
    [EmployeeId] nvarchar(128) NULL,
    [MemoryType] nvarchar(64) NOT NULL,
    [MemoryKey] nvarchar(128) NOT NULL,
    [Content] nvarchar(4000) NOT NULL,
    [Confidence] float NOT NULL DEFAULT 1.0E0,
    [Source] nvarchar(64) NOT NULL,
    [SourceConversationId] nvarchar(128) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_UserMemories] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_UserMemories_SourceConversationId] ON [UserMemories] ([SourceConversationId]);

CREATE INDEX [IX_UserMemories_User_Type_Key_Active] ON [UserMemories] ([UserId], [MemoryType], [MemoryKey], [IsActive]);

CREATE INDEX [IX_UserMemories_UserId_IsActive] ON [UserMemories] ([UserId], [IsActive]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260919060031_AddAvaUserMemories', N'9.0.0');

COMMIT;
GO

