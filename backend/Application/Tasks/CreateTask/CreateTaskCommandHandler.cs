using DanTaskManager.Services;
using MediatR;
using ServiceTaskCreateCommand = DanTaskManager.Services.TaskCreateCommand;

namespace DanTaskManager.Application.Tasks.CreateTask;

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, TaskCreationResult>
{
    private readonly ITaskApplicationService _taskApplicationService;

    public CreateTaskCommandHandler(ITaskApplicationService taskApplicationService)
    {
        _taskApplicationService = taskApplicationService;
    }

    public Task<TaskCreationResult> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        return _taskApplicationService.CreateAsync(
            new ServiceTaskCreateCommand(
                request.TaskType,
                request.Description,
                request.AssignedToUserId,
                request.CustomDataJson),
            cancellationToken);
    }
}
