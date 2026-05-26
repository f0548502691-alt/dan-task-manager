using DanTaskManager.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace DanTaskManager.Controllers;

/// <summary>
/// Controller לניהול משתמשים
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserApplicationService _userService;
    private readonly IValidator<CreateUserRequest> _createUserValidator;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserApplicationService userService,
        IValidator<CreateUserRequest> createUserValidator,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _createUserValidator = createUserValidator;
        _logger = logger;
    }

    /// <summary>
    /// קבלת כל המשתמשים
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> GetUsers(
        [FromQuery] PaginationQuery pagination)
    {
        var users = await _userService.GetAllAsync(
            pagination.ToPageRequest(),
            HttpContext.RequestAborted);
        return Ok(users);
    }

    /// <summary>
    /// קבלת משתמש לפי ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDetailsDto>> GetUser(int id)
    {
        var user = await _userService.GetByIdAsync(id, HttpContext.RequestAborted);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>
    /// יצירת משתמש חדש
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserDetailsDto>> CreateUser(CreateUserRequest request)
    {
        var validation = await _createUserValidator.ValidateAsync(request, HttpContext.RequestAborted);
        if (!validation.IsValid)
        {
            return BadRequest(new
            {
                error = string.Join("; ", validation.Errors
                    .Select(e => e.ErrorMessage)
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Distinct())
            });
        }

        var result = await _userService.CreateAsync(
            new UserCreateCommand(request.Name, request.Email),
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Message });
        }

        var user = result.CreatedUser!;

        _logger.LogInformation("משתמש חדש נוצר: {UserId} ({Email})", user.Id, user.Email);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    /// <summary>
    /// קבלת משימות של משתמש מסוים
    /// </summary>
    [HttpGet("{id}/tasks")]
    public async Task<ActionResult<PagedResult<TaskSummaryDto>>> GetUserTasks(
        int id,
        [FromQuery] PaginationQuery pagination)
    {
        var userExists = await _userService.ExistsAsync(id, HttpContext.RequestAborted);
        if (!userExists)
        {
            return NotFound("משתמש לא קיים");
        }

        var tasks = await _userService.GetUserTasksAsync(
            id,
            pagination.ToPageRequest(),
            HttpContext.RequestAborted);

        return Ok(tasks);
    }
}

/// <summary>
/// בקשה ליצירת משתמש חדש
/// </summary>
public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
