using DanTaskManager.Data;
using DanTaskManager.Domain;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DanTaskManager.Services;

public class UserApplicationService : IUserApplicationService
{
    private readonly ApplicationDbContext _context;

    public UserApplicationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<UserSummaryDto>> GetAllAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var page = pageRequest.NormalizedPage;
        var pageSize = pageRequest.NormalizedPageSize;

        var query = _context.Users
            .AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(u => u.Name)
            .Skip(pageRequest.Skip)
            .Take(pageSize)
            .Select(MapToUserSummary())
            .ToListAsync(cancellationToken);

        return PagedResult<UserSummaryDto>.Create(users, totalCount, page, pageSize);
    }

    public Task<UserDetailsDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(MapToUserDetails())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<TaskSummaryDto>> GetUserTasksAsync(
        int userId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var page = pageRequest.NormalizedPage;
        var pageSize = pageRequest.NormalizedPageSize;

        var query = _context.Tasks
            .AsNoTracking()
            .Where(t => t.AssignedToUserId == userId);
        var totalCount = await query.CountAsync(cancellationToken);

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(pageRequest.Skip)
            .Take(pageSize)
            .Select(TaskDtoMappings.ToTaskSummary())
            .ToListAsync(cancellationToken);

        return PagedResult<TaskSummaryDto>.Create(tasks, totalCount, page, pageSize);
    }

    public Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId, cancellationToken);
    }

    private static Expression<Func<AppUser, UserSummaryDto>> MapToUserSummary()
    {
        return user => new UserSummaryDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            TasksCount = user.Tasks.Count,
            OpenTasksCount = user.Tasks.Count(t => t.CurrentStatus != WorkflowConstants.ClosedStatus)
        };
    }

    private static Expression<Func<AppUser, UserDetailsDto>> MapToUserDetails()
    {
        return user => new UserDetailsDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            TasksCount = user.Tasks.Count,
            OpenTasksCount = user.Tasks.Count(t => t.CurrentStatus != WorkflowConstants.ClosedStatus)
        };
    }

}
