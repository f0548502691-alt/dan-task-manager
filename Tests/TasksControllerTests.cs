using DanTaskManager.Controllers;
using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DanTaskManager.Tests;

public class TasksControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TasksController _controller;

    public TasksControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TasksControllerTests-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        var factory = new TaskHandlerFactory(new ITaskHandler[]
        {
            new ProcurementTaskHandler(),
            new DevelopmentTaskHandler()
        });

        _controller = new TasksController(
            _context,
            factory,
            new ThrowingWorkflowService(),
            new MockLogger<TasksController>());
    }

    [Fact]
    public async Task CreateTask_WithUnsupportedTaskType_ReturnsSupportedTypesAndDoesNotPersist()
    {
        var request = new CreateTaskRequest
        {
            TaskType = "Analysis",
            Description = "Analyze the risky flow",
            AssignedToUserId = 1,
            CustomDataJson = "{}"
        };

        var result = await _controller.CreateTask(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payloadJson = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("TaskType", payloadJson);
        Assert.Contains("Analysis", payloadJson);
        Assert.Contains("Development", payloadJson);
        Assert.Contains("Procurement", payloadJson);
        Assert.Empty(await _context.Tasks.Where(task => task.TaskType == "Analysis").ToListAsync());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private class ThrowingWorkflowService : ITaskWorkflowService
    {
        public Task<WorkflowResult> ChangeStatusAsync(int taskId, int newStatus, string newDataJson) =>
            throw new NotSupportedException();

        public Task<WorkflowResult> CloseTaskAsync(int taskId, string finalNotes) =>
            throw new NotSupportedException();

        public Task<IEnumerable<BaseTask>> GetUserTasksAsync(int userId) =>
            throw new NotSupportedException();

        public Task<BaseTask?> GetTaskAsync(int taskId) =>
            throw new NotSupportedException();
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
