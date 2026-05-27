using DanTaskManager.Data;
using DanTaskManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Services;

/// <summary>
/// ממשק לשירות ניהול workflow של משימות
/// </summary>
public interface ITaskWorkflowService
{
    /// <summary>
    /// שינוי סטטוס של משימה עם כללי workflow
    /// </summary>
    Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        int nextAssignedToUserId,
        string newDataJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// סגירת משימה (סטטוס סופי)
    /// </summary>
    Task<WorkflowResult> CloseTaskAsync(
        int taskId,
        int nextAssignedToUserId,
        string finalNotes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// בדיקה האם מותר לבצע שינוי במשימה שאינה שינוי סטטוס
    /// </summary>
    Task<WorkflowResult> EnsureTaskMutableAsync(
        int taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// קבלת משימות של משתמש מסוים
    /// </summary>
    Task<IEnumerable<BaseTask>> GetUserTasksAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// קבלת משימה עם פרטיה מלאים
    /// </summary>
    Task<BaseTask?> GetTaskAsync(int taskId, CancellationToken cancellationToken = default);
}

/// <summary>
/// תוצאה של פעולת workflow
/// </summary>
public class WorkflowResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? NewStatus { get; set; }
    public BaseTask? UpdatedTask { get; set; }

    public static WorkflowResult SuccessResult(int newStatus, BaseTask task, string message = "")
        => new() { Success = true, NewStatus = newStatus, UpdatedTask = task, Message = message };

    public static WorkflowResult FailureResult(string message)
        => new() { Success = false, Message = message };
}

/// <summary>
/// שירות ניהול Workflow של משימות
/// מנהל את כללי ה-Workflow כולל תנועה אחורה/קדימה ווולידציה
/// </summary>
public class TaskWorkflowService : ITaskWorkflowService
{
    private readonly ApplicationDbContext _context;
    private readonly IReadOnlyList<ITaskWorkflowRuleProvider> _ruleProviders;
    private readonly ILogger<TaskWorkflowService> _logger;

    public TaskWorkflowService(
        ApplicationDbContext context,
        IEnumerable<ITaskWorkflowRuleProvider> ruleProviders,
        ILogger<TaskWorkflowService> logger)
    {
        _context = context;
        _ruleProviders = ruleProviders
            .OrderBy(provider => provider.Priority)
            .ToArray();
        _logger = logger;
    }

    public async Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        int nextAssignedToUserId,
        string newDataJson,
        CancellationToken cancellationToken = default)
    {
        // 1. קבלת המשימה
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null)
        {
            return WorkflowResult.FailureResult("Task does not exist");
        }

        // 2. בדיקה שהמשימה לא סגורה
        if (task.CurrentStatus == WorkflowConstants.ClosedStatus)
        {
            return WorkflowResult.FailureResult("Task is closed - status cannot be changed");
        }

        var nextAssigneeExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == nextAssignedToUserId, cancellationToken);
        if (!nextAssigneeExists)
        {
            return WorkflowResult.FailureResult("Next assignee does not exist");
        }

        if (!IsValidJsonPayload(newDataJson))
        {
            return WorkflowResult.FailureResult("customFields must be a valid JSON object");
        }

        // 3. איתור ספק הכללים עבור סוג המשימה
        var ruleProvider = ResolveRuleProvider(task.TaskType);
        if (ruleProvider == null)
        {
            _logger.LogWarning(
                "No workflow rule provider found for task type {TaskType} on task {TaskId}",
                task.TaskType,
                taskId);
            return WorkflowResult.FailureResult($"Unsupported task type: {task.TaskType}");
        }

        var finalStatus = ruleProvider.GetFinalStatus(task.TaskType);

        // 4. בדיקת כללי תנועה
        var movementValidation = ValidateStatusMovement(task, newStatus, finalStatus);
        if (!movementValidation.IsValid)
        {
            return WorkflowResult.FailureResult(movementValidation.Message);
        }

        // 5. וולידציה לפי ספק הכללים שנבחר
        var validationResult = ruleProvider.ValidateStatusChange(task, newStatus, newDataJson);
        if (!validationResult.IsValid)
        {
            return WorkflowResult.FailureResult(validationResult.Message);
        }

        // 6. עדכון המשימה
        var oldStatus = task.CurrentStatus;
        var oldAssignee = task.AssignedToUserId;
        task.CurrentStatus = newStatus;
        task.AssignedToUserId = nextAssignedToUserId;
        task.CustomDataJson = newDataJson;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} updated from status {OldStatus} to {NewStatus}, assignee {OldAssignee}->{NewAssignee}",
            taskId,
            oldStatus,
            newStatus,
            oldAssignee,
            nextAssignedToUserId);

        return WorkflowResult.SuccessResult(
            newStatus,
            task,
            $"Status updated successfully to {newStatus}");
    }

    public async Task<WorkflowResult> CloseTaskAsync(
        int taskId,
        int nextAssignedToUserId,
        string finalNotes,
        CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null)
        {
            return WorkflowResult.FailureResult("Task does not exist");
        }

        if (task.CurrentStatus == WorkflowConstants.ClosedStatus)
        {
            return WorkflowResult.FailureResult("Task is already closed");
        }

        var nextAssigneeExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == nextAssignedToUserId, cancellationToken);
        if (!nextAssigneeExists)
        {
            return WorkflowResult.FailureResult("Next assignee does not exist");
        }

        var ruleProvider = ResolveRuleProvider(task.TaskType);
        var finalStatus = ruleProvider?.GetFinalStatus(task.TaskType);
        if (!finalStatus.HasValue)
        {
            return WorkflowResult.FailureResult($"Unsupported task type: {task.TaskType}");
        }

        if (task.CurrentStatus != finalStatus.Value)
        {
            return WorkflowResult.FailureResult(
                $"A {task.TaskType} task can only be closed from final status {finalStatus.Value}");
        }

        // עדכון JSON עם הערות סופיות
        var updatedJson = task.CustomDataJson;
        try
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(updatedJson) ?? new();
            dict["finalNotes"] = finalNotes;
            dict["closedAt"] = DateTime.UtcNow.ToString("o");
            updatedJson = System.Text.Json.JsonSerializer.Serialize(dict);
        }
        catch
        {
            updatedJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                finalNotes,
                closedAt = DateTime.UtcNow.ToString("o")
            });
        }

        var oldAssignee = task.AssignedToUserId;
        task.CurrentStatus = WorkflowConstants.ClosedStatus;
        task.AssignedToUserId = nextAssignedToUserId;
        task.CustomDataJson = updatedJson;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} closed with notes: {Notes}, assignee {OldAssignee}->{NewAssignee}",
            taskId,
            finalNotes,
            oldAssignee,
            nextAssignedToUserId);

        return WorkflowResult.SuccessResult(
            WorkflowConstants.ClosedStatus,
            task,
            "Task closed successfully");
    }

    public async Task<WorkflowResult> EnsureTaskMutableAsync(
        int taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null)
        {
            return WorkflowResult.FailureResult("Task does not exist");
        }

        if (task.CurrentStatus == WorkflowConstants.ClosedStatus)
        {
            return WorkflowResult.FailureResult("Closed task is immutable and cannot be modified");
        }

        return WorkflowResult.SuccessResult(task.CurrentStatus, task);
    }

    public async Task<IEnumerable<BaseTask>> GetUserTasksAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.AssignedToUserId == userId)
            .Include(t => t.AssignedToUser)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<BaseTask?> GetTaskAsync(int taskId, CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
    }

    /// <summary>
    /// וולידציה של כללי תנועה בין סטטוסים
    /// </summary>
    private StatusMovementValidation ValidateStatusMovement(BaseTask task, int newStatus, int? finalStatus)
    {
        if (newStatus == WorkflowConstants.ClosedStatus)
        {
            return new() { IsValid = false, Message = "Task closing is allowed only via CloseTask" };
        }

        // בדיקה בסיסית - הסטטוס חייב להתחיל מ-1
        if (newStatus < WorkflowConstants.CreatedStatus)
        {
            return new() { IsValid = false, Message = $"Status must be {WorkflowConstants.CreatedStatus} or higher" };
        }

        // בדיקה - אי אפשר להעבור את הסטטוס הסופי
        if (finalStatus.HasValue && task.CurrentStatus >= finalStatus.Value && newStatus > task.CurrentStatus)
        {
            return new() { IsValid = false, Message = $"Task already reached final status ({finalStatus.Value})" };
        }

        // כללי תנועה: קדימה או אחורה?
        if (newStatus > task.CurrentStatus)
        {
            // תנועה קדימה - חייבת להיות בדיוק +1
            if (newStatus != task.CurrentStatus + 1)
            {
                return new()
                {
                    IsValid = false,
                    Message = $"Forward movement must be exactly +1 status. Current status: {task.CurrentStatus}, requested: {newStatus}"
                };
            }
        }
        else if (newStatus < task.CurrentStatus)
        {
            // תנועה אחורה - מותרת לכל סטטוס נמוך יותר
            _logger.LogInformation(
                "Backward movement detected for task {TaskId}: {OldStatus} -> {NewStatus}",
                task.Id,
                task.CurrentStatus,
                newStatus);
        }
        else
        {
            // אותו סטטוס
            return new() { IsValid = false, Message = "New status is identical to current status" };
        }

        return new() { IsValid = true };
    }

    private class StatusMovementValidation
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private static bool IsValidJsonPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private ITaskWorkflowRuleProvider? ResolveRuleProvider(string taskType)
    {
        if (!WorkflowConstants.IsSupportedTaskType(taskType))
        {
            return null;
        }

        return _ruleProviders.FirstOrDefault(provider => provider.CanHandle(taskType));
    }
}
