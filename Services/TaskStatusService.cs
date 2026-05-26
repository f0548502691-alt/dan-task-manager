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

        if (nextStatus < 0)
        {
            return TaskStatusChangeResult.FailureResult("סטטוס לא יכול להיות שלילי");
        }

        // קבלת Handler מתאים לפי סוג המשימה
        var handler = _handlerFactory.GetHandler(task.TaskType);

        // אם אין Handler - לא מאפשרים שינוי סטטוס
        if (handler == null)
        {
            _logger.LogWarning("לא נמצא Handler עבור סוג משימה: {TaskType}", task.TaskType);
            return TaskStatusChangeResult.FailureResult(
                $"סוג משימה לא נתמך: {task.TaskType}");
        }

        // בדיקת סטטוס סופי
        if (task.CurrentStatus >= handler.FinalStatus)
        {
            return TaskStatusChangeResult.FailureResult(
                $"המשימה כבר הגיעה לסטטוס סופי ({handler.FinalStatus}), לא ניתן לשנות");
        }

        // בדיקת שינוי לאחור (סטטוס נמוך יותר)
        if (nextStatus < task.CurrentStatus)
        {
            return TaskStatusChangeResult.FailureResult(
                $"לא ניתן להחזיר משימה לסטטוס קודם. סטטוס נוכחי: {task.CurrentStatus}, סטטוס מבוקש: {nextStatus}");
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

}
