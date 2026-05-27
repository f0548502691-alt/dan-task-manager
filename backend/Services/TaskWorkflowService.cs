using DanTaskManager.Data;
using DanTaskManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Services;

/// <summary>
/// Workflow-management contract. Enforces the rules that apply to every task
/// (forward-by-one, backward-free, no-jump-to-closed, final-status guard,
/// closed-immutability) and delegates per-type validation to the registered
/// <see cref="ITaskWorkflowRuleProvider"/> implementations.
/// </summary>
public interface ITaskWorkflowService
{
    /// <summary>Advance or rewind a task's status; rejects when general or per-type rules disallow it.</summary>
    Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        int nextAssignedToUserId,
        string newDataJson,
        CancellationToken cancellationToken = default);

    /// <summary>Move a task to the closed status (<see cref="WorkflowConstants.ClosedStatus"/>).</summary>
    Task<WorkflowResult> CloseTaskAsync(
        int taskId,
        int nextAssignedToUserId,
        string finalNotes,
        CancellationToken cancellationToken = default);

    /// <summary>Check whether a non-status mutation is allowed on the task (i.e. it isn't closed).</summary>
    Task<WorkflowResult> EnsureTaskMutableAsync(
        int taskId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<BaseTask>> GetUserTasksAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<BaseTask?> GetTaskAsync(int taskId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a workflow operation. <see cref="Code"/> is a stable string from
/// <see cref="WorkflowErrorCodes"/> (or empty on success) intended for machine
/// consumption — frontend conditionals, integration tests, and the public
/// `code` field of error responses. <see cref="Message"/> is the
/// human-readable description.
/// </summary>
public class WorkflowResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int? NewStatus { get; set; }
    public BaseTask? UpdatedTask { get; set; }

    public static WorkflowResult SuccessResult(int newStatus, BaseTask task, string message = "")
        => new() { Success = true, NewStatus = newStatus, UpdatedTask = task, Message = message };

    public static WorkflowResult FailureResult(string code, string message)
        => new() { Success = false, Code = code, Message = message };
}

/// <summary>
/// Workflow service implementation. Owns the general status-movement rules
/// and delegates per-type validation to <see cref="ITaskWorkflowRuleProvider"/>
/// instances ordered by <see cref="ITaskWorkflowRuleProvider.Priority"/>.
/// </summary>
public class TaskWorkflowService : ITaskWorkflowService
{
    private readonly ApplicationDbContext _context;
    private readonly IReadOnlyList<ITaskWorkflowRuleProvider> _ruleProviders;
    private readonly ILogger<TaskWorkflowService> _logger;

    public TaskWorkflowService(
        ApplicationDbContext context,
        IEnumerable<ITaskWorkflowRuleProvider> ruleProviders,
        ILogger<TaskWorkflowService> logger)
    {
        _context = context;
        _ruleProviders = ruleProviders
            .OrderBy(provider => provider.Priority)
            .ToArray();
        _logger = logger;
    }

    public async Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        int nextAssignedToUserId,
        string newDataJson,
        CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null)
        {
            return WorkflowResult.FailureResult(WorkflowErrorCodes.TaskNotFound, "Task does not exist");
        }

        if (task.CurrentStatus == WorkflowConstants.ClosedStatus)
        {
            return WorkflowResult.FailureResult(
                WorkflowErrorCodes.TaskClosed,
                "Task is closed - status cannot be changed");
        }

        var nextAssigneeExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == nextAssignedToUserId, cancellationToken);
        if (!nextAssigneeExists)
        {
            return WorkflowResult.FailureResult(
                WorkflowErrorCodes.AssigneeNotFound,
                "Next assignee does not exist");
        }

        if (!IsValidJsonPayload(newDataJson))
        {
            return WorkflowResult.FailureResult(
                WorkflowErrorCodes.InvalidJsonPayload,
                "customFields must be a valid JSON object");
        }

        var ruleProvider = ResolveRuleProvider(task.TaskType);
        if (ruleProvider == null)
        {
            _logger.LogWarning(
                "No workflow rule provider found for task type {TaskType} on task {TaskId}",
                task.TaskType,
                taskId);
            return WorkflowResult.FailureResult(
                WorkflowErrorCodes.UnsupportedTaskType,
                $"Unsupported task type: {task.TaskType}");
        }

        var finalStatus = ruleProvider.GetFinalStatus(task.TaskType);

        var movementValidation = ValidateStatusMovement(task, newStatus, finalStatus);
        if (!movementValidation.IsValid)
        {
            return WorkflowResult.FailureResult(movementValidation.Code, movementValidation.Message);
        }

        var validationResult = ruleProvider.ValidateStatusChange(task, newStatus, newDataJson);
        if (!validationResult.IsValid)
        {
            return WorkflowResult.FailureResult(
                WorkflowErrorCodes.FieldValidationFailed,
                validationResult.Message);
        }

        var oldStatus = task.CurrentStatus;
        var oldAssignee = task.AssignedToUserId;
        task.CurrentStatus = newStatus;
        task.AssignedToUserId = nextAssignedToUserId;
        task.CustomDataJson = newDataJson;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} updated from status {OldStatus} to {NewStatus}, assignee {OldAssignee}->{NewAssignee}",
            taskId,
            oldStatus,
            newStatus,
            oldAssignee,
            nextAssignedToUserId);

        return WorkflowResult.SuccessResult(
            newStatus,
            task,
            $"Status updated successfully to {newStatus}");
    }

    public async Task<WorkflowResult> CloseTaskAsync(
        int taskId,
        int nextAssignedToUserId,
        string finalNotes,
        CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null)
        {
            return WorkflowResult.FailureResult(WorkflowErrorCodes.TaskNotFound, "Task does not exist");
        }

        if (task.CurrentStatus == WorkflowConstants.ClosedStatus)
        {
            return WorkflowResult.FailureResult(
                WorkflowErrorCodes.TaskAlreadyClosed,
                "Task is already closed");
        }

        var nextAssigneeExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == nextAssignedToUserId, cancellationToken);
        if (!nextAssigneeExists)
        {
            return WorkflowResult.FailureResult(
                WorkflowErrorCodes.AssigneeNotFound,
                "Next assignee does not exist");
        }

        var ruleProvider = ResolveRuleProvider(task.TaskType);
        var finalStatus = ruleProvider?.GetFinalStatus(task.TaskType);
        if (!finalStatus.HasValue)
        {
            return WorkflowResult.FailureResult(
                WorkflowErrorCodes.UnsupportedTaskType,
                $"Unsupported task type: {task.TaskType}");
        }

        if (task.CurrentStatus != finalStatus.Value)
        {
            return WorkflowResult.FailureResult(
                WorkflowErrorCodes.CloseRequiresFinalStatus,
                $"A {task.TaskType} task can only be closed from final status {finalStatus.Value}");
        }

        var updatedJson = ruleProvider!.BuildCloseData(task, finalNotes);

        var oldAssignee = task.AssignedToUserId;
        task.CurrentStatus = WorkflowConstants.ClosedStatus;
        task.AssignedToUserId = nextAssignedToUserId;
        task.CustomDataJson = updatedJson;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} closed with notes: {Notes}, assignee {OldAssignee}->{NewAssignee}",
            taskId,
            finalNotes,
            oldAssignee,
            nextAssignedToUserId);

        return WorkflowResult.SuccessResult(
            WorkflowConstants.ClosedStatus,
            task,
            "Task closed successfully");
    }

    public async Task<WorkflowResult> EnsureTaskMutableAsync(
        int taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null)
        {
            return WorkflowResult.FailureResult(WorkflowErrorCodes.TaskNotFound, "Task does not exist");
        }

        if (task.CurrentStatus == WorkflowConstants.ClosedStatus)
        {
            return WorkflowResult.FailureResult(
                WorkflowErrorCodes.TaskClosed,
                "Closed task is immutable and cannot be modified");
        }

        return WorkflowResult.SuccessResult(task.CurrentStatus, task);
    }

    public async Task<IEnumerable<BaseTask>> GetUserTasksAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.AssignedToUserId == userId)
            .Include(t => t.AssignedToUser)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<BaseTask?> GetTaskAsync(int taskId, CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
    }

    /// <summary>
    /// Enforce the general status-movement rules: no jumps forward, free movement
    /// backward, no direct moves to <see cref="WorkflowConstants.ClosedStatus"/>,
    /// no exits past the type's final status, no same-status moves.
    /// </summary>
    private StatusMovementValidation ValidateStatusMovement(BaseTask task, int newStatus, int? finalStatus)
    {
        if (newStatus == WorkflowConstants.ClosedStatus)
        {
            return StatusMovementValidation.Invalid(
                WorkflowErrorCodes.CloseViaCloseTaskOnly,
                "Task closing is allowed only via CloseTask");
        }

        if (newStatus < WorkflowConstants.CreatedStatus)
        {
            return StatusMovementValidation.Invalid(
                WorkflowErrorCodes.IllegalStatusTransition,
                $"Status must be {WorkflowConstants.CreatedStatus} or higher");
        }

        if (finalStatus.HasValue && task.CurrentStatus >= finalStatus.Value && newStatus > task.CurrentStatus)
        {
            return StatusMovementValidation.Invalid(
                WorkflowErrorCodes.FinalStatusReached,
                $"Task already reached final status ({finalStatus.Value})");
        }

        if (newStatus > task.CurrentStatus)
        {
            if (newStatus != task.CurrentStatus + 1)
            {
                return StatusMovementValidation.Invalid(
                    WorkflowErrorCodes.IllegalStatusTransition,
                    $"Forward movement must be exactly +1 status. Current status: {task.CurrentStatus}, requested: {newStatus}");
            }
        }
        else if (newStatus < task.CurrentStatus)
        {
            _logger.LogInformation(
                "Backward movement detected for task {TaskId}: {OldStatus} -> {NewStatus}",
                task.Id,
                task.CurrentStatus,
                newStatus);
        }
        else
        {
            return StatusMovementValidation.Invalid(
                WorkflowErrorCodes.SameStatus,
                "New status is identical to current status");
        }

        return StatusMovementValidation.Ok();
    }

    private class StatusMovementValidation
    {
        public bool IsValid { get; private init; }
        public string Code { get; private init; } = string.Empty;
        public string Message { get; private init; } = string.Empty;

        public static StatusMovementValidation Ok() => new() { IsValid = true };

        public static StatusMovementValidation Invalid(string code, string message)
            => new() { IsValid = false, Code = code, Message = message };
    }

    private static bool IsValidJsonPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private ITaskWorkflowRuleProvider? ResolveRuleProvider(string taskType)
    {
        return _ruleProviders.FirstOrDefault(provider => provider.CanHandle(taskType));
    }
}
