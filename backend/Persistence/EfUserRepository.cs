using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DanTaskManager.Persistence;

public class EfUserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public EfUserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<UserSummaryDto>> GetPageAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var page = pageRequest.NormalizedPage;
        var pageSize = pageRequest.NormalizedPageSize;

        var query = _context.Users.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(user => user.Name)
            .Skip(pageRequest.Skip)
            .Take(pageSize)
            .Select(ToUserSummary())
            .ToListAsync(cancellationToken);

        return PagedResult<UserSummaryDto>.Create(users, totalCount, page, pageSize);
    }

    public Task<UserDetailsDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(ToUserDetails())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _context.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);
    }

    private static Expression<Func<AppUser, UserSummaryDto>> ToUserSummary()
    {
        return user => new UserSummaryDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            TasksCount = user.Tasks.Count,
            OpenTasksCount = user.Tasks.Count(task => task.CurrentStatus != WorkflowConstants.ClosedStatus)
        };
    }

    private static Expression<Func<AppUser, UserDetailsDto>> ToUserDetails()
    {
        return user => new UserDetailsDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            TasksCount = user.Tasks.Count,
            OpenTasksCount = user.Tasks.Count(task => task.CurrentStatus != WorkflowConstants.ClosedStatus)
        };
    }
}
