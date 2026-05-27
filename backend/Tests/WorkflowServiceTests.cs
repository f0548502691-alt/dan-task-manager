using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

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
            .UseInMemoryDatabase($"WorkflowServiceTestDb-{Guid.NewGuid()}")
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
        _factory = new TaskHandlerFactory(handlers);
        var validationService = CreateValidationService();
        _service = new TaskWorkflowService(
            _context,
            CreateRuleProviders(_factory, validationService),
            new MockLogger());
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
        // Arrange & Act (1 → 2)
        var priceData = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" } });
        var result = await _service.ChangeStatusAsync(1, 2, 2, priceData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.NewStatus);
        Assert.NotNull(result.UpdatedTask);
        Assert.Equal(2, result.UpdatedTask.AssignedToUserId);
    }

    [Fact]
    public async Task ChangeStatus_ForwardMovement_Plus2_ShouldFail()
    {
        // Arrange & Act (1 → 3 - invalid jump)
        var result = await _service.ChangeStatusAsync(1, 3, 2, "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("exactly +1", result.Message);
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
        var result = await _service.ChangeStatusAsync(1, 1, 1, "{}");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.NewStatus);
    }

    [Fact]
    public async Task ChangeStatus_BackwardToCreatedStatus_ShouldSucceed()
    {
        // Arrange
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 3;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Act (3 → 1)
        var result = await _service.ChangeStatusAsync(1, 1, 1, "{}");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.NewStatus);
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

        var result = await _service.ChangeStatusAsync(1, 2, 1, "{}");

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
        var result = await _service.ChangeStatusAsync(1, 2, 2, priceData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.NewStatus);
    }

    [Fact]
    public async Task ChangeStatus_UsesConfiguredRules_InsteadOfHandlerValidation()
    {
        // Arrange - relaxed configuration for Procurement status 2 without required fields.
        var relaxedValidationService = new TaskTypeValidationService(Options.Create(new TaskTypeValidationOptions
        {
            TaskTypes = new List<TaskTypeDefinition>
            {
                new()
                {
                    TaskType = "Procurement",
                    FinalStatus = 3
                }
            }
        }));

        var relaxedService = new TaskWorkflowService(
            _context,
            CreateRuleProviders(_factory, relaxedValidationService),
            new MockLogger());

        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 1;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Act - without config this payload would fail on handler "prices" validation.
        var result = await relaxedService.ChangeStatusAsync(1, 2, 2, "{}");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.NewStatus);
    }

    [Fact]
    public async Task ChangeStatus_UnknownTaskType_ShouldFail()
    {
        // Arrange
        var unknownTask = new BaseTask
        {
            Id = 99,
            TaskType = "Unknown",
            Description = "Unknown type task",
            CurrentStatus = 0,
            AssignedToUserId = 1,
            CustomDataJson = "{}"
        };
        _context.Tasks.Add(unknownTask);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ChangeStatusAsync(99, 1, 1, "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unsupported", result.Message);
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
        var result = await _service.ChangeStatusAsync(1, 1, 1, "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("closed", result.Message);
    }

    // === Close Task Tests ===

    [Fact]
    public async Task CloseTask_WithNotes_ShouldSucceed()
    {
        // Arrange
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 3;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CloseTaskAsync(1, 2, "Completed successfully");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(99, result.NewStatus);
        Assert.NotNull(result.UpdatedTask);
        Assert.Equal(99, result.UpdatedTask.CurrentStatus);
        Assert.Equal(2, result.UpdatedTask.AssignedToUserId);
    }

    [Fact]
    public async Task CloseTask_AlreadyClosed_ShouldFail()
    {
        // Arrange
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 3;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();
        await _service.CloseTaskAsync(1, 2, "First close");
        
        // Act
        var result = await _service.CloseTaskAsync(1, 2, "Second close");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("already closed", result.Message);
    }

    [Fact]
    public async Task CloseTask_WhenNotInFinalStatus_ShouldFail()
    {
        // Act
        var result = await _service.CloseTaskAsync(1, 2, "Early close");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("only be closed from final status", result.Message);
    }

    [Fact]
    public async Task CloseTask_WithUnknownNextAssignee_ShouldFail()
    {
        // Arrange
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 3;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CloseTaskAsync(1, 999, "Final notes");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Next assignee does not exist", result.Message);
    }

    [Fact]
    public async Task CloseTask_AddsNotesAndTimestampToJson()
    {
        // Arrange
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 3;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CloseTaskAsync(1, 2, "Final notes");

        // Assert
        Assert.True(result.Success);
        var json = result.UpdatedTask!.CustomDataJson;
        Assert.Contains("finalNotes", json);
        Assert.Contains("Final notes", json);
        Assert.Contains("closedAt", json);
    }

    // === Get User Tasks Tests ===

    [Fact]
    public async Task GetUserTasks_ShouldReturnAssignedTasksIncludingClosed()
    {
        // Arrange - close seed task 1
        var task = await _context.Tasks.FindAsync(1);
        task!.CurrentStatus = 3;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();
        await _service.CloseTaskAsync(1, 1, "Closed");

        // Add another open task with a non-conflicting ID
        var task2 = new BaseTask
        {
            Id = 100,
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
        Assert.True(tasks.Count() >= 2);
        Assert.Contains(tasks, t => t.Id == 1 && t.CurrentStatus == WorkflowConstants.ClosedStatus);
        Assert.Contains(tasks, t => t.Id == 100 && t.CurrentStatus == 1);
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
        var result = await _service.ChangeStatusAsync(1, 4, 1, "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("final status", result.Message);
    }

    // === Mock Logger ===

    private class MockLogger : ILogger<TaskWorkflowService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private static ITaskTypeValidationService CreateValidationService()
    {
        var options = Options.Create(new TaskTypeValidationOptions
        {
            TaskTypes = new List<TaskTypeDefinition>
            {
                new()
                {
                    TaskType = "Procurement",
                    FinalStatus = 3,
                    StatusRules = new List<TaskStatusRuleDefinition>
                    {
                        new()
                        {
                            Status = 2,
                            Fields = new List<FieldRuleDefinition>
                            {
                                new()
                                {
                                    Field = "prices",
                                    Type = "array",
                                    ArrayLength = 2,
                                    ElementType = "string"
                                }
                            }
                        },
                        new()
                        {
                            Status = 3,
                            Fields = new List<FieldRuleDefinition>
                            {
                                new()
                                {
                                    Field = "receipt",
                                    Type = "string",
                                    Required = true
                                }
                            }
                        }
                    }
                },
                new()
                {
                    TaskType = "Development",
                    FinalStatus = 4,
                    StatusRules = new List<TaskStatusRuleDefinition>
                    {
                        new()
                        {
                            Status = 2,
                            Fields = new List<FieldRuleDefinition>
                            {
                                new()
                                {
                                    Field = "specification",
                                    Type = "string",
                                    Required = true,
                                    MinLength = 10
                                }
                            }
                        },
                        new()
                        {
                            Status = 3,
                            Fields = new List<FieldRuleDefinition>
                            {
                                new()
                                {
                                    Field = "branchName",
                                    Type = "string",
                                    Required = true,
                                    Pattern = "valid_git_branch"
                                }
                            }
                        },
                        new()
                        {
                            Status = 4,
                            Fields = new List<FieldRuleDefinition>
                            {
                                new()
                                {
                                    Field = "versionNumber",
                                    Type = "stringOrNumber",
                                    Required = true,
                                    Pattern = "semantic_version"
                                }
                            }
                        }
                    }
                }
            }
        });

        return new TaskTypeValidationService(options);
    }

    private static IEnumerable<ITaskWorkflowRuleProvider> CreateRuleProviders(
        TaskHandlerFactory handlerFactory,
        ITaskTypeValidationService validationService)
    {
        return new ITaskWorkflowRuleProvider[]
        {
            new MetadataTaskWorkflowRuleProvider(validationService),
            new HandlerTaskWorkflowRuleProvider(handlerFactory)
        };
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
            .UseInMemoryDatabase($"WorkflowIntegrationTestDb-{Guid.NewGuid()}")
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
            CreateRuleProviders(
                new TaskHandlerFactory(handlers),
                CreateValidationService()),
            new MockLogger());
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        _context.Dispose();
    }

    [Fact]
    public async Task CompleteWorkflow_Procurement()
    {
        // Create task with a non-conflicting ID
        var task = new BaseTask
        {
            Id = 200,
            TaskType = "Procurement",
            Description = "Test",
            CurrentStatus = WorkflowConstants.CreatedStatus,
            AssignedToUserId = 1,
            CustomDataJson = "{}"
        };
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        // 1 → 2
        var prices = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" } });
        var r1 = await _service.ChangeStatusAsync(task.Id, 2, 2, prices);
        Assert.True(r1.Success);

        // 2 → 3
        var receipt = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" }, receipt = "REC-001" });
        var r2 = await _service.ChangeStatusAsync(task.Id, 3, 1, receipt);
        Assert.True(r2.Success);

        // Close
        var close = await _service.CloseTaskAsync(task.Id, 2, "Done");
        Assert.True(close.Success);
        Assert.Equal(99, close.NewStatus);
    }

    private static ITaskTypeValidationService CreateValidationService()
    {
        var options = Options.Create(new TaskTypeValidationOptions
        {
            TaskTypes = new List<TaskTypeDefinition>
            {
                new()
                {
                    TaskType = "Procurement",
                    FinalStatus = 3,
                    StatusRules = new List<TaskStatusRuleDefinition>
                    {
                        new()
                        {
                            Status = 2,
                            Fields = new List<FieldRuleDefinition>
                            {
                                new()
                                {
                                    Field = "prices",
                                    Type = "array",
                                    ArrayLength = 2,
                                    ElementType = "string"
                                }
                            }
                        },
                        new()
                        {
                            Status = 3,
                            Fields = new List<FieldRuleDefinition>
                            {
                                new()
                                {
                                    Field = "receipt",
                                    Type = "string",
                                    Required = true
                                }
                            }
                        }
                    }
                },
                new()
                {
                    TaskType = "Development",
                    FinalStatus = 4
                }
            }
        });

        return new TaskTypeValidationService(options);
    }

    private static IEnumerable<ITaskWorkflowRuleProvider> CreateRuleProviders(
        TaskHandlerFactory handlerFactory,
        ITaskTypeValidationService validationService)
    {
        return new ITaskWorkflowRuleProvider[]
        {
            new MetadataTaskWorkflowRuleProvider(validationService),
            new HandlerTaskWorkflowRuleProvider(handlerFactory)
        };
    }

    private class MockLogger : ILogger<TaskWorkflowService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
