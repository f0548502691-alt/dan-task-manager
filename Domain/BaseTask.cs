namespace DanTaskManager.Domain;

/// <summary>
/// מחלקת בסיס למשימות בתוך המערכת
/// תומכת במידע משתנה דינאמי דרך CustomDataJson
/// </summary>
public class BaseTask
{
    /// <summary>
    /// מזהה ייחודי של המשימה
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// סוג המשימה (לדוגמה: "Analysis", "Development", "Testing", וכו')
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// סטטוס המשימה (ערך מספרי: 0=לא התחילה, 1=בתהליך, 2=הושלמה, 3=ביוטלה)
    /// </summary>
    public int CurrentStatus { get; set; } = 0;

    /// <summary>
    /// מזהה המשתמש שמבצע את המשימה
    /// </summary>
    public int AssignedToUserId { get; set; }

    /// <summary>
    /// קשר להקצאת משימה למשתמש
    /// </summary>
    public AppUser? AssignedToUser { get; set; }

    /// <summary>
    /// תיאור המשימה
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON מותאם אישית המכיל נתונים משתנים בהתאם לסוג המשימה
    /// לדוגמה: {"priority": "high", "deadline": "2026-06-01", "customField": "value"}
    /// </summary>
    public string CustomDataJson { get; set; } = "{}";

    /// <summary>
    /// תאריך יצירת המשימה
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// תאריך עדכון אחרון
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
