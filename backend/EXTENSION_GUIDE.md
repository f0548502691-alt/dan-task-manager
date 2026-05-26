# 🔧 Extension Guide - Adding New Features to Dan Task Manager

## 📖 Table of Contents

1. [Adding New Handler Type](#adding-new-handler-type)
2. [Adding or Updating Task Type Metadata](#adding-or-updating-task-type-metadata)
3. [Adding New Endpoint](#adding-new-endpoint)
4. [Adding Validation Rule](#adding-validation-rule)
5. [Common Extension Scenarios](#common-extension-scenarios)
6. [Testing Extensions](#testing-extensions)

---

## 🎯 Adding New Handler Type

Use a handler when validation must stay in code. For configurable task types, prefer metadata through
`POST /api/task-types` and field definitions through `POST /api/task-types/{taskType}/fields`.

Current workflow constraints apply to both approaches:
- `FinalStatus` must be present, greater than or equal to `1`, and less than `99`.
- Status `99` is reserved for closed tasks and can only be reached through `POST /api/tasks/{id}/close`.
- Status `0` is not a valid workflow status.
- Status changes and close operations must include a valid `nextAssignedToUserId`.

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

### Step 2: Handler Discovery

Handlers are registered by assembly scanning in `Program.cs`:

```csharp
builder.Services.AddTaskHandlersFromAssembly(typeof(ITaskHandler).Assembly);
```

To be discovered, the handler must be a public, non-abstract class implementing `ITaskHandler` in the
assembly that contains `ITaskHandler`. `TaskHandlerFactory` indexes handlers by `TaskType` using
case-insensitive keys.

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

## 🧾 Adding or Updating Task Type Metadata

Metadata-backed task types are served by `TaskTypesController` and validated by
`TaskTypeValidationService`. Metadata providers have higher workflow priority than handlers, so a
database/configured task type with the same `TaskType` wins over handler validation.

### Create or update a task type

```http
POST /api/task-types
Content-Type: application/json

{
  "taskType": "QA",
  "displayName": "QA",
  "finalStatus": 3,
  "isActive": true
}
```

Constraints enforced by `UpsertTaskType`:
- `taskType` is required.
- `finalStatus` is required.
- `finalStatus >= WorkflowConstants.CreatedStatus` (`1`).
- `finalStatus < WorkflowConstants.ClosedStatus` (`99`).

### Add a field rule

```http
POST /api/task-types/QA/fields
Content-Type: application/json

{
  "field": "testResults",
  "type": "string",
  "required": true,
  "appliesFromStatus": 3,
  "appliesToStatus": 3,
  "allowedValues": ["PASSED", "FAILED", "PARTIAL"]
}
```

Field status constraints:
- `appliesFromStatus <= appliesToStatus` when both are provided.
- `appliesFromStatus` and `appliesToStatus` must be `>= 1`.
- Neither applies bound can exceed the task type `finalStatus`.

Supported field types include `string`, `number`, `array`, `object`, `boolean`, and `stringOrNumber`.
Patterns can be a regex, `valid_git_branch`, or `semantic_version`.

### Use the metadata-backed task type

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "QA",
  "description": "Verify checkout flow",
  "assignedToUserId": 1,
  "customFields": {}
}
```

```http
POST /api/tasks/42/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 1,
  "customFields": {}
}
```

The close operation must also carry the assignee that should own the closed task:

```http
POST /api/tasks/42/close
Content-Type: application/json

{
  "nextAssignedToUserId": 1,
  "finalNotes": "QA complete"
}
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
public async Task<WorkflowResult> ChangeStatusAsync(
    int taskId,
    int newStatus,
    int nextAssignedToUserId,
    string newDataJson)
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
    var task1 = new BaseTask { TaskType = "Procurement", AssignedToUserId = 1, CurrentStatus = 1 };
    var task2 = new BaseTask { TaskType = "Procurement", AssignedToUserId = 1, CurrentStatus = 1 };
    
    _context.Tasks.Add(task1);
    _context.Tasks.Add(task2);
    await _context.SaveChangesAsync();

    // Act: Try to move second task to Status 2
    var result = await _service.ChangeStatusAsync(task2.Id, 2, 1, "{\"prices\":[\"5000\",\"4800\"]}");

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
        _service = new TaskWorkflowService(
            _context,
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

1. **New Handler**: Implement `ITaskHandler`; assembly scanning registers it automatically
2. **New Endpoint**: Add to interface, implement in service, add to controller
3. **New Validation**: Add validation method, integrate into workflow
4. **New Datatype**: Add handler with appropriate validation

All extensions follow the same patterns and principles as the existing code.

**Happy extending! 🚀**
