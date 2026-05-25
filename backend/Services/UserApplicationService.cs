using DanTaskManager.Data;
using DanTaskManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Services;

public class UserApplicationService : IUserApplicationService
{
    private readonly ApplicationDbContext _context;

    public UserApplicationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Tasks)
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<AppUser?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _context.Users
            .AsNoTracking()
            .Include(u => u.Tasks)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<UserCreationResult> CreateAsync(
        UserCreateCommand command,
        CancellationToken cancellationToken = default)
    {
        var emailInUse = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == command.Email, cancellationToken);

        if (emailInUse)
        {
            return UserCreationResult.FailureResult("כתובת האימייל כבר קיימת במערכת");
        }

        var user = new AppUser
        {
            Name = command.Name,
            Email = command.Email,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return UserCreationResult.SuccessResult(user);
    }

    public async Task<IReadOnlyList<BaseTask>> GetUserTasksAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.AssignedToUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId, cancellationToken);
    }
}
