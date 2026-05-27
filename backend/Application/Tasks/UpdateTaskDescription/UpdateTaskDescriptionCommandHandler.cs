using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.UpdateTaskDescription;

public class UpdateTaskDescriptionCommandHandler : IRequestHandler<UpdateTaskDescriptionCommand, bool>
{
    private readonly ITaskApplicationService _taskApplicationService;

    public UpdateTaskDescriptionCommandHandler(ITaskApplicationService taskApplicationService)
    {
        _taskApplicationService = taskApplicationService;
    }

    public Task<bool> Handle(UpdateTaskDescriptionCommand request, CancellationToken cancellationToken)
    {
        return _taskApplicationService.UpdateDescriptionAsync(
            request.TaskId,
            request.Description,
            cancellationToken);
    }
}
