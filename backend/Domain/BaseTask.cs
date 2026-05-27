namespace DanTaskManager.Domain;

/// <summary>
/// Base entity for every task in the system. Per-type data lives in
/// <see cref="CustomDataJson"/>; validation of that JSON is delegated to the
/// rule provider for the given <see cref="TaskType"/>.
/// </summary>
public class BaseTask
{
    public int Id { get; set; }

    /// <summary>
    /// Task-type code (for example "Procurement", "Development", "Marketing").
    /// Matched case-insensitively against the registered rule providers.
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// Current workflow status. Starts at <see cref="WorkflowConstants.CreatedStatus"/>
    /// and advances by exactly +1 until the task type's final status; closing the
    /// task moves it to <see cref="WorkflowConstants.ClosedStatus"/> (99).
    /// </summary>
    public int CurrentStatus { get; set; } = WorkflowConstants.CreatedStatus;

    public int AssignedToUserId { get; set; }

    public AppUser? AssignedToUser { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Type-specific data as a JSON object. The schema is enforced by the rule
    /// provider for <see cref="TaskType"/>, not by the C# type system, so new
    /// task types can be added without changing this entity.
    /// </summary>
    public string CustomDataJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
