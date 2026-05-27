using DanTaskManager.Domain;
using DanTaskManager.Persistence;

namespace DanTaskManager.Services;

public class UserApplicationService : IUserApplicationService
{
    private readonly IUserRepository _userRepository;
    private readonly ITaskRepository _taskRepository;

    public UserApplicationService(
        IUserRepository userRepository,
        ITaskRepository taskRepository)
    {
        _userRepository = userRepository;
        _taskRepository = taskRepository;
    }

    public Task<PagedResult<UserSummaryDto>> GetAllAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        return _userRepository.GetPageAsync(pageRequest, cancellationToken);
    }

    public Task<UserDetailsDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _userRepository.GetByIdAsync(userId, cancellationToken);
    }

    public async Task<PagedResult<TaskSummaryDto>> GetUserTasksAsync(
        int userId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var page = await _taskRepository.GetPageAsync(
            pageRequest,
            assignedToUserId: userId,
            cancellationToken: cancellationToken);
        var tasks = page.Items.Select(TaskDtoMappings.ToTaskSummaryDto).ToList();

        return PagedResult<TaskSummaryDto>.Create(tasks, page.TotalCount, page.Page, page.PageSize);
    }

    public Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _userRepository.ExistsAsync(userId, cancellationToken);
    }

}
