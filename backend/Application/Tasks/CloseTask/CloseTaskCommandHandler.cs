using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.CloseTask;

public class CloseTaskCommandHandler : IRequestHandler<CloseTaskCommand, WorkflowResult>
{
    private readonly ITaskApplicationService _taskApplicationService;

    public CloseTaskCommandHandler(ITaskApplicationService taskApplicationService)
    {
        _taskApplicationService = taskApplicationService;
    }

    public Task<WorkflowResult> Handle(CloseTaskCommand request, CancellationToken cancellationToken)
    {
        return _taskApplicationService.CloseAsync(
            request.TaskId,
            request.NextAssignedToUserId,
            request.FinalNotes,
            cancellationToken);
    }
}
