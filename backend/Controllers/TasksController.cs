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
using DanTaskManager.Domain;
using DanTaskManager.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DanTaskManager.Controllers;

/// <summary>
/// Controller לניהול משימות עם Workflow
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

    /// <summary>
    /// קבלת כל המשימות
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskSummaryDto>>> GetTasks(
        [FromQuery] PaginationQuery pagination)
    {
        var tasks = await _mediator.Send(
            new GetAllTasksQuery(pagination.ToPageRequest()),
            HttpContext.RequestAborted);
        return Ok(tasks);
    }

    /// <summary>
    /// קבלת משימה לפי ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TaskDetailsDto>> GetTask(int id)
    {
        var task = await _mediator.Send(new GetTaskByIdQuery(id), HttpContext.RequestAborted);
        if (task == null)
        {
            return NotFound();
        }

        return Ok(task);
    }

    /// <summary>
    /// קבלת משימות לפי סוג
    /// </summary>
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

    /// <summary>
    /// קבלת משימות של משתמש מסוים
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<PagedResult<TaskSummaryDto>>> GetUserTasks(
        int userId,
        [FromQuery] PaginationQuery pagination)
    {
        var userExists = await _mediator.Send(new UserExistsQuery(userId), HttpContext.RequestAborted);
        if (!userExists)
        {
            return NotFound("User does not exist");
        }

        var tasks = await _mediator.Send(
            new GetTasksByUserQuery(userId, pagination.ToPageRequest()),
            HttpContext.RequestAborted);
        return Ok(tasks);
    }

    /// <summary>
    /// יצירת משימה חדשה
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TaskDetailsDto>> CreateTask(CreateTaskRequest request)
    {
        var validation = await _createTaskValidator.ValidateAsync(request, HttpContext.RequestAborted);
        if (!validation.IsValid)
        {
            return BadRequest(new { error = BuildValidationErrorMessage(validation.Errors.Select(e => e.ErrorMessage)) });
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
                return BadRequest(new
                {
                    error = result.Message,
                    supportedTaskTypes = result.SupportedTaskTypes
                });
            }

            return BadRequest(new { error = result.Message });
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
    /// שינוי סטטוס של משימה עם כללי Workflow
    /// תנועה קדימה: בדיוק +1 סטטוס
    /// תנועה אחורה: לכל סטטוס נמוך יותר
    /// </summary>
    [HttpPost("{id}/change-status")]
    public async Task<IActionResult> ChangeStatusWorkflow(int id, ChangeStatusWorkflowRequest request)
    {
        var validation = await _changeStatusValidator.ValidateAsync(request, HttpContext.RequestAborted);
        if (!validation.IsValid)
        {
            return BadRequest(new { error = BuildValidationErrorMessage(validation.Errors.Select(e => e.ErrorMessage)) });
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
            throw new WorkflowValidationException(result.Message);
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
    /// סגירת משימה (סטטוס סופי = 99)
    /// לא ניתן לשנות משימה סגורה
    /// </summary>
    [HttpPost("{id}/close")]
    public async Task<IActionResult> CloseTask(int id, CloseTaskRequest request)
    {
        var validation = await _closeTaskValidator.ValidateAsync(request, HttpContext.RequestAborted);
        if (!validation.IsValid)
        {
            return BadRequest(new { error = BuildValidationErrorMessage(validation.Errors.Select(e => e.ErrorMessage)) });
        }

        var result = await _mediator.Send(
            new CloseTaskCommand(id, request.NextAssignedToUserId, request.FinalNotes),
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            throw new WorkflowValidationException(result.Message);
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

    /// <summary>
    /// עדכון משימה קיימת (בדיקה בסיסית)
    /// </summary>
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
                return NotFound();
            }

            throw new WorkflowValidationException("Closed tasks are immutable and cannot be updated");
        }

        return NoContent();
    }

    /// <summary>
    /// מחיקת משימה
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var deleted = await _mediator.Send(new DeleteTaskCommand(id), HttpContext.RequestAborted);
        if (!deleted)
        {
            var task = await _mediator.Send(new GetTaskByIdQuery(id), HttpContext.RequestAborted);
            if (task == null)
            {
                return NotFound();
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

/// <summary>
/// בקשה ליצירת משימה חדשה
/// </summary>
public class CreateTaskRequest
{
    /// <summary>
    /// סוג המשימה (Procurement, Development, וכו')
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// תיאור המשימה
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// ID של המשתמש שמוקצה למשימה
    /// </summary>
    public int AssignedToUserId { get; set; }

    /// <summary>
    /// אובייקט customFields עם נתונים ספציפיים לסוג המשימה
    /// </summary>
    public JsonElement? CustomFields { get; set; }
}

/// <summary>
/// בקשה לעדכון משימה
/// </summary>
public class UpdateTaskRequest
{
    /// <summary>
    /// תיאור חדש
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// בקשה לשינוי סטטוס עם כללי Workflow
/// </summary>
public class ChangeStatusWorkflowRequest
{
    /// <summary>
    /// הסטטוס החדש (תנועה קדימה: בדיוק +1, תנועה אחורה: לכל סטטוס נמוך)
    /// </summary>
    public int NewStatus { get; set; }

    /// <summary>
    /// המשתמש שאליו המשימה תוקצה לאחר שינוי הסטטוס
    /// </summary>
    public int NextAssignedToUserId { get; set; }

    /// <summary>
    /// customFields חדשים עם נתונים מעודכנים
    /// </summary>
    public JsonElement? CustomFields { get; set; }
}

/// <summary>
/// בקשה לסגירת משימה
/// </summary>
public class CloseTaskRequest
{
    /// <summary>
    /// המשתמש שאליו המשימה תוקצה בעת הסגירה
    /// </summary>
    public int NextAssignedToUserId { get; set; }

    /// <summary>
    /// הערות סופיות על המשימה
    /// </summary>
    public string FinalNotes { get; set; } = string.Empty;
}
