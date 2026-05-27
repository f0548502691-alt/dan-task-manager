using DanTaskManager.Contracts.Requests.Common;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace DanTaskManager.Controllers;

/// <summary>
/// Read-only HTTP endpoints for users and their tasks.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserApplicationService _userService;

    public UsersController(IUserApplicationService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> GetUsers(
        [FromQuery] PaginationQuery pagination)
    {
        var users = await _userService.GetAllAsync(
            pagination.ToPageRequest(),
            HttpContext.RequestAborted);
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDetailsDto>> GetUser(int id)
    {
        var user = await _userService.GetByIdAsync(id, HttpContext.RequestAborted);

        if (user == null)
        {
            throw new ApiNotFoundException("User not found");
        }

        return Ok(user);
    }

    [HttpGet("{id}/tasks")]
    public async Task<ActionResult<PagedResult<TaskSummaryDto>>> GetUserTasks(
        int id,
        [FromQuery] PaginationQuery pagination)
    {
        var userExists = await _userService.ExistsAsync(id, HttpContext.RequestAborted);
        if (!userExists)
        {
            throw new ApiNotFoundException("User does not exist");
        }

        var tasks = await _userService.GetUserTasksAsync(
            id,
            pagination.ToPageRequest(),
            HttpContext.RequestAborted);

        return Ok(tasks);
    }
}
