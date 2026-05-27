using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.DeleteTask;

public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, bool>
{
    private readonly ITaskApplicationService _taskApplicationService;

    public DeleteTaskCommandHandler(ITaskApplicationService taskApplicationService)
    {
        _taskApplicationService = taskApplicationService;
    }

    public Task<bool> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        return _taskApplicationService.DeleteAsync(request.TaskId, cancellationToken);
    }
}
