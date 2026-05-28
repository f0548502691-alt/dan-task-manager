using System.Text.RegularExpressions;

namespace DanTaskManager.Tests;

public class DockerStartupConfigurationTests
{
    [Fact]
    public void DockerCompose_UsesSamePasswordVariableForApiAndSqlServer()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");

        Assert.Contains(
            "Password=${DANTASKMANAGER_DB_PASSWORD:-Your_strong_Password123}",
            compose);
        Assert.Contains(
            "MSSQL_SA_PASSWORD: ${DANTASKMANAGER_DB_PASSWORD:-Your_strong_Password123}",
            compose);
        Assert.DoesNotMatch(new Regex(@"(?<!DANTASKMANAGER_)DB_PASSWORD"), compose);
    }

    [Fact]
    public void EnvExample_DocumentsPasswordVariableUsedByCompose()
    {
        var envExample = ReadRepositoryFile(".env.example");

        Assert.Contains("DANTASKMANAGER_DB_PASSWORD=Your_strong_Password123", envExample);
        Assert.DoesNotContain(Environment.NewLine + "DB_PASSWORD=", envExample);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relativePath} from the test working directory.");
    }
}
