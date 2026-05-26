# 🔧 Extension Guide - Adding New Features to Dan Task Manager

## 📖 Table of Contents

1. [Adding or Changing Task-Type Custom Fields](#adding-or-changing-task-type-custom-fields)
2. [Adding New Handler Type](#adding-new-handler-type)
3. [Adding New Endpoint](#adding-new-endpoint)
4. [Adding Validation Rule](#adding-validation-rule)
5. [Common Extension Scenarios](#common-extension-scenarios)
6. [Testing Extensions](#testing-extensions)

---

## ⚙️ Adding or Changing Task-Type Custom Fields

Use configuration first when a task type only needs `CustomDataJson` field requirements by status. The active source is `TaskTypeValidation` in `backend/appsettings.json`, loaded by `TaskTypeValidationService` and used by `TaskWorkflowService`.

### When to Use Config vs. a Handler

| Need | Use |
|------|-----|
| Require fields for a status | `TaskTypeValidation` config |
| Check JSON primitive types, arrays, string lengths, array sizes, or regex patterns | `TaskTypeValidation` config |
| Set the final status for a task type | `TaskTypeValidation` config |
| Validate against database state, call external services, or use custom branching logic | `ITaskHandler` fallback or a service change |

If a task type appears in `TaskTypeValidation`, config validation is authoritative and the handler validation is skipped for that type.

### Step 1: Add or Update `appsettings.json`

```json
{
  "TaskTypeValidation": {
    "TaskTypes": [
      {
        "TaskType": "QA",
        "FinalStatus": 3,
        "StatusRules": [
          {
            "Status": 2,
            "Fields": [
              {
                "Field": "testEnvironment",
                "Type": "string",
                "Required": true,
                "MinLength": 3
              },
              {
                "Field": "testCases",
                "Type": "array",
                "MinItems": 1,
                "ElementType": "string"
              }
            ]
          },
          {
            "Status": 3,
            "Fields": [
              {
                "Field": "releaseVersion",
                "Type": "stringOrNumber",
                "Required": true,
                "Pattern": "semantic_version"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

### Step 2: Know When Rules Run

Rules run only during `POST /api/tasks/{id}/change-status` after the generic workflow checks pass:

1. The task exists and is not closed (`CurrentStatus != 99`).
2. `newDataJson` is valid JSON.
3. Forward moves advance exactly one status; backward moves can return to any lower status.
4. The requested status does not move beyond the configured final status.
5. The fields for the requested `Status` are validated against `newDataJson`.

Task creation validates the assignee, normalizes `customDataJson`, and logs a warning for unknown task types. It does not enforce status-specific custom fields.

### Supported Field Rule Properties

| Property | Description |
|----------|-------------|
| `Field` | JSON property name to validate. Empty names fail validation. |
| `Type` | `string`, `number`, `array`, `object`, `boolean`, or `stringOrNumber`. Empty/unknown types are treated as any JSON type. |
| `Required` | Defaults to `true`. Optional missing fields pass validation. |
| `MinLength`, `MaxLength` | String length limits. |
| `ArrayLength` | Exact array item count. |
| `MinItems`, `MaxItems` | Minimum/maximum array item count. |
| `ElementType` | Type required for every array item. |
| `Pattern` | `valid_git_branch`, `semantic_version`, or a regular expression. |

Built-in pattern constraints:
- `valid_git_branch`: rejects blank names, `//`, names ending in `/` or `.`, and spaces.
- `semantic_version`: accepts digit groups separated by dots, such as `1`, `1.0`, or `1.0.0`.

### Step 3: Add Focused Tests

Construct `TaskTypeValidationService` with options in tests, then pass it to `TaskWorkflowService`:

```csharp
var validationService = new TaskTypeValidationService(Options.Create(new TaskTypeValidationOptions
{
    TaskTypes = new List<TaskTypeDefinition>
    {
        new()
        {
            TaskType = "QA",
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
                            Field = "testCases",
                            Type = "array",
                            MinItems = 1,
                            ElementType = "string"
                        }
                    }
                }
            }
        }
    }
}));

var service = new TaskWorkflowService(
    context,
    handlerFactory,
    validationService,
    logger);
```

Cover at least:
- A valid status change with the required fields.
- A missing required field.
- A type mismatch or constraint failure.
- Final-status behavior if `FinalStatus` changes.

### Constraints and Pitfalls

- `TaskType` lookup is case-insensitive.
- Duplicate task-type entries are grouped case-insensitively; the last definition wins.
- A status with no `Fields` entry has no custom field validation.
- Configured task types skip handler validation, so keep config in sync with any existing handler expectations.
- Backward moves still replace `CustomDataJson` with the submitted payload.

## 🎯 Adding New Handler Type

Handlers remain useful for task types whose validation cannot be expressed with `TaskTypeValidation` alone.

### Step 1: Create Handler Class

```csharp
// Domain/Handlers/QATaskHandler.cs
using DanTaskManager.Domain;
using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// QA task handler with specific validation
/// </summary>
public class QATaskHandler : ITaskHandler
{
    /// <summary>
    /// Handler identifier
    /// </summary>
    public string TaskType => "QA";

    /// <summary>
    /// Final status for QA tasks
    /// </summary>
    public int FinalStatus => 3;

    /// <summary>
    /// Validate status transitions for QA tasks
    /// </summary>
    public ValidationResult ValidateStatusChange(
        string currentDataJson,
        int currentStatus,
        int nextStatus,
        string newDataJson)
    {
        // Status 1 → 2: Setup
        if (nextStatus == 2)
            return ValidateStatusTwo(newDataJson);

        // Status 2 → 3: Testing
        if (nextStatus == 3)
            return ValidateStatusThree(newDataJson);

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate Status 2 (Setup)
    /// Requires: testEnvironment, testCases
    /// </summary>
    private ValidationResult ValidateStatusTwo(string dataJson)
    {
        try
        {
            var json = JsonDocument.Parse(dataJson);
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
    private ValidationResult ValidateStatusThree(string dataJson)
    {
        try
        {
            var json = JsonDocument.Parse(dataJson);
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

### Step 2: Make Handler Discoverable

```csharp
// Program.cs
builder.Services.AddTaskHandlersFromAssembly(typeof(ITaskHandler).Assembly);
```

Handlers in the `DanTaskManager.Domain.Handlers` namespace are auto-registered at startup by `AddTaskHandlersFromAssembly`. Keep the class concrete, non-abstract, and implementing `ITaskHandler`.

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

#### Step 1: Add Method to Service Interface

```csharp
// Services/ITaskWorkflowService.cs
public interface ITaskWorkflowService
{
    // ... existing methods ...
    
    /// <summary>
    /// Get task statistics for a user
    /// </summary>
    Task<TaskStatistics> GetUserStatisticsAsync(int userId);
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

#### Step 2: Implement in Service

```csharp
// Services/TaskWorkflowService.cs
public async Task<TaskStatistics> GetUserStatisticsAsync(int userId)
{
    var tasks = await _context.Tasks
        .Where(t => t.AssignedToUserId == userId)
        .ToListAsync();

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
    var stats = await _workflowService.GetUserStatisticsAsync(userId);
    
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
private async Task<ValidationResult> ValidateUniqueTaskTypeAsync(int userId, string taskType)
{
    var existingTask = await _context.Tasks
        .FirstOrDefaultAsync(t =>
            t.AssignedToUserId == userId &&
            t.TaskType == taskType &&
            t.CurrentStatus < 99); // Not closed

    if (existingTask != null)
        return ValidationResult.Failure($"User already has an active {taskType} task");

    return ValidationResult.Success();
}
```

#### Step 2: Use in Workflow

```csharp
// In ChangeStatusAsync or appropriate place
public async Task<WorkflowResult> ChangeStatusAsync(int taskId, int newStatus, string newDataJson)
{
    var task = await _context.Tasks.FindAsync(taskId);
    
    // ... existing validations ...
    
    // New: Check unique constraint
    var uniqueValidation = await ValidateUniqueTaskTypeAsync(task.AssignedToUserId, task.TaskType);
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
    private ITaskWorkflowService _service = null!;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;
        
        _context = new ApplicationDbContext(_options);
        await _context.Database.EnsureCreatedAsync();
        
        var handlers = new ITaskHandler[] { new YourNewHandler() };
        var validationService = new TaskTypeValidationService(Options.Create(new TaskTypeValidationOptions()));
        _service = new TaskWorkflowService(
            _context,
            new TaskHandlerFactory(handlers),
            validationService,
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

1. **New custom fields**: Prefer `TaskTypeValidation` config in `appsettings.json`
2. **New Handler**: Implement `ITaskHandler` when config cannot express the behavior
3. **New Endpoint**: Add to interface, implement in service, add to controller
4. **New Validation**: Add focused tests for config rules or handler logic
5. **New Datatype**: Add config rules or a handler with appropriate validation

All extensions follow the same patterns and principles as the existing code.

**Happy extending! 🚀**
