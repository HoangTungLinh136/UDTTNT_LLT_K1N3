USE [HRManagementDB];
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

/* =========================================================
   1. SystemConfigs
   ========================================================= */

IF OBJECT_ID(N'dbo.SystemConfigs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemConfigs
    (
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(max) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NOT NULL,

        CONSTRAINT PK_SystemConfigs
            PRIMARY KEY ([Key])
    );
END;
GO


/* =========================================================
   2. Seed SystemConfigs
   Chỉ insert nếu key chưa tồn tại
   ========================================================= */

DECLARE @Seed TABLE
(
    [Key] nvarchar(100),
    [Value] nvarchar(max),
    [Category] nvarchar(50),
    [Description] nvarchar(500)
);

INSERT INTO @Seed ([Key],[Value],[Category],[Description])
VALUES
(N'Company.Address',        N'Tầng 12, Tòa nhà Tech, Hà Nội', N'Company',      N'Địa chỉ trụ sở'),
(N'Company.Email',          N'contact@hrms.vn',               N'Company',      N'Email liên hệ'),
(N'Company.Name',           N'Công ty CP Công nghệ HRMS',     N'Company',      N'Tên công ty'),
(N'Company.Phone',          N'1900 1000',                     N'Company',      N'Hotline'),
(N'Company.Representative', N'Nguyễn Văn A',                  N'Company',      N'Người đại diện'),
(N'Company.ShortName',      N'HRMS Corp',                     N'Company',      N'Tên viết tắt'),
(N'Company.TaxCode',        N'0101234567',                    N'Company',      N'Mã số thuế'),
(N'Company.Website',        N'https://hrms.vn',               N'Company',      N'Website'),
(N'Loc.Currency',           N'VND',                           N'Localization', N'Tiền tệ'),
(N'Loc.Language',           N'vi-VN',                         N'Localization', N'Ngôn ngữ mặc định'),
(N'Loc.Timezone',           N'SE Asia Standard Time',         N'Localization', N'Múi giờ'),
(N'Storage.AllowedTypes',   N'.jpg,.jpeg,.png,.pdf,.doc,.docx,.xls,.xlsx', N'Storage', N'Định dạng cho phép'),
(N'Storage.MaxFileSizeMB',  N'10',                            N'Storage',      N'Dung lượng upload tối đa (MB)'),
(N'Sys.MaintenanceMode',    N'false',                         N'System',       N'Chế độ bảo trì');

INSERT INTO dbo.SystemConfigs
(
    [Key],
    [Value],
    [Category],
    [Description],
    [UpdatedAt],
    [UpdatedBy]
)
SELECT
    s.[Key],
    s.[Value],
    s.[Category],
    s.[Description],
    CAST('2026-09-09T00:00:00' AS datetime2),
    N'System'
FROM @Seed s
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.SystemConfigs x
    WHERE x.[Key] = s.[Key]
);
GO


/* =========================================================
   3. MonitoringAlerts
   ========================================================= */

IF OBJECT_ID(N'dbo.MonitoringAlerts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MonitoringAlerts
    (
        [Id] int IDENTITY(1,1) NOT NULL,
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

        CONSTRAINT PK_MonitoringAlerts
            PRIMARY KEY ([Id])
    );
END;
GO


/* =========================================================
   4. SystemErrorLogs
   ========================================================= */

IF OBJECT_ID(N'dbo.SystemErrorLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemErrorLogs
    (
        [Id] int IDENTITY(1,1) NOT NULL,
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

        CONSTRAINT PK_SystemErrorLogs
            PRIMARY KEY ([Id])
    );
END;
GO


/* =========================================================
   5. VERIFY
   ========================================================= */

SELECT
    CASE WHEN OBJECT_ID(N'dbo.SystemConfigs',N'U') IS NOT NULL
         THEN N'EXISTS' ELSE N'MISSING' END AS SystemConfigs,

    CASE WHEN OBJECT_ID(N'dbo.MonitoringAlerts',N'U') IS NOT NULL
         THEN N'EXISTS' ELSE N'MISSING' END AS MonitoringAlerts,

    CASE WHEN OBJECT_ID(N'dbo.SystemErrorLogs',N'U') IS NOT NULL
         THEN N'EXISTS' ELSE N'MISSING' END AS SystemErrorLogs;

SELECT COUNT(*) AS SystemConfigCount
FROM dbo.SystemConfigs;
GO

COMMIT TRANSACTION;
GO
