using System;
using DanTaskManager.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DanTaskManager.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260527184500_InitialSchema")]
public partial class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TaskTypes",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                FinalStatus = table.Column<int>(type: "int", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskTypes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TaskFieldDefinitions",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TaskTypeMetadataId = table.Column<int>(type: "int", nullable: false),
                FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DataType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                IsRequired = table.Column<bool>(type: "bit", nullable: false),
                MinLength = table.Column<int>(type: "int", nullable: true),
                MaxLength = table.Column<int>(type: "int", nullable: true),
                MinValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                MaxValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                ArrayLength = table.Column<int>(type: "int", nullable: true),
                MinItems = table.Column<int>(type: "int", nullable: true),
                MaxItems = table.Column<int>(type: "int", nullable: true),
                ElementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                RegexPattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                AllowedValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AppliesFromStatus = table.Column<int>(type: "int", nullable: true),
                AppliesToStatus = table.Column<int>(type: "int", nullable: true),
                IsIndexed = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskFieldDefinitions", x => x.Id);
                table.ForeignKey(
                    name: "FK_TaskFieldDefinitions_TaskTypes_TaskTypeMetadataId",
                    column: x => x.TaskTypeMetadataId,
                    principalTable: "TaskTypes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Tasks",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TaskType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CurrentStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                AssignedToUserId = table.Column<int>(type: "int", nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                CustomDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tasks", x => x.Id);
                table.CheckConstraint("CK_Tasks_CustomDataJson_IsJson", "ISJSON([CustomDataJson]) = 1");
                table.ForeignKey(
                    name: "FK_Tasks_Users_AssignedToUserId",
                    column: x => x.AssignedToUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql("""
            DECLARE @SeedTimestamp datetime2 = '2026-05-25T00:00:00';

            SET IDENTITY_INSERT [TaskTypes] ON;
            INSERT INTO [TaskTypes] ([Id], [Code], [CreatedAt], [DisplayName], [FinalStatus], [IsActive], [UpdatedAt], [Version])
            VALUES
                (1, N'Procurement', @SeedTimestamp, N'Procurement', 3, CAST(1 AS bit), @SeedTimestamp, 1),
                (2, N'Development', @SeedTimestamp, N'Development', 4, CAST(1 AS bit), @SeedTimestamp, 1),
                (3, N'Marketing', @SeedTimestamp, N'Marketing', 3, CAST(1 AS bit), @SeedTimestamp, 1);
            SET IDENTITY_INSERT [TaskTypes] OFF;

            SET IDENTITY_INSERT [Users] ON;
            INSERT INTO [Users] ([Id], [CreatedAt], [Email], [Name])
            VALUES
                (1, @SeedTimestamp, N'dan@example.com', N'Dan Cohen'),
                (2, @SeedTimestamp, N'ruth@example.com', N'Ruth Levi'),
                (3, @SeedTimestamp, N'moshe@example.com', N'Moshe Avraham'),
                (4, @SeedTimestamp, N'noa@example.com', N'Noa Israeli'),
                (5, @SeedTimestamp, N'eitan@example.com', N'Eitan Barak'),
                (6, @SeedTimestamp, N'michal@example.com', N'Michal Gal');
            SET IDENTITY_INSERT [Users] OFF;

            SET IDENTITY_INSERT [TaskFieldDefinitions] ON;
            INSERT INTO [TaskFieldDefinitions]
                ([Id], [AllowedValuesJson], [AppliesFromStatus], [AppliesToStatus], [ArrayLength],
                 [CreatedAt], [DataType], [ElementType], [FieldKey], [IsIndexed], [IsRequired],
                 [MaxItems], [MaxLength], [MaxValue], [MinItems], [MinLength], [MinValue],
                 [RegexPattern], [TaskTypeMetadataId], [UpdatedAt])
            VALUES
                (1, NULL, 2, 2, 2, @SeedTimestamp, N'array', N'string', N'prices', CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, @SeedTimestamp),
                (2, NULL, 3, 3, NULL, @SeedTimestamp, N'string', NULL, N'receipt', CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, @SeedTimestamp),
                (3, NULL, 2, 2, NULL, @SeedTimestamp, N'string', NULL, N'specification', CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, 10, NULL, NULL, 2, @SeedTimestamp),
                (4, NULL, 3, 3, NULL, @SeedTimestamp, N'string', NULL, N'branchName', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, NULL, NULL, N'valid_git_branch', 2, @SeedTimestamp),
                (5, NULL, 4, 4, NULL, @SeedTimestamp, N'stringOrNumber', NULL, N'versionNumber', CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, NULL, NULL, N'semantic_version', 2, @SeedTimestamp),
                (6, NULL, 2, 2, NULL, @SeedTimestamp, N'string', NULL, N'campaignName', CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, 3, NULL, NULL, 3, @SeedTimestamp),
                (7, N'["B2B","B2C","Internal"]', 2, 2, NULL, @SeedTimestamp, N'string', NULL, N'targetAudience', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, NULL, NULL, NULL, 3, @SeedTimestamp),
                (8, NULL, 3, 3, NULL, @SeedTimestamp, N'string', NULL, N'launchDate', CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, NULL, NULL, N'^\d{4}-\d{2}-\d{2}$', 3, @SeedTimestamp);
            SET IDENTITY_INSERT [TaskFieldDefinitions] OFF;

            SET IDENTITY_INSERT [Tasks] ON;
            INSERT INTO [Tasks] ([Id], [AssignedToUserId], [CreatedAt], [CurrentStatus], [CustomDataJson], [Description], [TaskType], [UpdatedAt])
            VALUES
                (1, 1, @SeedTimestamp, 1, N'{}', N'Collect supplier quotes for new equipment', N'Procurement', @SeedTimestamp),
                (2, 2, @SeedTimestamp, 1, N'{}', N'Develop the user management module', N'Development', @SeedTimestamp);
            SET IDENTITY_INSERT [Tasks] OFF;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_TaskFieldDefinitions_TaskTypeMetadataId_FieldKey",
            table: "TaskFieldDefinitions",
            columns: new[] { "TaskTypeMetadataId", "FieldKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Tasks_AssignedToUserId",
            table: "Tasks",
            column: "AssignedToUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Tasks_TaskType",
            table: "Tasks",
            column: "TaskType");

        migrationBuilder.CreateIndex(
            name: "IX_TaskTypes_Code",
            table: "TaskTypes",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TaskFieldDefinitions");

        migrationBuilder.DropTable(
            name: "Tasks");

        migrationBuilder.DropTable(
            name: "TaskTypes");

        migrationBuilder.DropTable(
            name: "Users");
    }
}
