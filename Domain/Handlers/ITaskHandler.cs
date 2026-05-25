namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// ממשק Strategy למטפלי משימות שונים
/// כל סוג משימה יכול להיות בעל לוגיקה וולידציה שונה בהתאם לסטטוס
/// </summary>
public interface ITaskHandler
{
    /// <summary>
    /// סוג המשימה שעבורו מטפל זה אחראי
    /// </summary>
    string TaskType { get; }

    /// <summary>
    /// הסטטוס הסופי של המשימה (לא יכול להעבור אותו)
    /// </summary>
    int FinalStatus { get; }

    /// <summary>
    /// ולידציה של שינוי סטטוס עם בדיקת הנתונים המותאמים
    /// </summary>
    /// <param name="currentDataJson">JSON עכשווי של הנתונים המותאמים</param>
    /// <param name="currentStatus">הסטטוס הנוכחי</param>
    /// <param name="nextStatus">הסטטוס הבא</param>
    /// <param name="newDataJson">JSON חדש של הנתונים המותאמים</param>
    /// <returns>תוצאה עם סטטוס הצלחה והודעה</returns>
    ValidationResult ValidateStatusChange(
        string currentDataJson,
        int currentStatus,
        int nextStatus,
        string newDataJson);
}

/// <summary>
/// תוצאה של וולידציה
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// האם הוולידציה עברה בהצלחה
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// הודעת שגיאה (אם רלוונטי)
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// יצירת תוצאה של הצלחה
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// יצירת תוצאת שגיאה
    /// </summary>
    public static ValidationResult Failure(string message) =>
        new() { IsValid = false, Message = message };
}
