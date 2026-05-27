using DanTaskManager.Services;

namespace DanTaskManager.Persistence;

public interface IUserRepository
{
    Task<PagedResult<UserSummaryDto>> GetPageAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);

    Task<UserDetailsDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default);
}
