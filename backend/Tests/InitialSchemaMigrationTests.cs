using DanTaskManager.Data;
using DanTaskManager.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DanTaskManager.Tests;

public class InitialSchemaMigrationTests
{
    private const string InitialSchemaMigrationId = "20260527184500_InitialSchema";

    [Fact]
    public void InitialSchemaMigration_IsRegisteredWithApplicationSnapshot()
    {
        using var context = CreateSqlServerContext();
        var migrations = context.GetService<IMigrationsAssembly>();

        Assert.Contains(InitialSchemaMigrationId, migrations.Migrations.Keys);
        Assert.IsType<InitialSchema>(migrations.Migrations[InitialSchemaMigrationId].CreateInstance());
        Assert.IsType<ApplicationDbContextModelSnapshot>(migrations.ModelSnapshot);
    }

    [Fact]
    public void InitialSchemaMigration_GeneratesCriticalSchemaAndSeedData()
    {
        using var context = CreateSqlServerContext();

        var script = context
            .GetService<IMigrator>()
            .GenerateScript(fromMigration: null, toMigration: InitialSchemaMigrationId);

        Assert.Contains("CREATE TABLE [Users]", script);
        Assert.Contains("CREATE TABLE [TaskTypes]", script);
        Assert.Contains("CREATE TABLE [TaskFieldDefinitions]", script);
        Assert.Contains("CREATE TABLE [Tasks]", script);

        Assert.Contains(
            "CONSTRAINT [CK_Tasks_CustomDataJson_IsJson] CHECK (ISJSON([CustomDataJson]) = 1)",
            script);
        Assert.Contains(
            "CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);",
            script);
        Assert.Contains(
            "CREATE UNIQUE INDEX [IX_TaskTypes_Code] ON [TaskTypes] ([Code]);",
            script);
        Assert.Contains(
            "CREATE UNIQUE INDEX [IX_TaskFieldDefinitions_TaskTypeMetadataId_FieldKey] ON [TaskFieldDefinitions] ([TaskTypeMetadataId], [FieldKey]);",
            script);
        Assert.Contains("CONSTRAINT [FK_Tasks_Users_AssignedToUserId]", script);
        Assert.Contains("REFERENCES [Users] ([Id])", script);

        Assert.Contains("N'Development'", script);
        Assert.Contains("N'Marketing'", script);
        Assert.Contains("N'targetAudience'", script);
        Assert.Contains("N'[\"B2B\",\"B2C\",\"Internal\"]'", script);
        Assert.Contains("N'{}'", script);
    }

    private static ApplicationDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=DanTaskManagerMigrationTests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new ApplicationDbContext(options);
    }
}
