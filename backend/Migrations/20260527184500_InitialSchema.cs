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

        migrationBuilder.InsertData(
            table: "TaskTypes",
            columns: new[] { "Id", "Code", "CreatedAt", "DisplayName", "FinalStatus", "IsActive", "UpdatedAt", "Version" },
            values: new object[,]
            {
                { 1, "Procurement", new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "Procurement", 3, true, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), 1 },
                { 2, "Development", new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "Development", 4, true, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), 1 },
                { 3, "Marketing", new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "Marketing", 3, true, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), 1 }
            });

        migrationBuilder.InsertData(
            table: "Users",
            columns: new[] { "Id", "CreatedAt", "Email", "Name" },
            values: new object[,]
            {
                { 1, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "dan@example.com", "Dan Cohen" },
                { 2, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "ruth@example.com", "Ruth Levi" },
                { 3, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "moshe@example.com", "Moshe Avraham" },
                { 4, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "noa@example.com", "Noa Israeli" },
                { 5, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "eitan@example.com", "Eitan Barak" },
                { 6, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "michal@example.com", "Michal Gal" }
            });

        migrationBuilder.InsertData(
            table: "TaskFieldDefinitions",
            columns: new[]
            {
                "Id", "AllowedValuesJson", "AppliesFromStatus", "AppliesToStatus", "ArrayLength",
                "CreatedAt", "DataType", "ElementType", "FieldKey", "IsIndexed", "IsRequired",
                "MaxItems", "MaxLength", "MaxValue", "MinItems", "MinLength", "MinValue",
                "RegexPattern", "TaskTypeMetadataId", "UpdatedAt"
            },
            values: new object[,]
            {
                { 1, null, 2, 2, 2, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "array", "string", "prices", false, true, null, null, null, null, null, null, null, 1, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc) },
                { 2, null, 3, 3, null, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "string", null, "receipt", false, true, null, null, null, null, null, null, null, 1, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc) },
                { 3, null, 2, 2, null, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "string", null, "specification", false, true, null, null, null, null, 10, null, null, 2, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc) },
                { 4, null, 3, 3, null, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "string", null, "branchName", true, true, null, null, null, null, null, null, "valid_git_branch", 2, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc) },
                { 5, null, 4, 4, null, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "stringOrNumber", null, "versionNumber", false, true, null, null, null, null, null, null, "semantic_version", 2, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc) },
                { 6, null, 2, 2, null, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "string", null, "campaignName", false, true, null, null, null, null, 3, null, null, 3, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc) },
                { 7, "[\"B2B\",\"B2C\",\"Internal\"]", 2, 2, null, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "string", null, "targetAudience", true, true, null, null, null, null, null, null, null, 3, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc) },
                { 8, null, 3, 3, null, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), "string", null, "launchDate", false, true, null, null, null, null, null, null, "^\\d{4}-\\d{2}-\\d{2}$", 3, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc) }
            });

        migrationBuilder.InsertData(
            table: "Tasks",
            columns: new[] { "Id", "AssignedToUserId", "CreatedAt", "CurrentStatus", "CustomDataJson", "Description", "TaskType", "UpdatedAt" },
            values: new object[,]
            {
                { 1, 1, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), 1, "{}", "Collect supplier quotes for new equipment", "Procurement", new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc) },
                { 2, 2, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), 1, "{}", "Develop the user management module", "Development", new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc) }
            });

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
