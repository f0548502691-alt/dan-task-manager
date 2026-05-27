using MediatR;

namespace DanTaskManager.Application.Tasks.UserExists;

public record UserExistsQuery(int UserId) : IRequest<bool>;
