namespace DanTaskManager.Contracts.Requests.Tasks;

/// <summary>
/// בקשה לסגירת משימה
/// </summary>
public class CloseTaskRequest
{
    /// <summary>
    /// המשתמש שאליו המשימה תוקצה בעת הסגירה
    /// </summary>
    public int NextAssignedToUserId { get; set; }

    /// <summary>
    /// הערות סופיות על המשימה
    /// </summary>
    public string FinalNotes { get; set; } = string.Empty;
}
