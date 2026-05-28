using DanTaskManager.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DanTaskManager.Tests;

public class InitialSchemaMigrationTests
{
    [Fact]
    public void Model_UsesExplicitDecimalStoreTypesForFieldNumericBounds()
    {
        using var context = CreateSqlServerContext();

        var entityType = context.Model.FindEntityType(typeof(TaskFieldDefinition));

        Assert.NotNull(entityType);
        Assert.Equal("decimal(18,2)", entityType!.FindProperty(nameof(TaskFieldDefinition.MinValue))!.GetColumnType());
        Assert.Equal("decimal(18,2)", entityType.FindProperty(nameof(TaskFieldDefinition.MaxValue))!.GetColumnType());
    }

    [Fact]
    public void InitialSchemaMigration_GeneratesSqlServerScriptWithTypedSeedData()
    {
        using var context = CreateSqlServerContext();

        var script = context
            .GetService<IMigrator>()
            .GenerateScript(fromMigration: Migration.InitialDatabase, toMigration: "20260527184500_InitialSchema");

        Assert.Contains("[MinValue] decimal(18,2) NULL", script);
        Assert.Contains("[MaxValue] decimal(18,2) NULL", script);

        Assert.Contains("DECLARE @SeedTimestamp datetime2 = '2026-05-25T00:00:00';", script);
        Assert.Contains("SET IDENTITY_INSERT [TaskTypes] ON;", script);
        Assert.Contains("SET IDENTITY_INSERT [TaskFieldDefinitions] ON;", script);
        Assert.Contains("CAST(1 AS bit)", script);
        Assert.Contains("N'[\"B2B\",\"B2C\",\"Internal\"]'", script);
        Assert.Contains("N'^\\d{4}-\\d{2}-\\d{2}$'", script);
        Assert.Contains("N'{}'", script);
    }

    private static ApplicationDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=DanTaskManagerMigrationTests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new ApplicationDbContext(options);
    }
}
