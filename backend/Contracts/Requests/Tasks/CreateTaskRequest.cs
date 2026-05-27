using System.Text.Json;

namespace DanTaskManager.Contracts.Requests.Tasks;

/// <summary>
/// בקשה ליצירת משימה חדשה
/// </summary>
public class CreateTaskRequest
{
    /// <summary>
    /// סוג המשימה (Procurement, Development, וכו')
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// תיאור המשימה
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// ID של המשתמש שמוקצה למשימה
    /// </summary>
    public int AssignedToUserId { get; set; }

    /// <summary>
    /// אובייקט customFields עם נתונים ספציפיים לסוג המשימה
    /// </summary>
    public JsonElement? CustomFields { get; set; }
}
