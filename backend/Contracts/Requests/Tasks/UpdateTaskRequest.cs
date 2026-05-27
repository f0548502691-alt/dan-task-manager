namespace DanTaskManager.Contracts.Requests.Tasks;

/// <summary>
/// בקשה לעדכון משימה
/// </summary>
public class UpdateTaskRequest
{
    /// <summary>
    /// תיאור חדש
    /// </summary>
    public string? Description { get; set; }
}
