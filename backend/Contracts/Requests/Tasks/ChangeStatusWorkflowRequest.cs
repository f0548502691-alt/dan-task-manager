using System.Text.Json;

namespace DanTaskManager.Contracts.Requests.Tasks;

/// <summary>
/// Request body for <c>POST /api/tasks/{id}/change-status</c>.
/// </summary>
public class ChangeStatusWorkflowRequest
{
    /// <summary>
    /// Target status. Forward moves must equal current + 1; backward moves
    /// may target any lower status >= 1. The closed status (99) is rejected
    /// here — use the dedicated close endpoint instead.
    /// </summary>
    public int NewStatus { get; set; }

    /// <summary>Assignee for the task after the transition.</summary>
    public int NextAssignedToUserId { get; set; }

    /// <summary>New <c>CustomDataJson</c> contents; must satisfy the rules for the target status.</summary>
    public JsonElement? CustomFields { get; set; }
}
