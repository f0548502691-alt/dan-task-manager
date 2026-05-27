using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.UserExists;

public class UserExistsQueryHandler : IRequestHandler<UserExistsQuery, bool>
{
    private readonly ITaskApplicationService _taskApplicationService;

    public UserExistsQueryHandler(ITaskApplicationService taskApplicationService)
    {
        _taskApplicationService = taskApplicationService;
    }

    public Task<bool> Handle(UserExistsQuery request, CancellationToken cancellationToken)
    {
        return _taskApplicationService.UserExistsAsync(request.UserId, cancellationToken);
    }
}
