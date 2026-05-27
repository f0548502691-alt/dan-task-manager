using System.Text.Json;

namespace DanTaskManager.Contracts.Requests.Tasks;

/// <summary>
/// בקשה לשינוי סטטוס עם כללי Workflow
/// </summary>
public class ChangeStatusWorkflowRequest
{
    /// <summary>
    /// הסטטוס החדש (תנועה קדימה: בדיוק +1, תנועה אחורה: לכל סטטוס נמוך)
    /// </summary>
    public int NewStatus { get; set; }

    /// <summary>
    /// המשתמש שאליו המשימה תוקצה לאחר שינוי הסטטוס
    /// </summary>
    public int NextAssignedToUserId { get; set; }

    /// <summary>
    /// customFields חדשים עם נתונים מעודכנים
    /// </summary>
    public JsonElement? CustomFields { get; set; }
}
