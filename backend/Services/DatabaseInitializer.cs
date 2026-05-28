using DanTaskManager.Data;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Services;

public static class DatabaseInitializer
{
    public static void Initialize(WebApplication app)
    {
        ExecuteWithRetry(
            () =>
            {
                using var scope = app.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (dbContext.Database.GetMigrations().Any())
                {
                    dbContext.Database.Migrate();
                }
                else
                {
                    dbContext.Database.EnsureCreated();
                }
            },
            app.Logger,
            maxAttempts: 30,
            retryDelay: TimeSpan.FromSeconds(2),
            sleep: Thread.Sleep);
    }

    internal static void ExecuteWithRetry(
        Action initializeOnce,
        ILogger logger,
        int maxAttempts,
        TimeSpan retryDelay,
        Action<TimeSpan> sleep)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                initializeOnce();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Database initialization failed on attempt {Attempt}/{MaxAttempts}; retrying in {DelaySeconds} seconds.",
                    attempt,
                    maxAttempts,
                    retryDelay.TotalSeconds);

                sleep(retryDelay);
            }
        }
    }
}
