namespace DanTaskManager.Services;

public interface IUserApplicationService
{
    Task<PagedResult<UserSummaryDto>> GetAllAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);
    Task<UserDetailsDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserCreationResult> CreateAsync(UserCreateCommand command, CancellationToken cancellationToken = default);
    Task<PagedResult<TaskSummaryDto>> GetUserTasksAsync(
        int userId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default);
}

public record UserCreateCommand(string Name, string Email);

public class UserCreationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public UserDetailsDto? CreatedUser { get; init; }

    public static UserCreationResult SuccessResult(UserDetailsDto user)
        => new() { Success = true, CreatedUser = user };

    public static UserCreationResult FailureResult(string message)
        => new() { Success = false, Message = message };
}
