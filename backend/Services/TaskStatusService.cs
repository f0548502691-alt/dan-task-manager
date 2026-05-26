using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;

namespace DanTaskManager.Services;

/// <summary>
/// שירות לניהול סטטוסים של משימות עם וולידציה
/// מנצל את ה-Strategy pattern דרך ITaskHandler
/// </summary>
public class TaskStatusService : ITaskStatusService
{
    private readonly TaskHandlerFactory _handlerFactory;
    private readonly ILogger<TaskStatusService> _logger;

    public TaskStatusService(
        TaskHandlerFactory handlerFactory,
        ILogger<TaskStatusService> logger)
    {
        _handlerFactory = handlerFactory;
        _logger = logger;
    }

    public TaskStatusChangeResult ValidateAndChangeStatus(
        BaseTask task,
        int nextStatus,
        string newDataJson)
    {
        // בדיקה בסיסית
        if (task == null)
        {
            return TaskStatusChangeResult.FailureResult("המשימה היא null");
        }

        if (nextStatus < WorkflowConstants.CreatedStatus)
        {
            return TaskStatusChangeResult.FailureResult(
                $"סטטוס חייב להיות {WorkflowConstants.CreatedStatus} ומעלה");
        }

        // קבלת Handler מתאים לפי סוג המשימה
        var handler = _handlerFactory.GetHandler(task.TaskType);

        // אם אין Handler - בדיקה בסיסית בלבד
        if (handler == null)
        {
            _logger.LogWarning(
                "לא נמצא Handler עבור סוג משימה: {TaskType}. מבצע וולידציה בסיסית בלבד",
                task.TaskType);

            return ValidateBasicStatusChange(task, nextStatus);
        }

        if (nextStatus == WorkflowConstants.ClosedStatus)
        {
            return TaskStatusChangeResult.FailureResult("סגירת משימה מתבצעת רק דרך פעולה ייעודית");
        }

        // בדיקת סטטוס סופי
        if (task.CurrentStatus >= handler.FinalStatus)
        {
            if (nextStatus > task.CurrentStatus)
            {
                return TaskStatusChangeResult.FailureResult(
                    $"המשימה כבר הגיעה לסטטוס סופי ({handler.FinalStatus}), לא ניתן להתקדם");
            }
        }

        // וולידציה של תנועת סטטוס
        var movementValidation = ValidateMovement(task.CurrentStatus, nextStatus);
        if (!movementValidation.Success)
        {
            return movementValidation;
        }

        // וולידציה דרך Handler
        var validationResult = handler.ValidateStatusChange(
            task.CustomDataJson,
            task.CurrentStatus,
            nextStatus,
            newDataJson);

        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "וולידציה נכשלה עבור משימה {TaskId}: {Message}",
                task.Id,
                validationResult.Message);

            return TaskStatusChangeResult.FailureResult(validationResult.Message);
        }

        // כל הוולידציות עברו בהצלחה
        _logger.LogInformation(
            "שינוי סטטוס בוצע בהצלחה עבור משימה {TaskId}: {OldStatus} -> {NewStatus}",
            task.Id,
            task.CurrentStatus,
            nextStatus);

        return TaskStatusChangeResult.SuccessResult(
            nextStatus,
            $"סטטוס עודכן בהצלחה מ-{task.CurrentStatus} ל-{nextStatus}");
    }

    public int? GetFinalStatus(string taskType)
    {
        if (string.IsNullOrWhiteSpace(taskType))
            return null;

        var handler = _handlerFactory.GetHandler(taskType);
        return handler?.FinalStatus;
    }

    /// <summary>
    /// וולידציה בסיסית בלבד (כאשר אין Handler ספציפי)
    /// </summary>
    private static TaskStatusChangeResult ValidateBasicStatusChange(BaseTask task, int nextStatus)
    {
        return ValidateMovement(task.CurrentStatus, nextStatus);
    }

    private static TaskStatusChangeResult ValidateMovement(int currentStatus, int nextStatus)
    {
        if (nextStatus == currentStatus)
        {
            return TaskStatusChangeResult.FailureResult("סטטוס חדש זהה לסטטוס הנוכחי");
        }

        if (nextStatus > currentStatus && nextStatus != currentStatus + 1)
        {
            return TaskStatusChangeResult.FailureResult(
                $"תנועה קדימה חייבת להיות רציפה (+1). סטטוס נוכחי: {currentStatus}, מבוקש: {nextStatus}");
        }

        // תנועה אחורה תמיד מותרת לפי חוקיות המערכת.
        return TaskStatusChangeResult.SuccessResult(
            nextStatus,
            $"סטטוס עודכן בהצלחה מ-{currentStatus} ל-{nextStatus}");
    }
}
