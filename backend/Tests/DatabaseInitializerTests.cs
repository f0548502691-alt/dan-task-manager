using DanTaskManager.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DanTaskManager.Tests;

public class DatabaseInitializerTests
{
    [Fact]
    public void ExecuteWithRetry_RetriesTransientFailuresAndStopsAfterSuccess()
    {
        var attempts = 0;
        var sleepDurations = new List<TimeSpan>();
        var retryDelay = TimeSpan.FromMilliseconds(125);

        DatabaseInitializer.ExecuteWithRetry(
            initializeOnce: () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException("SQL Server is still starting.");
                }
            },
            logger: NullLogger.Instance,
            maxAttempts: 5,
            retryDelay: retryDelay,
            sleep: sleepDurations.Add);

        Assert.Equal(3, attempts);
        Assert.Equal(new[] { retryDelay, retryDelay }, sleepDurations);
    }

    [Fact]
    public void ExecuteWithRetry_RethrowsWhenAllAttemptsFail()
    {
        var attempts = 0;
        var sleepDurations = new List<TimeSpan>();
        var retryDelay = TimeSpan.FromMilliseconds(125);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseInitializer.ExecuteWithRetry(
                initializeOnce: () =>
                {
                    attempts++;
                    throw new InvalidOperationException($"failure {attempts}");
                },
                logger: NullLogger.Instance,
                maxAttempts: 3,
                retryDelay: retryDelay,
                sleep: sleepDurations.Add));

        Assert.Equal("failure 3", exception.Message);
        Assert.Equal(3, attempts);
        Assert.Equal(new[] { retryDelay, retryDelay }, sleepDurations);
    }
}
