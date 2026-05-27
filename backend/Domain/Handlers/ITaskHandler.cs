namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// Strategy contract for a code-backed task type. Implementations validate
/// status transitions and the JSON payload accompanying them; metadata-driven
/// task types do not need to implement this interface.
/// </summary>
public interface ITaskHandler
{
    /// <summary>The task-type code this handler is responsible for.</summary>
    string TaskType { get; }

    /// <summary>The terminal pre-close status for this task type.</summary>
    int FinalStatus { get; }

    /// <summary>
    /// Validate a proposed status transition together with its new JSON payload.
    /// </summary>
    /// <param name="currentDataJson">The task's existing <c>CustomDataJson</c>.</param>
    /// <param name="currentStatus">The task's current status before the change.</param>
    /// <param name="nextStatus">The requested new status.</param>
    /// <param name="newDataJson">The proposed <c>CustomDataJson</c> after the change.</param>
    ValidationResult ValidateStatusChange(
        string currentDataJson,
        int currentStatus,
        int nextStatus,
        string newDataJson);
}

/// <summary>
/// Marker for code-backed task types that should be discovered by DI.
/// Metadata-backed task types do not need handlers registered as a second source of validation.
/// </summary>
public interface IRegisterableTaskHandler : ITaskHandler
{
}

/// <summary>
/// Outcome of a single validation step. Use <see cref="Success"/> when no
/// rule was violated and <see cref="Failure(string)"/> with a human-readable
/// message when one was.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }

    public string Message { get; set; } = string.Empty;

    public static ValidationResult Success() => new() { IsValid = true };

    public static ValidationResult Failure(string message) =>
        new() { IsValid = false, Message = message };
}
