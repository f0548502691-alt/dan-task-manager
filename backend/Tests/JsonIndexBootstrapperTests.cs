using DanTaskManager.Data;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DanTaskManager.Tests;

public class JsonIndexBootstrapperTests
{
    [Fact]
    public void BuildColumnName_SanitizesFieldKeyToValidSqlIdentifier()
    {
        Assert.Equal("cd_branchName", JsonIndexBootstrapper.BuildColumnName("branchName"));
        Assert.Equal("cd_target_audience", JsonIndexBootstrapper.BuildColumnName("target audience"));
        Assert.Equal("cd___amount_USD_", JsonIndexBootstrapper.BuildColumnName("$.amount USD!"));
        Assert.Equal("cd_simple", JsonIndexBootstrapper.BuildColumnName("simple"));
    }

    [Fact]
    public async Task StartAsync_NoOpOnInMemoryProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"JsonIndexBootstrapper-{Guid.NewGuid()}"));
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        // Trigger schema creation so the bootstrapper sees seeded indexed fields.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        var bootstrapper = new JsonIndexBootstrapper(
            provider,
            NullLogger<JsonIndexBootstrapper>.Instance);

        // The InMemory provider would throw on raw ALTER TABLE — a silent no-op
        // is the only acceptable outcome here.
        await bootstrapper.StartAsync(CancellationToken.None);
    }
}
