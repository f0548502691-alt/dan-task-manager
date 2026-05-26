using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DanTaskManager.Tests;

public class TaskApplicationServiceTests : IAsyncLifetime
{
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private ApplicationDbContext _context = null!;
    private TaskApplicationService _service = null!;

    public TaskApplicationServiceTests()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"{nameof(TaskApplicationServiceTests)}-{Guid.NewGuid()}")
            .Options;
    }

    public async Task InitializeAsync()
    {
        _context = new ApplicationDbContext(_options);
        await _context.Database.EnsureCreatedAsync();

        _context.Users.AddRange(
            new AppUser { Id = 10, Name = "Owner", Email = "owner@example.com" },
            new AppUser { Id = 11, Name = "Reviewer", Email = "reviewer@example.com" });
        await _context.SaveChangesAsync();

        var handlerFactory = new TaskHandlerFactory(new ITaskHandler[]
        {
            new ProcurementTaskHandler(),
            new DevelopmentTaskHandler()
        });
        var workflowService = new TaskWorkflowService(
            _context,
            handlerFactory,
            NullLogger<TaskWorkflowService>.Instance);

        _service = new TaskApplicationService(
            _context,
            workflowService,
            handlerFactory,
            NullLogger<TaskApplicationService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_WithSupportedTaskType_ShouldStartAtCreatedStatus()
    {
        var result = await _service.CreateAsync(new TaskCreateCommand(
            "Procurement",
            "Buy monitors",
            10,
            "{\"budget\":5000}"));

        Assert.True(result.Success);
        Assert.NotNull(result.CreatedTask);
        Assert.Equal(WorkflowConstants.CreatedStatus, result.CreatedTask.CurrentStatus);
        Assert.Equal("{\"budget\":5000}", result.CreatedTask.CustomDataJson);
    }

    [Fact]
    public async Task CreateAsync_WithUnsupportedTaskType_ShouldFailWithoutPersistingTask()
    {
        var result = await _service.CreateAsync(new TaskCreateCommand(
            "Analysis",
            "Legacy task type",
            10,
            "{}"));

        Assert.False(result.Success);
        Assert.Contains("לא נתמך", result.Message);
        Assert.False(await _context.Tasks.AnyAsync(t => t.Description == "Legacy task type"));
    }

    [Fact]
    public async Task UpdateDescriptionAsync_WhenTaskIsClosed_ShouldReturnFalseAndKeepDescription()
    {
        var task = await AddTaskAsync(WorkflowConstants.ClosedStatus, "Original description");

        var updated = await _service.UpdateDescriptionAsync(task.Id, "Changed description");

        var persistedTask = await _context.Tasks.FindAsync(task.Id);
        Assert.False(updated);
        Assert.Equal("Original description", persistedTask!.Description);
    }

    [Fact]
    public async Task DeleteAsync_WhenTaskIsClosed_ShouldReturnFalseAndKeepTask()
    {
        var task = await AddTaskAsync(WorkflowConstants.ClosedStatus, "Closed task");

        var deleted = await _service.DeleteAsync(task.Id);

        Assert.False(deleted);
        Assert.NotNull(await _context.Tasks.FindAsync(task.Id));
    }

    private async Task<BaseTask> AddTaskAsync(int currentStatus, string description)
    {
        var task = new BaseTask
        {
            TaskType = "Procurement",
            Description = description,
            CurrentStatus = currentStatus,
            AssignedToUserId = 10,
            CustomDataJson = "{}"
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }
}
