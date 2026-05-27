using DanTaskManager.Domain;
using DanTaskManager.Services;

namespace DanTaskManager.Persistence;

public interface ITaskRepository
{
    Task<PagedResult<BaseTask>> GetPageAsync(
        PageRequest pageRequest,
        string? taskType = null,
        int? assignedToUserId = null,
        CancellationToken cancellationToken = default);

    Task<BaseTask?> GetByIdAsync(int taskId, CancellationToken cancellationToken = default);
    Task<BaseTask?> GetByIdReadOnlyAsync(int taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BaseTask>> GetByUserAsync(int userId, CancellationToken cancellationToken = default);
    void Add(BaseTask task);
    void Remove(BaseTask task);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
