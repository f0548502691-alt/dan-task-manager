using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.Json;

namespace DanTaskManager.Services;

public class TaskApplicationService : ITaskApplicationService
{
    private readonly ApplicationDbContext _context;
    private readonly ITaskWorkflowService _workflowService;
    private readonly TaskHandlerFactory _handlerFactory;
    private readonly ITaskTypeValidationService _taskTypeValidationService;
    private readonly ILogger<TaskApplicationService> _logger;

    public TaskApplicationService(
        ApplicationDbContext context,
        ITaskWorkflowService workflowService,
        TaskHandlerFactory handlerFactory,
        ITaskTypeValidationService taskTypeValidationService,
        ILogger<TaskApplicationService> logger)
    {
        _context = context;
        _workflowService = workflowService;
        _handlerFactory = handlerFactory;
        _taskTypeValidationService = taskTypeValidationService;
        _logger = logger;
    }

    public Task<PagedResult<TaskSummaryDto>> GetAllAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        return QueryTaskSummariesAsync(
            _context.Tasks.AsNoTracking(),
            pageRequest,
            cancellationToken);
    }

    public Task<PagedResult<TaskSummaryDto>> GetByTypeAsync(
        string taskType,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tasks
            .AsNoTracking()
            .Where(t => t.TaskType == taskType);

        return QueryTaskSummariesAsync(query, pageRequest, cancellationToken);
    }

    public Task<PagedResult<TaskSummaryDto>> GetByUserAsync(
        int userId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tasks
            .AsNoTracking()
            .Where(t => t.AssignedToUserId == userId);

        return QueryTaskSummariesAsync(query, pageRequest, cancellationToken);
    }

    public async Task<TaskDetailsDto?> GetByIdAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            return null;
        }

        return MapToTaskDetails(task, task.AssignedToUser);
    }

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

        if (!_handlerFactory.HasHandler(command.TaskType) &&
            !_taskTypeValidationService.HasTaskType(command.TaskType))
        {
            var supportedTaskTypes = GetSupportedTaskTypes();
            return TaskCreationResult.FailureResult(
                $"סוג משימה לא נתמך: {command.TaskType}",
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

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created task {TaskId} ({TaskType}) assigned to user {UserId}",
            task.Id,
            task.TaskType,
            task.AssignedToUserId);

        var createdTask = await GetByIdAsync(task.Id, cancellationToken);
        if (createdTask == null)
        {
            return TaskCreationResult.FailureResult("המשימה נוצרה אך לא ניתן היה לטעון אותה מחדש");
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
        var mutability = await _workflowService.EnsureTaskMutableAsync(taskId, cancellationToken);
        if (!mutability.Success)
        {
            return false;
        }

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
        int nextAssignedToUserId,
        string newDataJson,
        CancellationToken cancellationToken = default)
    {
        return _workflowService.ChangeStatusAsync(taskId, newStatus, nextAssignedToUserId, newDataJson, cancellationToken);
    }

    public Task<WorkflowResult> CloseAsync(
        int taskId,
        string finalNotes,
        CancellationToken cancellationToken = default)
    {
        return _workflowService.CloseTaskAsync(taskId, finalNotes, cancellationToken);
    }

    private async Task<PagedResult<TaskSummaryDto>> QueryTaskSummariesAsync(
        IQueryable<BaseTask> query,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        var page = pageRequest.NormalizedPage;
        var pageSize = pageRequest.NormalizedPageSize;

        var orderedQuery = query.OrderByDescending(t => t.CreatedAt);
        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip(pageRequest.Skip)
            .Take(pageSize)
            .Select(MapToTaskSummary())
            .ToListAsync(cancellationToken);

        return PagedResult<TaskSummaryDto>.Create(items, totalCount, page, pageSize);
    }

    private static Expression<Func<BaseTask, TaskSummaryDto>> MapToTaskSummary()
    {
        return task => new TaskSummaryDto
        {
            Id = task.Id,
            TaskType = task.TaskType,
            CurrentStatus = task.CurrentStatus,
            AssignedToUserId = task.AssignedToUserId,
            Description = task.Description,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            AssignedToUser = task.AssignedToUser == null
                ? null
                : new UserBriefDto
                {
                    Id = task.AssignedToUser.Id,
                    Name = task.AssignedToUser.Name,
                    Email = task.AssignedToUser.Email
                }
        };
    }

    private static TaskDetailsDto MapToTaskDetails(BaseTask task, AppUser? assignedToUser)
    {
        return new TaskDetailsDto
        {
            Id = task.Id,
            TaskType = task.TaskType,
            CurrentStatus = task.CurrentStatus,
            AssignedToUserId = task.AssignedToUserId,
            Description = task.Description,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            CustomFields = ParseCustomFields(task.CustomDataJson),
            AssignedToUser = assignedToUser == null
                ? null
                : new UserBriefDto
                {
                    Id = assignedToUser.Id,
                    Name = assignedToUser.Name,
                    Email = assignedToUser.Email
                }
        };
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
                errorMessage = "customFields חייב להיות אובייקט JSON";
                return false;
            }

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

    private static JsonElement ParseCustomFields(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
        }
    }

    private IReadOnlyCollection<string> GetSupportedTaskTypes()
    {
        var handlerTypes = _handlerFactory.GetRegisteredTaskTypes();
        var metadataTypes = (_taskTypeValidationService as ITaskTypeMetadataService)?
            .GetTaskTypes()
            .Select(taskType => taskType.TaskType)
            ?? Enumerable.Empty<string>();

        return handlerTypes
            .Concat(metadataTypes)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
