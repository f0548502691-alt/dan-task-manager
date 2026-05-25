using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DanTaskManager.Services;

public class TaskApplicationService : ITaskApplicationService
{
    private readonly ApplicationDbContext _context;
    private readonly ITaskWorkflowService _workflowService;
    private readonly TaskHandlerFactory _handlerFactory;
    private readonly ILogger<TaskApplicationService> _logger;

    public TaskApplicationService(
        ApplicationDbContext context,
        ITaskWorkflowService workflowService,
        TaskHandlerFactory handlerFactory,
        ILogger<TaskApplicationService> logger)
    {
        _context = context;
        _workflowService = workflowService;
        _handlerFactory = handlerFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BaseTask>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BaseTask>> GetByTypeAsync(
        string taskType,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TaskType == taskType)
            .Include(t => t.AssignedToUser)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BaseTask>> GetOpenByUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await _workflowService.GetUserTasksAsync(userId, cancellationToken);
        return tasks.ToList();
    }

    public Task<BaseTask?> GetByIdAsync(int taskId, CancellationToken cancellationToken = default)
        => _workflowService.GetTaskAsync(taskId, cancellationToken);

    public Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<TaskCreationResult> CreateAsync(
        TaskCreateCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!await UserExistsAsync(command.AssignedToUserId, cancellationToken))
        {
            return TaskCreationResult.FailureResult("משתמש לא קיים");
        }

        if (!TryNormalizeJson(command.CustomDataJson, out var normalizedJson, out var jsonError))
        {
            return TaskCreationResult.FailureResult(jsonError);
        }

        if (!_handlerFactory.HasHandler(command.TaskType))
        {
            _logger.LogWarning(
                "Creating task with task type {TaskType} without dedicated handler",
                command.TaskType);
        }

        var task = new BaseTask
        {
            TaskType = command.TaskType,
            Description = command.Description,
            AssignedToUserId = command.AssignedToUserId,
            CurrentStatus = 0,
            CustomDataJson = normalizedJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created task {TaskId} ({TaskType}) assigned to user {UserId}",
            task.Id,
            task.TaskType,
            task.AssignedToUserId);

        return TaskCreationResult.SuccessResult(task);
    }

    public async Task<bool> UpdateDescriptionAsync(
        int taskId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks.FindAsync(new object[] { taskId }, cancellationToken);
        if (task == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            task.Description = description;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks.FindAsync(new object[] { taskId }, cancellationToken);
        if (task == null)
        {
            return false;
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        string newDataJson,
        CancellationToken cancellationToken = default)
    {
        return _workflowService.ChangeStatusAsync(taskId, newStatus, newDataJson, cancellationToken);
    }

    public Task<WorkflowResult> CloseAsync(
        int taskId,
        string finalNotes,
        CancellationToken cancellationToken = default)
    {
        return _workflowService.CloseTaskAsync(taskId, finalNotes, cancellationToken);
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
            normalizedJson = doc.RootElement.GetRawText();
            return true;
        }
        catch (JsonException ex)
        {
            normalizedJson = "{}";
            errorMessage = $"JSON לא תקין: {ex.Message}";
            return false;
        }
    }
}
