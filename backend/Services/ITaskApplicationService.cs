namespace DanTaskManager.Services;

public interface ITaskApplicationService
{
    Task<PagedResult<TaskSummaryDto>> GetAllAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);
    Task<PagedResult<TaskSummaryDto>> GetByTypeAsync(
        string taskType,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);
    Task<PagedResult<TaskSummaryDto>> GetByUserAsync(
        int userId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);
    Task<TaskDetailsDto?> GetByIdAsync(int taskId, CancellationToken cancellationToken = default);
    Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default);
    Task<TaskCreationResult> CreateAsync(TaskCreateCommand command, CancellationToken cancellationToken = default);
    Task<bool> UpdateDescriptionAsync(int taskId, string? description, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int taskId, CancellationToken cancellationToken = default);
    Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        int nextAssignedToUserId,
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
    public TaskDetailsDto? CreatedTask { get; init; }

    public static TaskCreationResult SuccessResult(TaskDetailsDto task)
        => new() { Success = true, CreatedTask = task };

    public static TaskCreationResult FailureResult(string message)
        => new() { Success = false, Message = message };
}
