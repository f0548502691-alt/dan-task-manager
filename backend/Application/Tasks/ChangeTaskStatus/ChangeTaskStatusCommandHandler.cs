using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.ChangeTaskStatus;

public class ChangeTaskStatusCommandHandler : IRequestHandler<ChangeTaskStatusCommand, WorkflowResult>
{
    private readonly ITaskApplicationService _taskApplicationService;

    public ChangeTaskStatusCommandHandler(ITaskApplicationService taskApplicationService)
    {
        _taskApplicationService = taskApplicationService;
    }

    public Task<WorkflowResult> Handle(ChangeTaskStatusCommand request, CancellationToken cancellationToken)
    {
        return _taskApplicationService.ChangeStatusAsync(
            request.TaskId,
            request.NewStatus,
            request.NextAssignedToUserId,
            request.CustomDataJson,
            cancellationToken);
    }
}
