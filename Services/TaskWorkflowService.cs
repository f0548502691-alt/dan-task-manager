using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
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
    Task<WorkflowResult> ChangeStatusAsync(int taskId, int newStatus, string newDataJson);

    /// <summary>
    /// סגירת משימה (סטטוס סופי)
    /// </summary>
    Task<WorkflowResult> CloseTaskAsync(int taskId, string finalNotes);

    /// <summary>
    /// קבלת משימות של משתמש מסוים
    /// </summary>
    Task<IEnumerable<BaseTask>> GetUserTasksAsync(int userId);

    /// <summary>
    /// קבלת משימה עם פרטיה מלאים
    /// </summary>
    Task<BaseTask?> GetTaskAsync(int taskId);
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
    private readonly TaskHandlerFactory _handlerFactory;
    private readonly ILogger<TaskWorkflowService> _logger;

    // סטטוס סגירה (משימה סגורה לא יכולה להשתנות)
    private const int ClosedStatus = 99;

    public TaskWorkflowService(
        ApplicationDbContext context,
        TaskHandlerFactory handlerFactory,
        ILogger<TaskWorkflowService> logger)
    {
        _context = context;
        _handlerFactory = handlerFactory;
        _logger = logger;
    }

    public async Task<WorkflowResult> ChangeStatusAsync(int taskId, int newStatus, string newDataJson)
    {
        // 1. קבלת המשימה
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
        {
            return WorkflowResult.FailureResult("משימה לא קיימת");
        }

        // 2. בדיקה שהמשימה לא סגורה
        if (task.CurrentStatus == ClosedStatus)
        {
            return WorkflowResult.FailureResult("משימה סגורה - לא ניתן לשנות סטטוס");
        }

        // 3. קבלת ה-Handler לבדיקת הוולידציה
        var handler = _handlerFactory.GetHandler(task.TaskType);

        // 4. בדיקת כללי תנועה
        var movementValidation = ValidateStatusMovement(task, newStatus, handler);
        if (!movementValidation.IsValid)
        {
            return WorkflowResult.FailureResult(movementValidation.Message);
        }

        // 5. וולידציה ספציפית של Handler (אם קיים)
        if (handler != null)
        {
            var handlerValidation = handler.ValidateStatusChange(
                task.CustomDataJson,
                task.CurrentStatus,
                newStatus,
                newDataJson);

            if (!handlerValidation.IsValid)
            {
                return WorkflowResult.FailureResult(handlerValidation.Message);
            }
        }

        // 6. עדכון המשימה
        task.CurrentStatus = newStatus;
        task.CustomDataJson = newDataJson;
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "משימה {TaskId} עודכנה מסטטוס {OldStatus} ל-{NewStatus}",
            taskId,
            task.CurrentStatus,
            newStatus);

        return WorkflowResult.SuccessResult(
            newStatus,
            task,
            $"סטטוס עודכן בהצלחה ל-{newStatus}");
    }

    public async Task<WorkflowResult> CloseTaskAsync(int taskId, string finalNotes)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
        {
            return WorkflowResult.FailureResult("משימה לא קיימת");
        }

        if (task.CurrentStatus == ClosedStatus)
        {
            return WorkflowResult.FailureResult("משימה כבר סגורה");
        }

        // עדכון JSON עם הערות סופיות
        var updatedJson = task.CustomDataJson;
        try
        {
            var jsonDoc = System.Text.Json.JsonDocument.Parse(updatedJson);
            var json = System.Text.Json.JsonSerializer.Deserialize<dynamic>(updatedJson) ?? new();
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

        task.CurrentStatus = ClosedStatus;
        task.CustomDataJson = updatedJson;
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "משימה {TaskId} סגורה עם הערות: {Notes}",
            taskId,
            finalNotes);

        return WorkflowResult.SuccessResult(
            ClosedStatus,
            task,
            "משימה סגורה בהצלחה");
    }

    public async Task<IEnumerable<BaseTask>> GetUserTasksAsync(int userId)
    {
        return await _context.Tasks
            .Where(t => t.AssignedToUserId == userId && t.CurrentStatus != ClosedStatus)
            .Include(t => t.AssignedToUser)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<BaseTask?> GetTaskAsync(int taskId)
    {
        return await _context.Tasks
            .Include(t => t.AssignedToUser)
            .FirstOrDefaultAsync(t => t.Id == taskId);
    }

    /// <summary>
    /// וולידציה של כללי תנועה בין סטטוסים
    /// </summary>
    private StatusMovementValidation ValidateStatusMovement(BaseTask task, int newStatus, ITaskHandler? handler)
    {
        // בדיקה בסיסית - סטטוס לא יכול להיות שלילי
        if (newStatus < 0)
        {
            return new() { IsValid = false, Message = "סטטוס לא יכול להיות שלילי" };
        }

        // קבלת הסטטוס הסופי (אם קיים Handler)
        int? finalStatus = handler?.FinalStatus;

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
}
