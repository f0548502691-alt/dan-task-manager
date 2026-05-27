using System.Text.Json;

namespace DanTaskManager.Contracts.Requests.Tasks;

/// <summary>Request body for <c>POST /api/tasks</c>.</summary>
public class CreateTaskRequest
{
    /// <summary>Task-type code (e.g. <c>Procurement</c>, <c>Development</c>, <c>Marketing</c>).</summary>
    public string TaskType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Initial assignee user id.</summary>
    public int AssignedToUserId { get; set; }

    /// <summary>Initial <c>CustomDataJson</c> contents; usually empty at creation time.</summary>
    public JsonElement? CustomFields { get; set; }
}
