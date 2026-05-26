using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

        context.Users.Add(new AppUser
        {
            Id = 1,
            Name = "Test User",
            Email = "test@example.com"
        });
        await context.SaveChangesAsync();

        var handlers = new ITaskHandler[]
        {
            new AnalysisTaskHandler(),
            new DevelopmentTaskHandler(),
            new ProcurementTaskHandler(),
            new TestingTaskHandler()
        };
        var metadataService = new TaskTypeValidationService(Options.Create(new TaskTypeValidationOptions()));

        var service = new TaskApplicationService(
            context,
            new NoOpWorkflowService(),
            new TaskHandlerFactory(handlers),
            metadataService,
            metadataService,
            new MockLogger());

        var result = await service.CreateAsync(
            new TaskCreateCommand(
                "UnknownType",
                "Unsupported task type",
                1,
                "{}"));

        Assert.False(result.Success);
        Assert.Contains("לא נתמך", result.Message);
        Assert.Equal(
            new[] { "Analysis", "Development", "Procurement", "Testing" },
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
