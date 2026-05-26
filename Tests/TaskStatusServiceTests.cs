using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.Extensions.Logging;

namespace DanTaskManager.Tests;

public class TaskStatusServiceTests
{
    [Fact]
    public void ValidateAndChangeStatus_WithUnknownTaskType_FailsClosed()
    {
        var service = CreateService(new ProcurementTaskHandler());
        var task = new BaseTask
        {
            Id = 42,
            TaskType = "Unknown",
            CurrentStatus = 0,
            CustomDataJson = "{}"
        };

        var result = service.ValidateAndChangeStatus(task, 1, "{}");

        Assert.False(result.Success);
        Assert.Null(result.NewStatus);
        Assert.Contains("לא נתמך", result.Message);
        Assert.Equal(0, task.CurrentStatus);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown")]
    public void GetFinalStatus_WithUnsupportedOrBlankTaskType_ReturnsNull(string taskType)
    {
        var service = CreateService(new ProcurementTaskHandler());

        var finalStatus = service.GetFinalStatus(taskType);

        Assert.Null(finalStatus);
    }

    private static TaskStatusService CreateService(params ITaskHandler[] handlers)
    {
        return new TaskStatusService(
            new TaskHandlerFactory(handlers),
            new MockLogger<TaskStatusService>());
    }

    private class MockLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
