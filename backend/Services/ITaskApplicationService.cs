using DanTaskManager.Domain;

namespace DanTaskManager.Services;

public interface ITaskApplicationService
{
    Task<IReadOnlyList<BaseTask>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BaseTask>> GetByTypeAsync(string taskType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BaseTask>> GetOpenByUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<BaseTask?> GetByIdAsync(int taskId, CancellationToken cancellationToken = default);
    Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default);
    Task<TaskCreationResult> CreateAsync(TaskCreateCommand command, CancellationToken cancellationToken = default);
    Task<bool> UpdateDescriptionAsync(int taskId, string? description, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int taskId, CancellationToken cancellationToken = default);
    Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        string newDataJson,
        CancellationToken cancellationToken = default);
    Task<WorkflowResult> CloseAsync(
        int taskId,
        string finalNotes,
        CancellationToken cancellationToken = default);
}

public record TaskCreateCommand(
    string TaskType,
    string Description,
    int AssignedToUserId,
    string CustomDataJson);

public class TaskCreationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public BaseTask? CreatedTask { get; init; }

    public static TaskCreationResult SuccessResult(BaseTask task)
        => new() { Success = true, CreatedTask = task };

    public static TaskCreationResult FailureResult(string message)
        => new() { Success = false, Message = message };
}
