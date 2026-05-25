namespace DanTaskManager.Domain;

/// <summary>
/// ייצוג משתמש בתוך המערכת
/// </summary>
public class AppUser
{
    /// <summary>
    /// מזהה ייחודי של המשתמש
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// שם המשתמש
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// דוא"ל המשתמש
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// תאריך יצירת חשבון
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// קשר לביצוע: משתמש יכול לבצע משימות רבות
    /// </summary>
    public ICollection<BaseTask> Tasks { get; set; } = new List<BaseTask>();
}
