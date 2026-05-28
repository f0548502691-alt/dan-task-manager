using System.Text.RegularExpressions;

namespace DanTaskManager.Tests;

public class DockerStartupConfigurationTests
{
    [Fact]
    public void DockerCompose_UsesSameDefaultPasswordForBackendAndSqlServer()
    {
        var compose = ReadRepoFile("docker-compose.yml");

        var backendConnectionString = Regex.Match(
            compose,
            @"ConnectionStrings__DefaultConnection:\s*(?<value>[^\r\n]+)");
        Assert.True(backendConnectionString.Success, "Backend connection string must be configured in docker-compose.yml.");

        var backendPassword = Regex.Match(
            backendConnectionString.Groups["value"].Value,
            @"Password=\$\{DANTASKMANAGER_DB_PASSWORD:-(?<fallback>[^}]+)\}");
        var sqlServerPassword = Regex.Match(
            compose,
            @"MSSQL_SA_PASSWORD:\s*\$\{DANTASKMANAGER_DB_PASSWORD:-(?<fallback>[^}]+)\}");

        Assert.True(backendPassword.Success, "Backend must read the database password from DANTASKMANAGER_DB_PASSWORD.");
        Assert.True(sqlServerPassword.Success, "SQL Server must read the SA password from DANTASKMANAGER_DB_PASSWORD.");
        Assert.Equal(sqlServerPassword.Groups["fallback"].Value, backendPassword.Groups["fallback"].Value);
        Assert.Equal("Your_strong_Password123", backendPassword.Groups["fallback"].Value);
        Assert.DoesNotContain("${DB_PASSWORD", compose);
        Assert.DoesNotContain("DB_PASSWORD:?", compose);
    }

    [Fact]
    public void EnvExample_DocumentsComposePasswordVariable()
    {
        var envExample = ReadRepoFile(".env.example");

        Assert.Contains("DANTASKMANAGER_DB_PASSWORD=Your_strong_Password123", envExample);
        Assert.DoesNotMatch(@"(?m)^DB_PASSWORD=", envExample);
        Assert.Contains("SQL Server's password policy", envExample);
    }

    [Fact]
    public void Program_RegistersDbBackedTaskTypeValidationServiceWithMemoryCache()
    {
        var program = ReadRepoFile("backend", "Program.cs");

        Assert.Contains("using Microsoft.Extensions.Caching.Memory;", program);
        Assert.Contains("builder.Services.AddMemoryCache();", program);
        Assert.Matches(
            @"builder\.Services\.AddScoped\(\s*sp\s*=>\s*new\s+TaskTypeValidationService\(\s*" +
            @"sp\.GetRequiredService<ApplicationDbContext>\(\)\s*,\s*" +
            @"sp\.GetRequiredService<IMemoryCache>\(\)\s*\)\s*\)\s*;",
            program);
        Assert.DoesNotContain("builder.Services.AddScoped<TaskTypeValidationService>();", program);
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from the test output directory.");
    }
}
