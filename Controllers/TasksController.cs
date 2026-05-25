using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DanTaskManager.Controllers;

/// <summary>
/// Controller לניהול משימות עם Workflow
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITaskStatusService _taskStatusService;
    private readonly ITaskWorkflowService _workflowService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        ApplicationDbContext context,
        ITaskStatusService taskStatusService,
        ITaskWorkflowService workflowService,
        ILogger<TasksController> logger)
    {
        _context = context;
        _taskStatusService = taskStatusService;
        _workflowService = workflowService;
        _logger = logger;
    }

    /// <summary>
    /// קבלת כל המשימות
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BaseTask>>> GetTasks()
    {
        var tasks = await _context.Tasks
            .Include(t => t.AssignedToUser)
            .ToListAsync();
        
        return Ok(tasks);
    }

    /// <summary>
    /// קבלת משימה לפי ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<BaseTask>> GetTask(int id)
    {
        var task = await _workflowService.GetTaskAsync(id);
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
    public async Task<ActionResult<IEnumerable<BaseTask>>> GetTasksByType(string taskType)
    {
        var tasks = await _context.Tasks
            .Include(t => t.AssignedToUser)
            .Where(t => t.TaskType == taskType)
            .ToListAsync();

        return Ok(tasks);
    }

    /// <summary>
    /// קבלת משימות של משתמש מסוים (לא סגורות)
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<BaseTask>>> GetUserTasks(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound("משתמש לא קיים");
        }

        var tasks = await _workflowService.GetUserTasksAsync(userId);
        return Ok(tasks);
    }

    /// <summary>
    /// יצירת משימה חדשה
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BaseTask>> CreateTask(CreateTaskRequest request)
    {
        // וולידציה בסיסית
        if (string.IsNullOrWhiteSpace(request.TaskType))
        {
            return BadRequest(new { error = "TaskType נדרש" });
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { error = "Description נדרש" });
        }

        // בדיקה שהמשתמש קיים
        var user = await _context.Users.FindAsync(request.AssignedToUserId);
        if (user == null)
        {
            return BadRequest(new { error = "משתמש לא קיים" });
        }

        var task = new BaseTask
        {
            TaskType = request.TaskType,
            Description = request.Description,
            AssignedToUserId = request.AssignedToUserId,
            CurrentStatus = 0, // תמיד מתחיל בסטטוס 0
            CustomDataJson = request.CustomDataJson ?? "{}"
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "משימה חדשה יצרה: {TaskId}, סוג: {TaskType}, משתמש: {UserId}",
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
        if (string.IsNullOrEmpty(request.NewDataJson))
        {
            return BadRequest(new { error = "NewDataJson נדרש" });
        }

        var result = await _workflowService.ChangeStatusAsync(
            id,
            request.NewStatus,
            request.NewDataJson);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Message });
        }

        _logger.LogInformation(
            "סטטוס משימה {TaskId} שונה ל-{NewStatus}",
            id,
            result.NewStatus);

        return Ok(new
        {
            success = true,
            message = result.Message,
            newStatus = result.NewStatus,
            task = result.UpdatedTask
        });
    }

    /// <summary>
    /// סגירת משימה (סטטוס סופי = 99)
    /// לא ניתן לשנות משימה סגורה
    /// </summary>
    [HttpPost("{id}/close")]
    public async Task<IActionResult> CloseTask(int id, CloseTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FinalNotes))
        {
            return BadRequest(new { error = "FinalNotes נדרש" });
        }

        var result = await _workflowService.CloseTaskAsync(id, request.FinalNotes);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Message });
        }

        _logger.LogInformation(
            "משימה {TaskId} סגורה עם הערות: {Notes}",
            id,
            request.FinalNotes);

        return Ok(new
        {
            success = true,
            message = result.Message,
            task = result.UpdatedTask
        });
    }

    /// <summary>
    /// עדכון משימה קיימת (בדיקה בסיסית)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(int id, UpdateTaskRequest request)
    {
        var task = await _context.Tasks.FindAsync(id);

        if (task == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(request.Description))
            task.Description = request.Description;

        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// מחיקת משימה
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var task = await _context.Tasks.FindAsync(id);

        if (task == null)
        {
            return NotFound();
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation("משימה {TaskId} נמחקה", id);

        return NoContent();
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
    /// JSON מותאם עם נתונים ספציפיים לסוג המשימה
    /// </summary>
    public string? CustomDataJson { get; set; }
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
    /// JSON חדש עם נתונים מעודכנים
    /// </summary>
    public string NewDataJson { get; set; } = "{}";
}

/// <summary>
/// בקשה לסגירת משימה
/// </summary>
public class CloseTaskRequest
{
    /// <summary>
    /// הערות סופיות על המשימה
    /// </summary>
    public string FinalNotes { get; set; } = string.Empty;
}
