using DanTaskManager.Domain;
using DanTaskManager.Persistence;
using System.Text.Json;

namespace DanTaskManager.Services;

public class TaskApplicationService : ITaskApplicationService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITaskWorkflowService _workflowService;
    private readonly ILogger<TaskApplicationService> _logger;

    public TaskApplicationService(
        ITaskRepository taskRepository,
        IUserRepository userRepository,
        ITaskWorkflowService workflowService,
        ILogger<TaskApplicationService> logger)
    {
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _workflowService = workflowService;
        _logger = logger;
    }

    public async Task<PagedResult<TaskSummaryDto>> GetAllAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var page = await _taskRepository.GetPageAsync(pageRequest, cancellationToken: cancellationToken);
        return MapTaskSummaryPage(page);
    }

    public async Task<PagedResult<TaskSummaryDto>> GetByTypeAsync(
        string taskType,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var page = await _taskRepository.GetPageAsync(
            pageRequest,
            taskType: taskType,
            cancellationToken: cancellationToken);
        return MapTaskSummaryPage(page);
    }

    public async Task<PagedResult<TaskSummaryDto>> GetByUserAsync(
        int userId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var page = await _taskRepository.GetPageAsync(
            pageRequest,
            assignedToUserId: userId,
            cancellationToken: cancellationToken);
        return MapTaskSummaryPage(page);
    }

    public async Task<TaskDetailsDto?> GetByIdAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdReadOnlyAsync(taskId, cancellationToken);
        return task == null ? null : TaskDtoMappings.ToTaskDetailsDto(task);
    }

    public Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _userRepository.ExistsAsync(userId, cancellationToken);
    }

    public async Task<TaskCreationResult> CreateAsync(
        TaskCreateCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!await UserExistsAsync(command.AssignedToUserId, cancellationToken))
        {
            return TaskCreationResult.FailureResult("User does not exist");
        }

        if (!TryNormalizeJson(command.CustomDataJson, out var normalizedJson, out var jsonError))
        {
            return TaskCreationResult.FailureResult(jsonError);
        }

        if (!WorkflowConstants.IsSupportedTaskType(command.TaskType))
        {
            var supportedTaskTypes = GetSupportedTaskTypes();
            return TaskCreationResult.FailureResult(
                $"Unsupported task type: {command.TaskType}",
                supportedTaskTypes);
        }

        var task = new BaseTask
        {
            TaskType = command.TaskType,
            Description = command.Description,
            AssignedToUserId = command.AssignedToUserId,
            CurrentStatus = WorkflowConstants.CreatedStatus,
            CustomDataJson = normalizedJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _taskRepository.Add(task);
        await _taskRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created task {TaskId} ({TaskType}) assigned to user {UserId}",
            task.Id,
            task.TaskType,
            task.AssignedToUserId);

        var createdTask = await GetByIdAsync(task.Id, cancellationToken);
        if (createdTask == null)
        {
            return TaskCreationResult.FailureResult("Task was created but could not be loaded afterward");
        }

        return TaskCreationResult.SuccessResult(createdTask);
    }

    public async Task<bool> UpdateDescriptionAsync(
        int taskId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var mutability = await _workflowService.EnsureTaskMutableAsync(taskId, cancellationToken);
        if (!mutability.Success)
        {
            return false;
        }

        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            task.Description = description;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _taskRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var mutability = await _workflowService.EnsureTaskMutableAsync(taskId, cancellationToken);
        if (!mutability.Success)
        {
            return false;
        }

        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            return false;
        }

        _taskRepository.Remove(task);
        await _taskRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        int nextAssignedToUserId,
        string newDataJson,
        CancellationToken cancellationToken = default)
    {
        return _workflowService.ChangeStatusAsync(taskId, newStatus, nextAssignedToUserId, newDataJson, cancellationToken);
    }

    public Task<WorkflowResult> CloseAsync(
        int taskId,
        int nextAssignedToUserId,
        string finalNotes,
        CancellationToken cancellationToken = default)
    {
        return _workflowService.CloseTaskAsync(taskId, nextAssignedToUserId, finalNotes, cancellationToken);
    }

    private static PagedResult<TaskSummaryDto> MapTaskSummaryPage(PagedResult<BaseTask> page)
    {
        var items = page.Items
            .Select(TaskDtoMappings.ToTaskSummaryDto)
            .ToList();

        return PagedResult<TaskSummaryDto>.Create(items, page.TotalCount, page.Page, page.PageSize);
    }

    private static bool TryNormalizeJson(
        string? rawJson,
        out string normalizedJson,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var candidate = string.IsNullOrWhiteSpace(rawJson) ? "{}" : rawJson;

        try
        {
            using var doc = JsonDocument.Parse(candidate);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                normalizedJson = "{}";
                errorMessage = "customFields must be a JSON object";
                return false;
            }

            normalizedJson = doc.RootElement.GetRawText();
            return true;
        }
        catch (JsonException ex)
        {
            normalizedJson = "{}";
            errorMessage = $"Invalid JSON payload: {ex.Message}";
            return false;
        }
    }

    private IReadOnlyCollection<string> GetSupportedTaskTypes()
    {
        return WorkflowConstants.SupportedTaskTypes
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
