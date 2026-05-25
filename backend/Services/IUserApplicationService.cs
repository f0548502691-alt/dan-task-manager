using DanTaskManager.Domain;

namespace DanTaskManager.Services;

public interface IUserApplicationService
{
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AppUser?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserCreationResult> CreateAsync(UserCreateCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BaseTask>> GetUserTasksAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default);
}

public record UserCreateCommand(string Name, string Email);

public class UserCreationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public AppUser? CreatedUser { get; init; }

    public static UserCreationResult SuccessResult(AppUser user)
        => new() { Success = true, CreatedUser = user };

    public static UserCreationResult FailureResult(string message)
        => new() { Success = false, Message = message };
}
