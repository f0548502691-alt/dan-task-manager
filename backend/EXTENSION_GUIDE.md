# 🔧 Extension Guide - Adding New Features to Dan Task Manager

## 📖 Table of Contents

1. [Adding New Handler Type](#adding-new-handler-type)
2. [Adding New Endpoint](#adding-new-endpoint)
3. [Adding Validation Rule](#adding-validation-rule)
4. [Common Extension Scenarios](#common-extension-scenarios)
5. [Testing Extensions](#testing-extensions)

---

## 🎯 Adding New Handler Type

### Step 1: Create Handler Class

```csharp
// Domain/Handlers/QATaskHandler.cs
using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// QA task handler with specific validation
/// </summary>
public class QATaskHandler : StatusValidationTaskHandlerBase
{
    public QATaskHandler()
        : base(new Dictionary<int, Func<string, ValidationResult>>
        {
            [2] = ValidateStatusTwo,
            [3] = ValidateStatusThree
        })
    {
    }

    public string TaskType => "QA";

    public int FinalStatus => 3;

    /// <summary>
    /// Validate Status 2 (Setup)
    /// Requires: testEnvironment, testCases
    /// </summary>
    private static ValidationResult ValidateStatusTwo(string dataJson)
    {
        try
        {
            using var json = JsonDocument.Parse(dataJson);
            var root = json.RootElement;

            // Check testEnvironment
            if (!root.TryGetProperty("testEnvironment", out var env))
                return ValidationResult.Failure("'testEnvironment' field is required");

            if (env.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(env.GetString()))
                return ValidationResult.Failure("'testEnvironment' must be a non-empty string");

            // Check testCases array
            if (!root.TryGetProperty("testCases", out var testCases))
                return ValidationResult.Failure("'testCases' array is required");

            if (testCases.ValueKind != JsonValueKind.Array)
                return ValidationResult.Failure("'testCases' must be an array");

            var count = testCases.GetArrayLength();
            if (count < 1)
                return ValidationResult.Failure("'testCases' must contain at least 1 test case");

            return ValidationResult.Success();
        }
        catch (JsonException)
        {
            return ValidationResult.Failure("Invalid JSON format");
        }
    }

    /// <summary>
    /// Validate Status 3 (Testing - Final)
    /// Requires: testResults, bugsFound
    /// </summary>
    private static ValidationResult ValidateStatusThree(string dataJson)
    {
        try
        {
            using var json = JsonDocument.Parse(dataJson);
            var root = json.RootElement;

            // Check testResults
            if (!root.TryGetProperty("testResults", out var results))
                return ValidationResult.Failure("'testResults' field is required");

            if (results.ValueKind != JsonValueKind.String)
                return ValidationResult.Failure("'testResults' must be a string");

            var resultsValue = results.GetString();
            if (string.IsNullOrEmpty(resultsValue) || !new[] { "PASSED", "FAILED", "PARTIAL" }.Contains(resultsValue))
                return ValidationResult.Failure("'testResults' must be PASSED, FAILED, or PARTIAL");

            // Check bugsFound (optional but if present, must be valid)
            if (root.TryGetProperty("bugsFound", out var bugs))
            {
                if (bugs.ValueKind != JsonValueKind.Array)
                    return ValidationResult.Failure("'bugsFound' must be an array if provided");
            }

            return ValidationResult.Success();
        }
        catch (JsonException)
        {
            return ValidationResult.Failure("Invalid JSON format");
        }
    }
}
```

### Step 2: Let DI Discover the Handler

Handlers are registered by convention from `Program.cs`:

```csharp
builder.Services.AddTaskHandlersFromAssembly(typeof(ITaskHandler).Assembly);
```

Requirements for automatic registration:
- The class must implement `ITaskHandler` (directly or via `StatusValidationTaskHandlerBase`).
- The class must be concrete and non-abstract.
- The class must be in the `DanTaskManager.Domain.Handlers` namespace.

No `Program.cs` change is needed when a new handler follows those constraints.

### Step 3: Write Tests

```csharp
// Tests/QAHandlerTests.cs
using DanTaskManager.Domain.Handlers;
using System.Text.Json;

namespace DanTaskManager.Tests;

public class QAHandlerTests
{
    private readonly QATaskHandler _handler = new();

    [Fact]
    public void ValidateStatusTwo_WithRequiredFields_ShouldPass()
    {
        // Arrange
        var data = JsonSerializer.Serialize(new
        {
            testEnvironment = "Staging",
            testCases = new[] { "TC001", "TC002", "TC003" }
        });

        // Act
        var result = _handler.ValidateStatusChange("", 1, 2, data);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateStatusTwo_MissingTestEnvironment_ShouldFail()
    {
        // Arrange
        var data = JsonSerializer.Serialize(new
        {
            testCases = new[] { "TC001" }
        });

        // Act
        var result = _handler.ValidateStatusChange("", 1, 2, data);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("testEnvironment", result.Message);
    }

    [Fact]
    public void ValidateStatusThree_WithAllFields_ShouldPass()
    {
        // Arrange
        var data = JsonSerializer.Serialize(new
        {
            testResults = "PASSED",
            bugsFound = new[] { "BUG001", "BUG002" }
        });

        // Act
        var result = _handler.ValidateStatusChange("", 2, 3, data);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateStatusThree_InvalidTestResults_ShouldFail()
    {
        // Arrange
        var data = JsonSerializer.Serialize(new
        {
            testResults = "UNKNOWN"
        });

        // Act
        var result = _handler.ValidateStatusChange("", 2, 3, data);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("PASSED, FAILED, or PARTIAL", result.Message);
    }
}
```

### Step 4: Test It

```bash
# Run tests
dotnet test

# Run application
dotnet run

# Test with API
curl -X POST http://localhost:5000/api/tasks \
  -H "Content-Type: application/json" \
  -d '{
    "taskType": "QA",
    "description": "Test new feature",
    "assignedToUserId": 1
  }'
```

---

## ➕ Adding New Endpoint

### Example: Get Task Statistics

#### Step 1: Add Method to the Application Service Interface

```csharp
// Services/ITaskApplicationService.cs
public interface ITaskApplicationService
{
    // ... existing methods ...
    
    /// <summary>
    /// Get task statistics for a user
    /// </summary>
    Task<TaskStatistics> GetUserStatisticsAsync(
        int userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Task statistics
/// </summary>
public class TaskStatistics
{
    public int UserId { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public Dictionary<string, int> TasksByType { get; set; } = new();
}
```

#### Step 2: Implement in the Application Service

```csharp
// Services/TaskApplicationService.cs
public async Task<TaskStatistics> GetUserStatisticsAsync(
    int userId,
    CancellationToken cancellationToken = default)
{
    var tasks = await _context.Tasks
        .AsNoTracking()
        .Where(t => t.AssignedToUserId == userId)
        .ToListAsync(cancellationToken);

    var stats = new TaskStatistics
    {
        UserId = userId,
        TotalTasks = tasks.Count,
        CompletedTasks = tasks.Count(t => t.CurrentStatus == 99),
        InProgressTasks = tasks.Count(t => t.CurrentStatus > 0 && t.CurrentStatus < 99),
        TasksByType = tasks
            .GroupBy(t => t.TaskType)
            .ToDictionary(g => g.Key, g => g.Count())
    };

    return stats;
}
```

#### Step 3: Add Controller Endpoint

```csharp
// Controllers/TasksController.cs
/// <summary>
/// Get task statistics for user
/// </summary>
[HttpGet("user/{userId}/statistics")]
public async Task<ActionResult<TaskStatistics>> GetUserStatistics(int userId)
{
    var stats = await _taskService.GetUserStatisticsAsync(userId, HttpContext.RequestAborted);
    
    if (stats == null || stats.TotalTasks == 0)
        return NotFound(new { error = "No tasks found for user" });
    
    return Ok(stats);
}
```

#### Step 4: Test It

```bash
# Test endpoint
curl http://localhost:5000/api/tasks/user/1/statistics

# Response:
{
  "userId": 1,
  "totalTasks": 5,
  "completedTasks": 2,
  "inProgressTasks": 3,
  "tasksByType": {
    "Procurement": 2,
    "Development": 3
  }
}
```

---

## 🔍 Adding Validation Rule

### Example: Prevent Duplicate Task Types for User

#### Step 1: Add Validation to Service

```csharp
// Services/TaskWorkflowService.cs
private async Task<ValidationResult> ValidateUniqueTaskTypeAsync(
    int userId,
    string taskType,
    CancellationToken cancellationToken)
{
    var existingTask = await _context.Tasks
        .AsNoTracking()
        .FirstOrDefaultAsync(t =>
            t.AssignedToUserId == userId &&
            t.TaskType == taskType &&
            t.CurrentStatus < 99,
            cancellationToken); // Not closed

    if (existingTask != null)
        return ValidationResult.Failure($"User already has an active {taskType} task");

    return ValidationResult.Success();
}
```

#### Step 2: Use in Workflow

```csharp
// In ChangeStatusAsync or appropriate place
public async Task<WorkflowResult> ChangeStatusAsync(
    int taskId,
    int newStatus,
    string newDataJson,
    CancellationToken cancellationToken = default)
{
    var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
    
    // ... existing validations ...
    
    // New: Check unique constraint
    var uniqueValidation = await ValidateUniqueTaskTypeAsync(
        task.AssignedToUserId,
        task.TaskType,
        cancellationToken);
    if (!uniqueValidation.IsValid)
        return WorkflowResult.FailureResult(uniqueValidation.Message);
    
    // ... rest of implementation ...
}
```

#### Step 3: Write Test

```csharp
[Fact]
public async Task ChangeStatus_WithDuplicateTaskType_ShouldFail()
{
    // Arrange: Two Procurement tasks for same user
    var task1 = new BaseTask { TaskType = "Procurement", AssignedToUserId = 1, CurrentStatus = 0 };
    var task2 = new BaseTask { TaskType = "Procurement", AssignedToUserId = 1, CurrentStatus = 0 };
    
    _context.Tasks.Add(task1);
    _context.Tasks.Add(task2);
    await _context.SaveChangesAsync();

    // Act: Try to move second task to Status 1
    var result = await _service.ChangeStatusAsync(task2.Id, 1, "{}");

    // Assert
    Assert.False(result.Success);
    Assert.Contains("already has an active", result.Message);
}
```

---

## 📋 Common Extension Scenarios

### Scenario 1: Add Task Priority Levels

```csharp
// Add to BaseTask
public enum TaskPriority { Low = 1, Medium = 2, High = 3 }
public TaskPriority Priority { get; set; }

// Migration
ALTER TABLE Tasks ADD Priority INT DEFAULT 2;

// Use in sorting
var tasks = await _context.Tasks
    .Where(t => t.AssignedToUserId == userId)
    .OrderByDescending(t => t.Priority)
    .ThenByDescending(t => t.CreatedAt)
    .ToListAsync();
```

### Scenario 2: Add Task Comments/Notes

```csharp
// Domain/TaskComment.cs
public class TaskComment
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public int UserId { get; set; }
    public string Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public BaseTask? Task { get; set; }
    public AppUser? User { get; set; }
}

// Add to DbContext
public DbSet<TaskComment> Comments { get; set; }

// Add endpoint
[HttpPost("{taskId}/comments")]
public async Task<IActionResult> AddComment(int taskId, [FromBody] string comment)
{
    // Implementation
}
```

### Scenario 3: Add Approval Workflow

```csharp
// New handler
public class ApprovalTaskHandler : ITaskHandler
{
    public string TaskType => "Approval";
    public int FinalStatus => 2;
    
    // Validates that approver comments are present
    private ValidationResult ValidateStatusTwo(string dataJson)
    {
        var json = JsonDocument.Parse(dataJson);
        var root = json.RootElement;
        
        if (!root.TryGetProperty("approverComments", out var comments))
            return ValidationResult.Failure("Approver comments required");
            
        return ValidationResult.Success();
    }
}
```

### Scenario 4: Add Deadline Tracking

```csharp
// Add to BaseTask
public DateTime? Deadline { get; set; }

// Validation
private ValidationResult ValidateDeadline(BaseTask task)
{
    if (task.Deadline.HasValue && task.Deadline < DateTime.UtcNow)
        return ValidationResult.Failure("Task deadline has passed");
    
    return ValidationResult.Success();
}

// Status warning
if (task.Deadline.HasValue && task.Deadline < DateTime.UtcNow.AddDays(1))
    // Log warning or notify user
}
```

---

## 🧪 Testing Extensions

### Unit Test Template

```csharp
public class ExtensionFeatureTests : IAsyncLifetime
{
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private ApplicationDbContext _context = null!;
    private ITaskApplicationService _service = null!;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;
        
        _context = new ApplicationDbContext(_options);
        await _context.Database.EnsureCreatedAsync();
        
        var handlers = new ITaskHandler[] { new YourNewHandler() };
        var workflowService = new TaskWorkflowService(
            _context,
            new TaskHandlerFactory(handlers),
            new MockLogger());
        _service = new TaskApplicationService(
            _context,
            workflowService,
            new TaskHandlerFactory(handlers),
            new MockLogger());
    }

    [Fact]
    public async Task YourNewFeature_WithValidInput_ShouldSucceed()
    {
        // Arrange
        var task = new BaseTask { /* ... */ };
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.YourNewMethod(/* ... */);

        // Assert
        Assert.True(result.Success);
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        _context.Dispose();
    }
}
```

### Integration Test Template

```csharp
// Test the full flow from API to database
[Fact]
public async Task FullWorkflow_WithNewFeature_ShouldPass()
{
    // 1. Create task
    // 2. Verify initial state
    // 3. Perform operation
    // 4. Verify side effects
    // 5. Clean up
}
```

---

## 📚 Best Practices for Extensions

✅ **Follow SOLID Principles**
- Single Responsibility: One handler per task type
- Open/Closed: Extend, don't modify existing code
- Liskov: Handlers follow ITaskHandler contract
- Interface Segregation: Only implement needed methods
- Dependency Inversion: Depend on abstractions

✅ **Maintain Consistency**
- Use same naming conventions
- Follow existing patterns
- Write XML documentation
- Add appropriate logging

✅ **Test Thoroughly**
- Unit tests for new logic
- Integration tests for workflows
- Edge cases
- Error scenarios

✅ **Document Changes**
- Update README if significant
- Add examples
- Document breaking changes
- Update architecture diagrams

---

## 🔗 Related Documentation

- Architecture: [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md)
- Workflows: [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md)
- Best Practices: [BEST_PRACTICES.md](BEST_PRACTICES.md)
- Examples: [WORKFLOW_EXAMPLES.cs](WORKFLOW_EXAMPLES.cs)

---

## 🎓 Summary

To extend the system:

1. **New Handler**: Extend `StatusValidationTaskHandlerBase` under `Domain/Handlers`
2. **New Endpoint**: Add to an application-service interface, implement in service, add a thin controller action
3. **New Validation**: Add validation method, integrate into workflow
4. **New Datatype**: Add handler with appropriate validation

All extensions follow the same patterns and principles as the existing code.

**Happy extending! 🚀**
