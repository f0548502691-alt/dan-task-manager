using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

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

        var handlers = new ITaskHandler[]
        {
            new AnalysisTaskHandler(),
            new DevelopmentTaskHandler(),
            new ProcurementTaskHandler(),
            new TestingTaskHandler()
        };
        var metadataService = new TaskTypeValidationService(Options.Create(new TaskTypeValidationOptions()));
        var handlerFactory = new TaskHandlerFactory(handlers);
        var taskTypeCatalog = new TaskTypeCatalogService(metadataService, handlerFactory);

        var service = new TaskApplicationService(
            context,
            new NoOpWorkflowService(),
            taskTypeCatalog,
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
            new[] { "Analysis", "Development", "Procurement", "Testing" },
            result.SupportedTaskTypes);
    }

    [Fact]
    public async Task UpdateDescriptionAsync_WhenTaskIsMutable_UpdatesDescription()
    {
        await using var context = await CreateContextAsync();
        var workflowMock = new Mock<ITaskWorkflowService>();
        workflowMock
            .Setup(service => service.EnsureTaskMutableAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkflowResult.SuccessResult(
                WorkflowConstants.CreatedStatus,
                new BaseTask { Id = 1 }));
        var service = CreateService(context, workflowMock.Object);

        var original = await context.Tasks
            .AsNoTracking()
            .SingleAsync(task => task.Id == 1);

        var updated = await service.UpdateDescriptionAsync(1, "Updated procurement request");

        Assert.True(updated);
        var persisted = await context.Tasks
            .AsNoTracking()
            .SingleAsync(task => task.Id == 1);
        Assert.Equal("Updated procurement request", persisted.Description);
        Assert.True(persisted.UpdatedAt > original.UpdatedAt);
        workflowMock.Verify(
            service => service.EnsureTaskMutableAsync(1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateDescriptionAsync_WhenWorkflowRejects_DoesNotModifyTask()
    {
        await using var context = await CreateContextAsync();
        var workflowMock = new Mock<ITaskWorkflowService>();
        workflowMock
            .Setup(service => service.EnsureTaskMutableAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkflowResult.FailureResult("Closed task is immutable"));
        var service = CreateService(context, workflowMock.Object);

        var original = await context.Tasks
            .AsNoTracking()
            .SingleAsync(task => task.Id == 1);

        var updated = await service.UpdateDescriptionAsync(1, "Should not be saved");

        Assert.False(updated);
        var persisted = await context.Tasks
            .AsNoTracking()
            .SingleAsync(task => task.Id == 1);
        Assert.Equal(original.Description, persisted.Description);
        Assert.Equal(original.UpdatedAt, persisted.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_WhenTaskIsMutable_RemovesTask()
    {
        await using var context = await CreateContextAsync();
        var workflowMock = new Mock<ITaskWorkflowService>();
        workflowMock
            .Setup(service => service.EnsureTaskMutableAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkflowResult.SuccessResult(
                WorkflowConstants.CreatedStatus,
                new BaseTask { Id = 1 }));
        var service = CreateService(context, workflowMock.Object);

        var deleted = await service.DeleteAsync(1);

        Assert.True(deleted);
        Assert.Null(await context.Tasks.FindAsync(1));
        workflowMock.Verify(
            service => service.EnsureTaskMutableAsync(1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenWorkflowRejects_LeavesTaskInPlace()
    {
        await using var context = await CreateContextAsync();
        var workflowMock = new Mock<ITaskWorkflowService>();
        workflowMock
            .Setup(service => service.EnsureTaskMutableAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkflowResult.FailureResult("Closed task is immutable"));
        var service = CreateService(context, workflowMock.Object);

        var deleted = await service.DeleteAsync(1);

        Assert.False(deleted);
        Assert.NotNull(await context.Tasks.FindAsync(1));
    }

    private static async Task<ApplicationDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TaskApplicationServiceTests-{Guid.NewGuid()}")
            .Options;
        var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static TaskApplicationService CreateService(
        ApplicationDbContext context,
        ITaskWorkflowService workflowService)
    {
        return new TaskApplicationService(
            context,
            workflowService,
            Mock.Of<ITaskTypeCatalog>(),
            new MockLogger());
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
