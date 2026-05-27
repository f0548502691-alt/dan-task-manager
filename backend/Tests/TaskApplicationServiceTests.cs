using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Persistence;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Tests;

public class TaskApplicationServiceTests
{
    [Fact]
    public async Task CreateAsync_WithUnsupportedTaskType_ReturnsSupportedTaskTypes()
    {
        var dbName = $"TaskApplicationServiceTests-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Users.Add(new AppUser
        {
            Id = 1,
            Name = "Test User",
            Email = "test@example.com"
        });
        await context.SaveChangesAsync();

        var service = new TaskApplicationService(
            new EfTaskRepository(context),
            new EfUserRepository(context),
            new NoOpWorkflowService(),
            new MockLogger());

        var result = await service.CreateAsync(
            new TaskCreateCommand(
                "UnknownType",
                "Unsupported task type",
                1,
                "{}"));

        Assert.False(result.Success);
        Assert.Contains("Unsupported", result.Message);
        Assert.Equal(
            new[] { "Development", "Procurement" },
            result.SupportedTaskTypes);
    }

    private class NoOpWorkflowService : ITaskWorkflowService
    {
        public Task<WorkflowResult> ChangeStatusAsync(
            int taskId,
            int newStatus,
            int nextAssignedToUserId,
            string newDataJson,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WorkflowResult> CloseTaskAsync(
            int taskId,
            int nextAssignedToUserId,
            string finalNotes,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WorkflowResult> EnsureTaskMutableAsync(
            int taskId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IEnumerable<BaseTask>> GetUserTasksAsync(
            int userId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<BaseTask?> GetTaskAsync(int taskId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class MockLogger : ILogger<TaskApplicationService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }
}
