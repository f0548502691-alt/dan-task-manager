using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DanTaskManager.Tests;

/// <summary>
/// בדיקות יחידתיות עבור TaskWorkflowService
/// </summary>
public class TaskWorkflowServiceTests : IAsyncLifetime
{
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private ApplicationDbContext _context = null!;
    private TaskHandlerFactory _factory = null!;
    private TaskWorkflowService _service = null!;

    public TaskWorkflowServiceTests()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("WorkflowServiceTestDb")
            .Options;
    }

    public async Task InitializeAsync()
    {
        _context = new ApplicationDbContext(_options);
        await _context.Database.EnsureCreatedAsync();

        // Setup handlers
        var handlers = new ITaskHandler[]
        {
            new ProcurementTaskHandler(),
            new DevelopmentTaskHandler()
        };
        _factory = new TaskHandlerFactory(handlers);
        _service = new TaskWorkflowService(_context, _factory, new MockLogger());

        // Seed data
        var user = new AppUser { Id = 1, Name = "Test User", Email = "test@test.com" };
        _context.Users.Add(user);

        var task = new BaseTask
        {
            Id = 1,
            TaskType = "Procurement",
            Description = "Test procurement",
            CurrentStatus = 0,
            AssignedToUserId = 1,
            CustomDataJson = "{}"
        };
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        _context.Dispose();
    }

    // === Forward Movement Tests ===

    [Fact]
    public async Task ChangeStatus_ForwardMovement_Plus1_ShouldSucceed()
    {
        // Arrange & Act (0 → 1)
        var result = await _service.ChangeStatusAsync(1, 1, "{}");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.NewStatus);
        Assert.NotNull(result.UpdatedTask);
    }

    [Fact]
    public async Task ChangeStatus_ForwardMovement_Plus2_ShouldFail()
    {
        // Arrange & Act (0 → 2 - invalid jump)
        var result = await _service.ChangeStatusAsync(1, 2, "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("בדיוק ב-1 סטטוס", result.Message);
    }

    // === Backward Movement Tests ===

    [Fact]
    public async Task ChangeStatus_BackwardMovement_ShouldSucceed()
    {
        // Arrange
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 2;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Act (2 → 1)
        var result = await _service.ChangeStatusAsync(1, 1, "{}");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.NewStatus);
    }

    [Fact]
    public async Task ChangeStatus_BackwardToMuchLowerStatus_ShouldSucceed()
    {
        // Arrange
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 3;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Act (3 → 0)
        var result = await _service.ChangeStatusAsync(1, 0, "{}");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.NewStatus);
    }

    // === Validation Tests ===

    [Fact]
    public async Task ChangeStatus_WithoutValidation_ShouldFail()
    {
        // Arrange & Act
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 1;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        var result = await _service.ChangeStatusAsync(1, 2, "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("prices", result.Message);
    }

    [Fact]
    public async Task ChangeStatus_WithValidHandlerData_ShouldSucceed()
    {
        // Arrange
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 1;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        var priceData = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" } });

        // Act
        var result = await _service.ChangeStatusAsync(1, 2, priceData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.NewStatus);
    }

    // === Closed Status Tests ===

    [Fact]
    public async Task ChangeStatus_WhenTaskClosed_ShouldFail()
    {
        // Arrange
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 99; // Closed status
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ChangeStatusAsync(1, 0, "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("סגורה", result.Message);
    }

    // === Close Task Tests ===

    [Fact]
    public async Task CloseTask_WithNotes_ShouldSucceed()
    {
        // Act
        var result = await _service.CloseTaskAsync(1, "Completed successfully");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(99, result.NewStatus);
        Assert.NotNull(result.UpdatedTask);
        Assert.Equal(99, result.UpdatedTask.CurrentStatus);
    }

    [Fact]
    public async Task CloseTask_AlreadyClosed_ShouldFail()
    {
        // Arrange
        await _service.CloseTaskAsync(1, "First close");
        
        // Act
        var result = await _service.CloseTaskAsync(1, "Second close");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("כבר סגורה", result.Message);
    }

    [Fact]
    public async Task CloseTask_AddsNotesAndTimestampToJson()
    {
        // Act
        var result = await _service.CloseTaskAsync(1, "Final notes");

        // Assert
        Assert.True(result.Success);
        var json = result.UpdatedTask!.CustomDataJson;
        Assert.Contains("finalNotes", json);
        Assert.Contains("Final notes", json);
        Assert.Contains("closedAt", json);
    }

    // === Get User Tasks Tests ===

    [Fact]
    public async Task GetUserTasks_ShouldExcludeClosedTasks()
    {
        // Arrange
        await _service.CloseTaskAsync(1, "Closed");

        // Add another open task
        var task2 = new BaseTask
        {
            Id = 2,
            TaskType = "Development",
            Description = "Dev task",
            CurrentStatus = 1,
            AssignedToUserId = 1,
            CustomDataJson = "{}"
        };
        _context.Tasks.Add(task2);
        await _context.SaveChangesAsync();

        // Act
        var tasks = await _service.GetUserTasksAsync(1);

        // Assert
        Assert.Single(tasks);
        Assert.Equal(2, tasks.First().Id); // Only open task
    }

    [Fact]
    public async Task GetUserTasks_EmptyForNonExistentUser()
    {
        // Act
        var tasks = await _service.GetUserTasksAsync(999);

        // Assert
        Assert.Empty(tasks);
    }

    // === Get Task Tests ===

    [Fact]
    public async Task GetTask_WithValidId_ShouldReturnTask()
    {
        // Act
        var task = await _service.GetTaskAsync(1);

        // Assert
        Assert.NotNull(task);
        Assert.Equal(1, task.Id);
    }

    [Fact]
    public async Task GetTask_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var task = await _service.GetTaskAsync(999);

        // Assert
        Assert.Null(task);
    }

    // === Final Status Tests ===

    [Fact]
    public async Task ChangeStatus_BeyondFinalStatus_ShouldFail()
    {
        // Arrange
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 3; // Procurement final status
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ChangeStatusAsync(1, 4, "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("סטטוס סופי", result.Message);
    }

    // === Mock Logger ===

    private class MockLogger : ILogger<TaskWorkflowService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}

/// <summary>
/// Integration tests for complete workflows
/// </summary>
public class TaskWorkflowIntegrationTests : IAsyncLifetime
{
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private ApplicationDbContext _context = null!;
    private ITaskWorkflowService _service = null!;

    public TaskWorkflowIntegrationTests()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("WorkflowIntegrationTestDb")
            .Options;
    }

    public async Task InitializeAsync()
    {
        _context = new ApplicationDbContext(_options);
        await _context.Database.EnsureCreatedAsync();

        var handlers = new ITaskHandler[]
        {
            new ProcurementTaskHandler(),
            new DevelopmentTaskHandler()
        };
        _service = new TaskWorkflowService(
            _context,
            new TaskHandlerFactory(handlers),
            new MockLogger());

        var user = new AppUser { Id = 1, Name = "Test", Email = "test@test.com" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        _context.Dispose();
    }

    [Fact]
    public async Task CompleteWorkflow_Procurement()
    {
        // Create task
        var task = new BaseTask
        {
            TaskType = "Procurement",
            Description = "Test",
            CurrentStatus = 0,
            AssignedToUserId = 1,
            CustomDataJson = "{}"
        };
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        // 0 → 1
        var r1 = await _service.ChangeStatusAsync(task.Id, 1, "{}");
        Assert.True(r1.Success);

        // 1 → 2
        var prices = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" } });
        var r2 = await _service.ChangeStatusAsync(task.Id, 2, prices);
        Assert.True(r2.Success);

        // 2 → 3
        var receipt = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" }, receipt = "REC-001" });
        var r3 = await _service.ChangeStatusAsync(task.Id, 3, receipt);
        Assert.True(r3.Success);

        // Close
        var close = await _service.CloseTaskAsync(task.Id, "Done");
        Assert.True(close.Success);
        Assert.Equal(99, close.NewStatus);
    }

    private class MockLogger : ILogger<TaskWorkflowService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
