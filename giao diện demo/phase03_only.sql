BEGIN TRANSACTION;
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

