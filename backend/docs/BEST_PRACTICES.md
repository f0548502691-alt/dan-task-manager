# 📖 Best Practices & Code Conventions

## 🏗️ Architecture Principles

### 1. Separation of Concerns
```csharp
// ❌ Bad: Mixed concerns
public class TaskController
{
    public void UpdateTask(int id)
    {
        // Validation
        // Database access
        // Business logic
        // HTTP response
    }
}

// ✅ Good: Separated concerns
public class TasksController
{
    private readonly ITaskWorkflowService _service;
    
    public async Task<IActionResult> ChangeStatus(int id, ChangeStatusWorkflowRequest request)
    {
        // HTTP handling only
        var result = await _service.ChangeStatusAsync(id, request.NewStatus, request.NewDataJson);
        return result.Success ? Ok(result) : BadRequest(new { error = result.Message });
    }
}

public class TaskWorkflowService : ITaskWorkflowService
{
    public async Task<WorkflowResult> ChangeStatusAsync(int taskId, int newStatus, string newDataJson)
    {
        // Business logic only
        // Validation
        // Handler delegation
        // Database updates
    }
}
```

### 2. Dependency Injection
```csharp
// ❌ Bad: Tightly coupled
public class TaskWorkflowService
{
    public TaskWorkflowService()
    {
        _context = new ApplicationDbContext();
        _factory = new TaskHandlerFactory();
    }
}

// ✅ Good: Injected dependencies
public class TaskWorkflowService
{
    private readonly ApplicationDbContext _context;
    private readonly TaskHandlerFactory _factory;
    
    public TaskWorkflowService(
        ApplicationDbContext context,
        TaskHandlerFactory factory,
        ILogger<TaskWorkflowService> logger)
    {
        _context = context;
        _factory = factory;
        _logger = logger;
    }
}

// Registered in Program.cs
services.AddScoped<ITaskWorkflowService, TaskWorkflowService>();
```

### 3. Single Responsibility
```csharp
// ❌ Bad: Multiple responsibilities
public class TaskHandler
{
    public void ProcessTask()
    {
        // Validate status
        // Update database
        // Send email
        // Log activity
        // Create audit entry
    }
}

// ✅ Good: Single responsibility
public class ProcurementTaskHandler : ITaskHandler
{
    // ONLY validates procurement-specific rules
    public ValidationResult ValidateStatusChange(...)
    {
        // Validate prices
        // Validate receipt
        return ValidationResult.Success();
    }
}
```

---

## 📝 Code Standards

### Naming Conventions

#### Classes
```csharp
// Handlers
public class ProcurementTaskHandler
public class DevelopmentTaskHandler

// Services
public class TaskWorkflowService
public class TaskStatusService

// Controllers
public class TasksController
public class UsersController

// Request/Response DTOs
public class CreateTaskRequest
public class ChangeStatusWorkflowRequest
public class WorkflowResult
```

#### Methods
```csharp
// Async methods
public async Task<TaskWorkflowResult> ChangeStatusAsync(...)
public async Task<IEnumerable<BaseTask>> GetUserTasksAsync(...)

// Validation methods
public ValidationResult ValidateStatusChange(...)
private ValidationResult ValidateStatusTwo(...)

// Helper methods
private bool IsValidGitBranchName(string branch)
private decimal ParsePrice(string priceString)
```

#### Properties
```csharp
// Domain models
public int Id { get; set; }
public string TaskType { get; set; }
public int CurrentStatus { get; set; }

// Request DTOs
public int NewStatus { get; set; }
public string NewDataJson { get; set; }

// Result objects
public bool Success { get; set; }
public string Message { get; set; }
```

---

## ✅ Validation Patterns

### 1. Request Validation
```csharp
// ✅ Good: Validate at entry point
[HttpPost]
public async Task<IActionResult> CreateTask(CreateTaskRequest request)
{
    // Validate input
    if (string.IsNullOrWhiteSpace(request.TaskType))
        return BadRequest(new { error = "TaskType cannot be empty" });
    
    if (string.IsNullOrWhiteSpace(request.Description))
        return BadRequest(new { error = "Description cannot be empty" });
    
    // Check if user exists
    var user = await _context.Users.FindAsync(request.AssignedToUserId);
    if (user == null)
        return BadRequest(new { error = $"User {request.AssignedToUserId} not found" });
    
    // Check if handler exists
    if (!_factory.HasHandler(request.TaskType))
        return BadRequest(new { error = $"Unknown task type: {request.TaskType}" });
    
    // Proceed with creation
    return CreatedAtAction(...);
}
```

### 2. State Validation
```csharp
// ✅ Good: Validate state transitions
public async Task<WorkflowResult> ChangeStatusAsync(int taskId, int newStatus, string newDataJson)
{
    var task = await _context.Tasks.FindAsync(taskId);
    
    // Check if closed
    if (task.CurrentStatus == 99)
        return WorkflowResult.Failure("Task is closed");
    
    // Validate movement
    var movement = ValidateStatusMovement(task.CurrentStatus, newStatus);
    if (!movement.IsValid)
        return WorkflowResult.Failure(movement.Message);
    
    // Delegate to handler
    var handler = _factory.GetHandler(task.TaskType);
    var validation = handler.ValidateStatusChange(...);
    if (!validation.IsValid)
        return WorkflowResult.Failure(validation.Message);
    
    return WorkflowResult.Success();
}
```

### 3. Data Validation (Handler)
```csharp
// ✅ Good: Parse and validate JSON
private ValidationResult ValidateStatusTwo(string dataJson)
{
    try
    {
        var json = JsonDocument.Parse(dataJson);
        
        // Check field exists
        if (!json.RootElement.TryGetProperty("prices", out var pricesElement))
            return ValidationResult.Failure("'prices' field not found");
        
        // Check is array
        if (pricesElement.ValueKind != JsonValueKind.Array)
            return ValidationResult.Failure("'prices' must be an array");
        
        // Check count
        var count = pricesElement.GetArrayLength();
        if (count != 2)
            return ValidationResult.Failure($"'prices' must have exactly 2 items, found {count}");
        
        // Check each element
        foreach (var item in pricesElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(item.GetString()))
                return ValidationResult.Failure("Each price must be a non-empty string");
        }
        
        return ValidationResult.Success();
    }
    catch (JsonException)
    {
        return ValidationResult.Failure("Invalid JSON format");
    }
}
```

---

## 🔄 Workflow Patterns

### Status Movement Logic
```csharp
// ✅ Good: Clear, documented logic
private StatusMovementValidation ValidateStatusMovement(int currentStatus, int newStatus, int finalStatus)
{
    // Forward movement: must be exactly +1
    if (newStatus > currentStatus)
    {
        if (newStatus != currentStatus + 1)
            return new StatusMovementValidation 
            { 
                IsValid = false, 
                Message = $"Forward movement must be exactly +1. Current: {currentStatus}, Requested: {newStatus}"
            };
        
        // Check final status
        if (currentStatus >= finalStatus)
            return new StatusMovementValidation 
            { 
                IsValid = false, 
                Message = $"Task reached final status: {finalStatus}"
            };
    }
    
    // Backward movement: can go to any lower status
    else if (newStatus < currentStatus)
    {
        // Allowed
    }
    
    // Same status: not allowed
    else
    {
        return new StatusMovementValidation 
        { 
            IsValid = false, 
            Message = "Cannot move to same status"
        };
    }
    
    return new StatusMovementValidation { IsValid = true };
}
```

---

## 📦 Response Patterns

### Success Response
```csharp
// ✅ Consistent response format
public class WorkflowResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int? NewStatus { get; set; }
    public BaseTask? UpdatedTask { get; set; }

    public static WorkflowResult Success(string message = "", int newStatus = 0, BaseTask? task = null)
    {
        return new WorkflowResult
        {
            Success = true,
            Message = message,
            NewStatus = newStatus,
            UpdatedTask = task
        };
    }

    public static WorkflowResult Failure(string message)
    {
        return new WorkflowResult
        {
            Success = false,
            Message = message
        };
    }
}

// Usage
return WorkflowResult.Success("Status changed to 2", 2, updatedTask);
return WorkflowResult.Failure("Invalid movement");
```

### HTTP Endpoint Pattern
```csharp
// ✅ Good: Clear HTTP handling
[HttpPost("{id}/change-status")]
public async Task<IActionResult> ChangeStatusWorkflow(
    int id,
    [FromBody] ChangeStatusWorkflowRequest request)
{
    // Input validation
    if (request.NewStatus < 0)
        return BadRequest(new { error = "Invalid status" });
    
    // Call service
    var result = await _workflowService.ChangeStatusAsync(id, request.NewStatus, request.NewDataJson);
    
    // Return appropriate response
    if (!result.Success)
        return BadRequest(new { error = result.Message });
    
    return Ok(new
    {
        success = true,
        message = result.Message,
        newStatus = result.NewStatus,
        task = result.UpdatedTask
    });
}
```

---

## 🧪 Testing Patterns

### Unit Test Structure
```csharp
// ✅ Good: AAA pattern (Arrange, Act, Assert)
[Fact]
public async Task ChangeStatus_ForwardMovement_Plus1_ShouldSucceed()
{
    // Arrange - Set up data
    var task = new BaseTask { CurrentStatus = 1 };
    _context.Tasks.Add(task);
    await _context.SaveChangesAsync();

    // Act - Execute operation
    var result = await _service.ChangeStatusAsync(task.Id, 2, "{}");

    // Assert - Verify results
    Assert.True(result.Success);
    Assert.Equal(2, result.NewStatus);
    Assert.NotNull(result.UpdatedTask);
}
```

### Error Path Testing
```csharp
// ✅ Good: Test error paths
[Fact]
public async Task ChangeStatus_InvalidJump_ShouldFail()
{
    // Arrange
    var task = new BaseTask { CurrentStatus = 1 };
    _context.Tasks.Add(task);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.ChangeStatusAsync(task.Id, 3, "{}");

    // Assert
    Assert.False(result.Success);
    Assert.Contains("exactly +1", result.Message);
}
```

---

## 🔐 Error Handling

### Try-Catch Pattern
```csharp
// ✅ Good: Specific error handling
public async Task<WorkflowResult> ChangeStatusAsync(int taskId, int newStatus, string newDataJson)
{
    try
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
            return WorkflowResult.Failure($"Task {taskId} not found");

        // Business logic...
        
        await _context.SaveChangesAsync();
        return WorkflowResult.Success("Status changed", newStatus, task);
    }
    catch (DbUpdateException ex)
    {
        _logger.LogError(ex, $"Database error updating task {taskId}");
        return WorkflowResult.Failure("Database error - please try again");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Unexpected error in ChangeStatusAsync for task {taskId}");
        return WorkflowResult.Failure("An unexpected error occurred");
    }
}
```

### Logging Pattern
```csharp
// ✅ Good: Structured logging
public async Task<WorkflowResult> ChangeStatusAsync(int taskId, int newStatus, string newDataJson)
{
    _logger.LogInformation($"Attempting to change task {taskId} status from {task.CurrentStatus} to {newStatus}");
    
    try
    {
        // Operation...
        _logger.LogInformation($"Successfully changed task {taskId} status to {newStatus}");
        return WorkflowResult.Success(...);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Failed to change task {taskId} status");
        return WorkflowResult.Failure("Operation failed");
    }
}
```

---

## 📚 Documentation Standards

### XML Comments
```csharp
/// <summary>
/// Changes task status with workflow validation
/// </summary>
/// <param name="taskId">The task ID</param>
/// <param name="newStatus">The new status</param>
/// <param name="newDataJson">Handler-specific data as JSON</param>
/// <returns>WorkflowResult indicating success or failure</returns>
/// <remarks>
/// Validates:
/// - Task is not closed (Status 99)
/// - Forward movement is exactly +1
/// - Backward movement is to any lower status
/// - Handler-specific validation passes
/// </remarks>
public async Task<WorkflowResult> ChangeStatusAsync(
    int taskId,
    int newStatus,
    string newDataJson)
{
    // Implementation
}
```

### Endpoint Documentation
```csharp
/// <summary>
/// Change task status with workflow rules
/// </summary>
/// <remarks>
/// Request body must contain:
/// - newStatus: int (next status)
/// - newDataJson: string (handler-specific data)
/// 
/// Workflow rules:
/// - Forward movement: +1 only
/// - Backward movement: to any lower status
/// - Closed tasks (99): cannot be changed
/// 
/// Returns 200 on success, 400 on validation error
/// </remarks>
[HttpPost("{id}/change-status")]
public async Task<IActionResult> ChangeStatusWorkflow(int id, ChangeStatusWorkflowRequest request)
{
    // Implementation
}
```

---

## ✨ Summary of Best Practices

✅ **Separation of Concerns** - Controllers handle HTTP, Services handle business logic  
✅ **Dependency Injection** - Inject dependencies, don't create them  
✅ **Single Responsibility** - Each class has one reason to change  
✅ **Validation** - Validate at entry points and in services  
✅ **Clear Naming** - Names explain purpose and behavior  
✅ **Consistent Responses** - Use standard result/response classes  
✅ **Error Handling** - Catch specific exceptions, log appropriately  
✅ **Unit Testing** - Test success and error paths  
✅ **Documentation** - Comments explain why, not what  
✅ **Logging** - Track important operations and errors  

---

## 🚀 Next Steps

1. **Run Tests**: `dotnet test` - Ensure all tests pass
2. **Build Project**: `dotnet build` - Check for compilation errors
3. **Run Application**: `dotnet run` - Start the server
4. **Test Endpoints**: Use Postman or curl to test APIs
5. **Extend System**: Add new handlers for additional task types

---

**Follow these patterns to maintain code quality and consistency! 💪**
