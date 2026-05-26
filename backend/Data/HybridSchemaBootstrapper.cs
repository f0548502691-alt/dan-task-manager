using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Data;

public static class HybridSchemaBootstrapper
{
    public static void EnsureSchema(ApplicationDbContext dbContext)
    {
        const string sql = """
            IF OBJECT_ID(N'[TaskTypes]', N'U') IS NULL
            BEGIN
                CREATE TABLE [TaskTypes](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Code] NVARCHAR(100) NOT NULL,
                    [DisplayName] NVARCHAR(255) NOT NULL,
                    [FinalStatus] INT NULL,
                    [IsActive] BIT NOT NULL CONSTRAINT [DF_TaskTypes_IsActive] DEFAULT(1),
                    [Version] INT NOT NULL CONSTRAINT [DF_TaskTypes_Version] DEFAULT(1),
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_TaskTypes_CreatedAt] DEFAULT(GETUTCDATE()),
                    [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_TaskTypes_UpdatedAt] DEFAULT(GETUTCDATE())
                );
            END;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_TaskTypes_Code' AND object_id = OBJECT_ID(N'[TaskTypes]')
            )
            BEGIN
                CREATE UNIQUE INDEX [IX_TaskTypes_Code] ON [TaskTypes]([Code]);
            END;

            IF OBJECT_ID(N'[TaskFieldDefinitions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [TaskFieldDefinitions](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [TaskTypeMetadataId] INT NOT NULL,
                    [FieldKey] NVARCHAR(100) NOT NULL,
                    [DataType] NVARCHAR(50) NOT NULL,
                    [IsRequired] BIT NOT NULL CONSTRAINT [DF_TaskFieldDefinitions_IsRequired] DEFAULT(1),
                    [MinLength] INT NULL,
                    [MaxLength] INT NULL,
                    [MinValue] DECIMAL(18,2) NULL,
                    [MaxValue] DECIMAL(18,2) NULL,
                    [ArrayLength] INT NULL,
                    [MinItems] INT NULL,
                    [MaxItems] INT NULL,
                    [ElementType] NVARCHAR(50) NULL,
                    [RegexPattern] NVARCHAR(500) NULL,
                    [AllowedValuesJson] NVARCHAR(MAX) NULL,
                    [AppliesFromStatus] INT NULL,
                    [AppliesToStatus] INT NULL,
                    [IsIndexed] BIT NOT NULL CONSTRAINT [DF_TaskFieldDefinitions_IsIndexed] DEFAULT(0),
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_TaskFieldDefinitions_CreatedAt] DEFAULT(GETUTCDATE()),
                    [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_TaskFieldDefinitions_UpdatedAt] DEFAULT(GETUTCDATE()),
                    CONSTRAINT [FK_TaskFieldDefinitions_TaskTypes_TaskTypeMetadataId]
                        FOREIGN KEY ([TaskTypeMetadataId]) REFERENCES [TaskTypes]([Id]) ON DELETE CASCADE
                );
            END;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_TaskFieldDefinitions_TaskTypeMetadataId_FieldKey'
                  AND object_id = OBJECT_ID(N'[TaskFieldDefinitions]')
            )
            BEGIN
                CREATE UNIQUE INDEX [IX_TaskFieldDefinitions_TaskTypeMetadataId_FieldKey]
                ON [TaskFieldDefinitions]([TaskTypeMetadataId], [FieldKey]);
            END;

            IF COL_LENGTH('Tasks', 'PriorityIndex') IS NULL
            BEGIN
                ALTER TABLE [Tasks]
                ADD [PriorityIndex] AS JSON_VALUE([CustomDataJson], '$.priority') PERSISTED;
            END;

            IF COL_LENGTH('Tasks', 'BranchNameIndex') IS NULL
            BEGIN
                ALTER TABLE [Tasks]
                ADD [BranchNameIndex] AS JSON_VALUE([CustomDataJson], '$.branchName') PERSISTED;
            END;

            IF COL_LENGTH('Tasks', 'DeadlineUtcIndex') IS NULL
            BEGIN
                ALTER TABLE [Tasks]
                ADD [DeadlineUtcIndex] AS TRY_CONVERT(datetime2, JSON_VALUE([CustomDataJson], '$.deadline'), 127) PERSISTED;
            END;

            IF NOT EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = N'CK_Tasks_CustomDataJson_IsJson'
                  AND parent_object_id = OBJECT_ID(N'[Tasks]')
            )
            BEGIN
                ALTER TABLE [Tasks] WITH NOCHECK
                ADD CONSTRAINT [CK_Tasks_CustomDataJson_IsJson]
                CHECK (ISJSON([CustomDataJson]) = 1);
            END;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Tasks_TaskType_Priority' AND object_id = OBJECT_ID(N'[Tasks]')
            )
            BEGIN
                CREATE INDEX [IX_Tasks_TaskType_Priority]
                ON [Tasks]([TaskType], [PriorityIndex])
                WHERE [PriorityIndex] IS NOT NULL;
            END;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Tasks_TaskType_BranchName' AND object_id = OBJECT_ID(N'[Tasks]')
            )
            BEGIN
                CREATE INDEX [IX_Tasks_TaskType_BranchName]
                ON [Tasks]([TaskType], [BranchNameIndex])
                WHERE [BranchNameIndex] IS NOT NULL;
            END;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Tasks_TaskType_DeadlineUtc' AND object_id = OBJECT_ID(N'[Tasks]')
            )
            BEGIN
                CREATE INDEX [IX_Tasks_TaskType_DeadlineUtc]
                ON [Tasks]([TaskType], [DeadlineUtcIndex])
                WHERE [DeadlineUtcIndex] IS NOT NULL;
            END;

            IF NOT EXISTS (SELECT 1 FROM [TaskTypes] WHERE [Code] = N'Procurement')
            BEGIN
                INSERT INTO [TaskTypes] ([Code], [DisplayName], [FinalStatus], [IsActive], [Version], [CreatedAt], [UpdatedAt])
                VALUES (N'Procurement', N'Procurement', 3, 1, 1, GETUTCDATE(), GETUTCDATE());
            END;

            IF NOT EXISTS (SELECT 1 FROM [TaskTypes] WHERE [Code] = N'Development')
            BEGIN
                INSERT INTO [TaskTypes] ([Code], [DisplayName], [FinalStatus], [IsActive], [Version], [CreatedAt], [UpdatedAt])
                VALUES (N'Development', N'Development', 4, 1, 1, GETUTCDATE(), GETUTCDATE());
            END;

            DECLARE @ProcurementId INT = (SELECT TOP 1 [Id] FROM [TaskTypes] WHERE [Code] = N'Procurement');
            DECLARE @DevelopmentId INT = (SELECT TOP 1 [Id] FROM [TaskTypes] WHERE [Code] = N'Development');

            IF @ProcurementId IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM [TaskFieldDefinitions]
                    WHERE [TaskTypeMetadataId] = @ProcurementId AND [FieldKey] = N'prices'
               )
            BEGIN
                INSERT INTO [TaskFieldDefinitions]
                    ([TaskTypeMetadataId], [FieldKey], [DataType], [IsRequired], [ArrayLength], [ElementType], [AppliesFromStatus], [AppliesToStatus], [IsIndexed], [CreatedAt], [UpdatedAt])
                VALUES
                    (@ProcurementId, N'prices', N'array', 1, 2, N'string', 2, 2, 0, GETUTCDATE(), GETUTCDATE());
            END;

            IF @ProcurementId IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM [TaskFieldDefinitions]
                    WHERE [TaskTypeMetadataId] = @ProcurementId AND [FieldKey] = N'receipt'
               )
            BEGIN
                INSERT INTO [TaskFieldDefinitions]
                    ([TaskTypeMetadataId], [FieldKey], [DataType], [IsRequired], [AppliesFromStatus], [AppliesToStatus], [IsIndexed], [CreatedAt], [UpdatedAt])
                VALUES
                    (@ProcurementId, N'receipt', N'string', 1, 3, 3, 0, GETUTCDATE(), GETUTCDATE());
            END;

            IF @DevelopmentId IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM [TaskFieldDefinitions]
                    WHERE [TaskTypeMetadataId] = @DevelopmentId AND [FieldKey] = N'specification'
               )
            BEGIN
                INSERT INTO [TaskFieldDefinitions]
                    ([TaskTypeMetadataId], [FieldKey], [DataType], [IsRequired], [MinLength], [AppliesFromStatus], [AppliesToStatus], [IsIndexed], [CreatedAt], [UpdatedAt])
                VALUES
                    (@DevelopmentId, N'specification', N'string', 1, 10, 2, 2, 0, GETUTCDATE(), GETUTCDATE());
            END;

            IF @DevelopmentId IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM [TaskFieldDefinitions]
                    WHERE [TaskTypeMetadataId] = @DevelopmentId AND [FieldKey] = N'branchName'
               )
            BEGIN
                INSERT INTO [TaskFieldDefinitions]
                    ([TaskTypeMetadataId], [FieldKey], [DataType], [IsRequired], [RegexPattern], [AppliesFromStatus], [AppliesToStatus], [IsIndexed], [CreatedAt], [UpdatedAt])
                VALUES
                    (@DevelopmentId, N'branchName', N'string', 1, N'valid_git_branch', 3, 3, 1, GETUTCDATE(), GETUTCDATE());
            END;

            IF @DevelopmentId IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM [TaskFieldDefinitions]
                    WHERE [TaskTypeMetadataId] = @DevelopmentId AND [FieldKey] = N'versionNumber'
               )
            BEGIN
                INSERT INTO [TaskFieldDefinitions]
                    ([TaskTypeMetadataId], [FieldKey], [DataType], [IsRequired], [RegexPattern], [AppliesFromStatus], [AppliesToStatus], [IsIndexed], [CreatedAt], [UpdatedAt])
                VALUES
                    (@DevelopmentId, N'versionNumber', N'stringOrNumber', 1, N'semantic_version', 4, 4, 0, GETUTCDATE(), GETUTCDATE());
            END;
            """;

        dbContext.Database.ExecuteSqlRaw(sql);
    }
}
