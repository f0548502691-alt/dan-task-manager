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
            return WorkflowResult.FailureResult("משימה לא קיימת");
        }

        // 2. בדיקה שהמשימה לא סגורה
        if (task.CurrentStatus == WorkflowConstants.ClosedStatus)
        {
            return WorkflowResult.FailureResult("משימה סגורה - לא ניתן לשנות סטטוס");
        }

        var nextAssigneeExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == nextAssignedToUserId, cancellationToken);
        if (!nextAssigneeExists)
        {
            return WorkflowResult.FailureResult("המשתמש הבא לא קיים");
        }

        if (!IsValidJsonPayload(newDataJson))
        {
            return WorkflowResult.FailureResult("customFields חייב להיות אובייקט JSON תקין");
        }

        // 3. איתור ספק הכללים עבור סוג המשימה
        var ruleProvider = ResolveRuleProvider(task.TaskType);
        if (ruleProvider == null)
        {
            _logger.LogWarning(
                "לא נמצא ספק כללים עבור סוג משימה {TaskType} במשימה {TaskId}",
                task.TaskType,
                taskId);
            return WorkflowResult.FailureResult($"סוג משימה לא נתמך: {task.TaskType}");
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
            "משימה {TaskId} עודכנה מסטטוס {OldStatus} ל-{NewStatus}, הקצאה {OldAssignee}->{NewAssignee}",
            taskId,
            oldStatus,
            newStatus,
            oldAssignee,
            nextAssignedToUserId);

        return WorkflowResult.SuccessResult(
            newStatus,
            task,
            $"סטטוס עודכן בהצלחה ל-{newStatus}");
    }

    public async Task<WorkflowResult> CloseTaskAsync(
        int taskId,
        string finalNotes,
        CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null)
        {
            return WorkflowResult.FailureResult("משימה לא קיימת");
        }

        if (task.CurrentStatus == WorkflowConstants.ClosedStatus)
        {
            return WorkflowResult.FailureResult("משימה כבר סגורה");
        }

        var ruleProvider = ResolveRuleProvider(task.TaskType);
        var finalStatus = ruleProvider?.GetFinalStatus(task.TaskType);
        if (!finalStatus.HasValue)
        {
            return WorkflowResult.FailureResult($"סוג משימה לא נתמך: {task.TaskType}");
        }

        if (task.CurrentStatus != finalStatus.Value)
        {
            return WorkflowResult.FailureResult(
                $"ניתן לסגור משימה מסוג {task.TaskType} רק מסטטוס סופי {finalStatus.Value}");
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

        task.CurrentStatus = WorkflowConstants.ClosedStatus;
        task.CustomDataJson = updatedJson;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "משימה {TaskId} סגורה עם הערות: {Notes}",
            taskId,
            finalNotes);

        return WorkflowResult.SuccessResult(
            WorkflowConstants.ClosedStatus,
            task,
            "משימה סגורה בהצלחה");
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
            return WorkflowResult.FailureResult("משימה לא קיימת");
        }

        if (task.CurrentStatus == WorkflowConstants.ClosedStatus)
        {
            return WorkflowResult.FailureResult("משימה סגורה היא immutable ולא ניתן לבצע שינוי");
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
            return new() { IsValid = false, Message = "סגירת משימה מתבצעת רק דרך CloseTask" };
        }

        // בדיקה בסיסית - הסטטוס חייב להתחיל מ-1
        if (newStatus < WorkflowConstants.CreatedStatus)
        {
            return new() { IsValid = false, Message = $"סטטוס חייב להיות {WorkflowConstants.CreatedStatus} ומעלה" };
        }

        // בדיקה - אי אפשר להעבור את הסטטוס הסופי
        if (finalStatus.HasValue && task.CurrentStatus >= finalStatus.Value && newStatus > task.CurrentStatus)
        {
            return new() { IsValid = false, Message = $"משימה כבר הגיעה לסטטוס סופי ({finalStatus.Value})" };
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
                    Message = $"תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: {task.CurrentStatus}, מבוקש: {newStatus}"
                };
            }
        }
        else if (newStatus < task.CurrentStatus)
        {
            // תנועה אחורה - מותרת לכל סטטוס נמוך יותר
            _logger.LogInformation(
                "תנועה אחורה מסומנת למשימה {TaskId}: {OldStatus} -> {NewStatus}",
                task.Id,
                task.CurrentStatus,
                newStatus);
        }
        else
        {
            // אותו סטטוס
            return new() { IsValid = false, Message = "סטטוס חדש זהה לסטטוס הנוכחי" };
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
        return _ruleProviders.FirstOrDefault(provider => provider.CanHandle(taskType));
    }
}
