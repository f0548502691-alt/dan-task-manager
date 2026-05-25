using DanTaskManager.Data;
using DanTaskManager.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Controllers;

/// <summary>
/// Controller לניהול משתמשים
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(ApplicationDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// קבלת כל המשתמשים
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppUser>>> GetUsers()
    {
        var users = await _context.Users
            .Include(u => u.Tasks)
            .ToListAsync();
        
        return Ok(users);
    }

    /// <summary>
    /// קבלת משתמש לפי ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AppUser>> GetUser(int id)
    {
        var user = await _context.Users
            .Include(u => u.Tasks)
            .FirstOrDefaultAsync(u => u.Id == id);

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
    public async Task<ActionResult<AppUser>> CreateUser(CreateUserRequest request)
    {
        var user = new AppUser
        {
            Name = request.Name,
            Email = request.Email
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    /// <summary>
    /// קבלת משימות של משתמש מסוים
    /// </summary>
    [HttpGet("{id}/tasks")]
    public async Task<ActionResult<IEnumerable<BaseTask>>> GetUserTasks(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound("משתמש לא קיים");
        }

        var tasks = await _context.Tasks
            .Where(t => t.AssignedToUserId == id)
            .ToListAsync();

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
