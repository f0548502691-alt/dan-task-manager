using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;

namespace DanTaskManager.Services;

/// <summary>
/// ממשק לשירות ניהול סטטוסים של משימות
/// </summary>
public interface ITaskStatusService
{
    /// <summary>
    /// וולידציה ושינוי סטטוס של משימה
    /// </summary>
    /// <param name="task">המשימה לעדכון</param>
    /// <param name="nextStatus">הסטטוס הבא</param>
    /// <param name="newDataJson">JSON חדש של הנתונים המותאמים</param>
    /// <returns>תוצאה עם סטטוס הצלחה והודעה</returns>
    TaskStatusChangeResult ValidateAndChangeStatus(
        BaseTask task,
        int nextStatus,
        string newDataJson);

    /// <summary>
    /// קבלת סטטוס סופי עבור סוג משימה מסוים
    /// </summary>
    int? GetFinalStatus(string taskType);
}

/// <summary>
/// תוצאה של שינוי סטטוס
/// </summary>
public class TaskStatusChangeResult
{
    /// <summary>
    /// האם השינוי בוצע בהצלחה
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// הודעה (שגיאה או הצלחה)
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// הסטטוס החדש (אם בוצע בהצלחה)
    /// </summary>
    public int? NewStatus { get; set; }

    public static TaskStatusChangeResult SuccessResult(int newStatus, string message = "") =>
        new() { Success = true, NewStatus = newStatus, Message = message };

    public static TaskStatusChangeResult FailureResult(string message) =>
        new() { Success = false, Message = message };
}
