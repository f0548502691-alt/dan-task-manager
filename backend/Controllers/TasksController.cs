using DanTaskManager.Application.Tasks.ChangeTaskStatus;
using DanTaskManager.Application.Tasks.CloseTask;
using DanTaskManager.Application.Tasks.CreateTask;
using DanTaskManager.Application.Tasks.DeleteTask;
using DanTaskManager.Application.Tasks.GetAllTasks;
using DanTaskManager.Application.Tasks.GetTaskById;
using DanTaskManager.Application.Tasks.GetTasksByType;
using DanTaskManager.Application.Tasks.GetTasksByUser;
using DanTaskManager.Application.Tasks.UpdateTaskDescription;
using DanTaskManager.Application.Tasks.UserExists;
using DanTaskManager.Contracts.Requests.Common;
using DanTaskManager.Contracts.Requests.Tasks;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DanTaskManager.Controllers;

/// <summary>
/// Task-management HTTP endpoints. All write paths run through MediatR
/// commands so the controller stays thin; workflow rules live in the
/// services layer.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly IValidator<CreateTaskRequest> _createTaskValidator;
    private readonly IValidator<ChangeStatusWorkflowRequest> _changeStatusValidator;
    private readonly IValidator<CloseTaskRequest> _closeTaskValidator;
    private readonly IMediator _mediator;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        IValidator<CreateTaskRequest> createTaskValidator,
        IValidator<ChangeStatusWorkflowRequest> changeStatusValidator,
        IValidator<CloseTaskRequest> closeTaskValidator,
        IMediator mediator,
        ILogger<TasksController> logger)
    {
        _createTaskValidator = createTaskValidator;
        _changeStatusValidator = changeStatusValidator;
        _closeTaskValidator = closeTaskValidator;
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskSummaryDto>>> GetTasks(
        [FromQuery] PaginationQuery pagination)
    {
        var tasks = await _mediator.Send(
            new GetAllTasksQuery(pagination.ToPageRequest()),
            HttpContext.RequestAborted);
        return Ok(tasks);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskDetailsDto>> GetTask(int id)
    {
        var task = await _mediator.Send(new GetTaskByIdQuery(id), HttpContext.RequestAborted);
        if (task == null)
        {
            throw new ApiNotFoundException("Task not found");
        }

        return Ok(task);
    }

    [HttpGet("byType/{taskType}")]
    public async Task<ActionResult<PagedResult<TaskSummaryDto>>> GetTasksByType(
        string taskType,
        [FromQuery] PaginationQuery pagination)
    {
        var tasks = await _mediator.Send(
            new GetTasksByTypeQuery(taskType, pagination.ToPageRequest()),
            HttpContext.RequestAborted);

        return Ok(tasks);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<PagedResult<TaskSummaryDto>>> GetUserTasks(
        int userId,
        [FromQuery] PaginationQuery pagination)
    {
        var userExists = await _mediator.Send(new UserExistsQuery(userId), HttpContext.RequestAborted);
        if (!userExists)
        {
            throw new ApiNotFoundException("User does not exist");
        }

        var tasks = await _mediator.Send(
            new GetTasksByUserQuery(userId, pagination.ToPageRequest()),
            HttpContext.RequestAborted);
        return Ok(tasks);
    }

    [HttpPost]
    public async Task<ActionResult<TaskDetailsDto>> CreateTask(CreateTaskRequest request)
    {
        var validation = await _createTaskValidator.ValidateAsync(request, HttpContext.RequestAborted);
        if (!validation.IsValid)
        {
            throw new ApiValidationException(BuildValidationErrorMessage(validation.Errors.Select(e => e.ErrorMessage)));
        }

        var result = await _mediator.Send(
            new CreateTaskCommand(
                request.TaskType,
                request.Description,
                request.AssignedToUserId,
                ExtractCustomFieldsJson(request.CustomFields)),
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            if (result.SupportedTaskTypes.Count > 0)
            {
                var supportedTypes = string.Join(", ", result.SupportedTaskTypes);
                throw new ApiValidationException(
                    $"{result.Message}. Supported task types: {supportedTypes}",
                    "task_type_validation_failed");
            }

            throw new ApiValidationException(result.Message, "task_creation_failed");
        }

        var task = result.CreatedTask!;

        _logger.LogInformation(
            "Created task: {TaskId}, type: {TaskType}, user: {UserId}",
            task.Id,
            task.TaskType,
            task.AssignedToUserId);

        return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
    }

    /// <summary>
    /// Change a task's status. Forward movement is restricted to exactly +1;
    /// backward movement may target any lower status. Reaching the closed
    /// status (99) is not allowed here — use <c>POST /{id}/close</c>.
    /// </summary>
    [HttpPost("{id}/change-status")]
    public async Task<IActionResult> ChangeStatusWorkflow(int id, ChangeStatusWorkflowRequest request)
    {
        var validation = await _changeStatusValidator.ValidateAsync(request, HttpContext.RequestAborted);
        if (!validation.IsValid)
        {
            throw new ApiValidationException(BuildValidationErrorMessage(validation.Errors.Select(e => e.ErrorMessage)));
        }

        var result = await _mediator.Send(
            new ChangeTaskStatusCommand(
                id,
                request.NewStatus,
                request.NextAssignedToUserId,
                ExtractCustomFieldsJson(request.CustomFields)),
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            throw new WorkflowValidationException(result.Message, result.Code);
        }

        _logger.LogInformation(
            "Task {TaskId} status changed to {NewStatus}",
            id,
            result.NewStatus);

        var updatedTask = await _mediator.Send(new GetTaskByIdQuery(id), HttpContext.RequestAborted);

        return Ok(new
        {
            success = true,
            message = result.Message,
            newStatus = result.NewStatus,
            task = updatedTask
        });
    }

    /// <summary>
    /// Close a task (move it to status 99). Only valid from the task type's
    /// final status; a closed task becomes immutable.
    /// </summary>
    [HttpPost("{id}/close")]
    public async Task<IActionResult> CloseTask(int id, CloseTaskRequest request)
    {
        var validation = await _closeTaskValidator.ValidateAsync(request, HttpContext.RequestAborted);
        if (!validation.IsValid)
        {
            throw new ApiValidationException(BuildValidationErrorMessage(validation.Errors.Select(e => e.ErrorMessage)));
        }

        var result = await _mediator.Send(
            new CloseTaskCommand(id, request.NextAssignedToUserId, request.FinalNotes),
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            throw new WorkflowValidationException(result.Message, result.Code);
        }

        _logger.LogInformation(
            "Task {TaskId} closed with notes: {Notes}",
            id,
            request.FinalNotes);

        var updatedTask = await _mediator.Send(new GetTaskByIdQuery(id), HttpContext.RequestAborted);

        return Ok(new
        {
            success = true,
            message = result.Message,
            task = updatedTask
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(int id, UpdateTaskRequest request)
    {
        var updated = await _mediator.Send(
            new UpdateTaskDescriptionCommand(id, request.Description),
            HttpContext.RequestAborted);
        if (!updated)
        {
            var task = await _mediator.Send(new GetTaskByIdQuery(id), HttpContext.RequestAborted);
            if (task == null)
            {
                throw new ApiNotFoundException("Task not found");
            }

            throw new WorkflowValidationException("Closed tasks are immutable and cannot be updated");
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var deleted = await _mediator.Send(new DeleteTaskCommand(id), HttpContext.RequestAborted);
        if (!deleted)
        {
            var task = await _mediator.Send(new GetTaskByIdQuery(id), HttpContext.RequestAborted);
            if (task == null)
            {
                throw new ApiNotFoundException("Task not found");
            }

            throw new WorkflowValidationException("Closed tasks are immutable and cannot be deleted");
        }

        _logger.LogInformation("Task {TaskId} deleted", id);

        return NoContent();
    }

    private static string ExtractCustomFieldsJson(JsonElement? customFields)
    {
        if (!customFields.HasValue)
        {
            return "{}";
        }

        return customFields.Value.ValueKind == JsonValueKind.Object
            ? customFields.Value.GetRawText()
            : "{}";
    }

    private static string BuildValidationErrorMessage(IEnumerable<string> errors)
    {
        return string.Join("; ", errors.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct());
    }
}
