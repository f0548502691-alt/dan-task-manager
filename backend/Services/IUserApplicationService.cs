namespace DanTaskManager.Services;

public interface IUserApplicationService
{
    Task<PagedResult<UserSummaryDto>> GetAllAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);
    Task<UserDetailsDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<PagedResult<TaskSummaryDto>> GetUserTasksAsync(
        int userId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default);
}
