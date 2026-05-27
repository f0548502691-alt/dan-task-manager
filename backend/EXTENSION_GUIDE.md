# 🔧 Extension Guide - Adding New Features to Dan Task Manager

## 📖 Table of Contents

1. [Adding or Changing Supported Task Types](#adding-or-changing-supported-task-types)
2. [Adding New Endpoint](#adding-new-endpoint)
3. [Adding Validation Rule](#adding-validation-rule)
4. [Common Extension Scenarios](#common-extension-scenarios)
5. [Testing Extensions](#testing-extensions)

---

## 🎯 Adding or Changing Supported Task Types

The current product workflow intentionally supports only `Procurement` and
`Development`. A new task type is not metadata-only: task creation,
metadata writes, workflow provider resolution, and the frontend board all depend
on the same supported-type contract.

### Current allow-list

```csharp
// Domain/WorkflowConstants.cs
public static readonly string[] SupportedTaskTypes =
{
    "Procurement",
    "Development"
};
```

`TaskApplicationService.CreateAsync`, `TaskWorkflowService.ResolveRuleProvider`,
and `TaskTypesController` all check this allow-list. If you add another type,
update this constant first and then keep the rest of the system in sync.

### Backend checklist

1. Add the new type to `WorkflowConstants.SupportedTaskTypes`.
2. Register the fallback handler explicitly in `Program.cs`.
3. Seed or migrate `TaskTypeMetadata` and `TaskFieldDefinition` rows in
   `ApplicationDbContext` or a migration.
4. Add handler or metadata validation tests for each required status.
5. Update unsupported-type tests so the returned `supportedTaskTypes` list is
   still deterministic.

Example handler registration:

```csharp
builder.Services.AddTransient<ITaskHandler, ProcurementTaskHandler>();
builder.Services.AddTransient<ITaskHandler, DevelopmentTaskHandler>();
builder.Services.AddTransient<ITaskHandler, QATaskHandler>();
```

### API contract checklist

Clients must use the current request shapes:

```json
{
  "taskType": "Procurement",
  "description": "Collect supplier quotes",
  "assignedToUserId": 1,
  "customFields": {}
}
```

```json
{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

Do not use older `customDataJson` or `newDataJson` request properties. They are
internal storage/history names, not the current public API.

### Frontend checklist

Update the Angular workflow UI with the backend change:

1. Add the type to `TASK_TYPE_OPTIONS` in
   `frontend/src/app/tasks/task-workflow-board.component.ts`.
2. Add the final status to `TASK_FINAL_STATUS_BY_TYPE`.
3. Add status labels if generic `Status N` labels are not enough.
4. Add or update a `TaskWorkflowAdapter` for type-specific form hydration and
   `customFields` payload construction.
5. Add a field component if the workflow needs more than the fallback JSON
   editor.

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

    // Act: Try to move second task to Status 2 without changing assignment
    var result = await _service.ChangeStatusAsync(
        task2.Id,
        2,
        1,
        "{\"prices\":[\"5000\",\"4800\"]}");

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

1. **New Handler**: Implement ITaskHandler, register in Program.cs
2. **New Endpoint**: Add to interface, implement in service, add to controller
3. **New Validation**: Add validation method, integrate into workflow
4. **New Datatype**: Add handler with appropriate validation

All extensions follow the same patterns and principles as the existing code.

**Happy extending! 🚀**
