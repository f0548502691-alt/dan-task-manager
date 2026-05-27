using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Persistence;

public class EfTaskRepository : ITaskRepository
{
    private readonly ApplicationDbContext _context;

    public EfTaskRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<BaseTask>> GetPageAsync(
        PageRequest pageRequest,
        string? taskType = null,
        int? assignedToUserId = null,
        CancellationToken cancellationToken = default)
    {
        var page = pageRequest.NormalizedPage;
        var pageSize = pageRequest.NormalizedPageSize;

        var query = _context.Tasks
            .AsNoTracking()
            .Include(task => task.AssignedToUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(taskType))
        {
            query = query.Where(task => task.TaskType == taskType);
        }

        if (assignedToUserId.HasValue)
        {
            query = query.Where(task => task.AssignedToUserId == assignedToUserId.Value);
        }

        var orderedQuery = query.OrderByDescending(task => task.CreatedAt);
        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        var items = await orderedQuery
            .Skip(pageRequest.Skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<BaseTask>.Create(items, totalCount, page, pageSize);
    }

    public Task<BaseTask?> GetByIdAsync(int taskId, CancellationToken cancellationToken = default)
    {
        return _context.Tasks.FirstOrDefaultAsync(task => task.Id == taskId, cancellationToken);
    }

    public Task<BaseTask?> GetByIdReadOnlyAsync(int taskId, CancellationToken cancellationToken = default)
    {
        return _context.Tasks
            .AsNoTracking()
            .Include(task => task.AssignedToUser)
            .FirstOrDefaultAsync(task => task.Id == taskId, cancellationToken);
    }

    public async Task<IReadOnlyList<BaseTask>> GetByUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Include(task => task.AssignedToUser)
            .Where(task => task.AssignedToUserId == userId)
            .OrderByDescending(task => task.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(BaseTask task)
    {
        _context.Tasks.Add(task);
    }

    public void Remove(BaseTask task)
    {
        _context.Tasks.Remove(task);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
