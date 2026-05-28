using System.Text.RegularExpressions;

namespace DanTaskManager.Tests;

public class DockerQuickStartDocumentationTests
{
    [Fact]
    public void ReadmeQuickStart_UsesEnvExampleAndSingleComposeCommand()
    {
        var readme = ReadRepoFile("README.md");
        var dockerQuickStart = ExtractSection(readme, "## Quick start with Docker");

        Assert.Contains("cp .env.example .env", dockerQuickStart);
        Assert.Contains("docker compose up -d", dockerQuickStart);
        Assert.DoesNotContain("npm start", dockerQuickStart);
        Assert.Contains("No separate frontend command or local Node.js installation is required", dockerQuickStart);
    }

    [Fact]
    public void ReadmePasswordGuidance_MatchesComposeAndEnvExample()
    {
        var readme = ReadRepoFile("README.md");
        var compose = ReadRepoFile("docker-compose.yml");
        var envExample = ReadRepoFile(".env.example");

        var envPassword = ExtractAssignment(envExample, "DANTASKMANAGER_DB_PASSWORD");
        var composeFallbacks = Regex.Matches(
                compose,
                @"\$\{DANTASKMANAGER_DB_PASSWORD:-(?<password>[^}]+)\}")
            .Select(match => match.Groups["password"].Value)
            .ToArray();

        Assert.Contains("DANTASKMANAGER_DB_PASSWORD", readme);
        Assert.Contains("SQL Server requires 8+ chars with mixed character types", readme);
        Assert.Equal(new[] { envPassword, envPassword }, composeFallbacks);
        Assert.Contains($"Password=${{DANTASKMANAGER_DB_PASSWORD:-{envPassword}}}", compose);
        Assert.Contains($"MSSQL_SA_PASSWORD: ${{DANTASKMANAGER_DB_PASSWORD:-{envPassword}}}", compose);
    }

    [Fact]
    public void ReadmeFrontendDockerDescription_MatchesDockerProxyConfiguration()
    {
        var readme = ReadRepoFile("README.md");
        var packageJson = ReadRepoFile("frontend/package.json");
        var frontendDockerfile = ReadRepoFile("frontend/Dockerfile");
        var dockerProxy = ReadRepoFile("frontend/proxy.docker.conf.json");

        Assert.Contains("frontend/proxy.docker.conf.json", readme);
        Assert.Contains("http://localhost:4200", readme);
        Assert.Contains("\"start:docker\": \"ng serve --host 0.0.0.0 --port 4200 --proxy-config proxy.docker.conf.json\"", packageJson);
        Assert.Contains("CMD [\"npm\", \"run\", \"start:docker\"]", frontendDockerfile);
        Assert.Contains("\"target\": \"http://backend:8080\"", dockerProxy);
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")) &&
                Directory.Exists(Path.Combine(directory.FullName, "backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string ExtractSection(string markdown, string heading)
    {
        var start = markdown.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing markdown heading '{heading}'.");

        var nextHeading = markdown.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
        return nextHeading >= 0
            ? markdown[start..nextHeading]
            : markdown[start..];
    }

    private static string ExtractAssignment(string contents, string key)
    {
        var match = Regex.Match(contents, $@"(?m)^{Regex.Escape(key)}=(?<value>\S+)$");
        Assert.True(match.Success, $"Missing {key} assignment.");
        return match.Groups["value"].Value;
    }
}
