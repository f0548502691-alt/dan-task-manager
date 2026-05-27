namespace DanTaskManager.Contracts.Requests.Tasks;

/// <summary>Request body for <c>PUT /api/tasks/{id}</c>.</summary>
public class UpdateTaskRequest
{
    /// <summary>New description; null leaves the existing value untouched.</summary>
    public string? Description { get; set; }
}
