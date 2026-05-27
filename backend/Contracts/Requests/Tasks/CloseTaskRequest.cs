namespace DanTaskManager.Contracts.Requests.Tasks;

/// <summary>Request body for <c>POST /api/tasks/{id}/close</c>.</summary>
public class CloseTaskRequest
{
    /// <summary>Assignee for the task once it is closed.</summary>
    public int NextAssignedToUserId { get; set; }

    /// <summary>Final notes; persisted to the task's <c>CustomDataJson.finalNotes</c>.</summary>
    public string FinalNotes { get; set; } = string.Empty;
}
